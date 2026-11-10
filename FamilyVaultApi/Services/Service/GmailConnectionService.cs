using AutoMapper;
using FamilyVaultApi.Data.Entities;
using FamilyVaultApi.Exceptions;
using FamilyVaultApi.Models.Dto.Requests.Gmail;
using FamilyVaultApi.Models.Dto.Responses.Gmail;
using FamilyVaultApi.Models.Internal;
using FamilyVaultApi.Models.Internal.Enums;
using FamilyVaultApi.Repositories.IRepository;
using FamilyVaultApi.Services.IService;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using System.Security;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace FamilyVaultApi.Services.Service
{
    // Conexão com o Gmail (OAuth no servidor, escopo gmail.readonly) e as verificações pedidas pela pessoa.
    // A leitura do Gmail é do worker (GmailScanProcessor); aqui só se cria a verificação.
    public class GmailConnectionService : IGmailConnectionService
    {
        private const string NotConfiguredMessage = "A integração com o Gmail não está configurada.";

        private readonly IGmailRepository _repository;
        private readonly IServiceProvider _services;
        private readonly GmailOptions _options;
        private readonly TimeProvider _timeProvider;
        private readonly IMapper _mapper;
        private readonly ILogger<GmailConnectionService> _logger;

        // O cliente do Google e o protetor só existem com o Gmail configurado: resolvidos na hora do uso
        public GmailConnectionService(IGmailRepository repository, IServiceProvider services, IOptions<GmailOptions> options,
            TimeProvider timeProvider, IMapper mapper, ILogger<GmailConnectionService> logger)
        {
            _repository = repository;
            _services = services;
            _options = options.Value;
            _timeProvider = timeProvider;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<GmailConnectResponseDto> ConnectAsync(GmailConnectRequestDto? dto, ClaimsPrincipal userClaims)
        {
            var userId = GetCallerId(userClaims);
            var client = RequireClient();

            var returnUrl = string.IsNullOrWhiteSpace(dto?.ReturnUrl) ? _options.AllowedReturnUrls[0] : dto.ReturnUrl.Trim();
            if (!_options.AllowedReturnUrls.Contains(returnUrl, StringComparer.Ordinal))
                throw new BadRequestException("Endereço de retorno não permitido.");

            var state = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
            var now = Now();

            await _repository.AddStateAsync(new GmailOAuthState
            {
                UserId = userId,
                StateHash = Hash(state),
                ReturnUrl = returnUrl,
                ExpiresAt = now.AddMinutes(_options.StateLifetimeMinutes)
            }, now);

            return new GmailConnectResponseDto { AuthorizationUrl = client.BuildAuthorizationUrl(state) };
        }

        public async Task<string> HandleCallbackAsync(string? code, string? state, string? error, CancellationToken cancellationToken)
        {
            var defaultReturnUrl = _options.AllowedReturnUrls.FirstOrDefault() ?? "/";
            var client = _services.GetService<IGmailApiClient>();
            var protector = _services.GetService<GmailTokenProtector>();

            if (client == null || protector == null || string.IsNullOrEmpty(state))
                return WithResult(defaultReturnUrl, "error", "state");

            var now = Now();
            var saved = await _repository.ConsumeStateAsync(Hash(state), now);
            if (saved == null)
                return WithResult(defaultReturnUrl, "error", "state");

            // A pessoa recusou na tela do Google (error=access_denied) ou o Google não mandou o code
            if (!string.IsNullOrEmpty(error) || string.IsNullOrEmpty(code))
                return WithResult(saved.ReturnUrl, "error", "denied");

            try
            {
                var authorization = await client.ExchangeCodeAsync(code, cancellationToken);

                if (!authorization.HasGmailReadScope || string.IsNullOrEmpty(authorization.RefreshToken))
                {
                    // Sem o escopo do Gmail o token não serve: devolve ao Google em vez de guardar
                    if (!string.IsNullOrEmpty(authorization.RefreshToken))
                        await TryRevokeAsync(client, authorization.RefreshToken, cancellationToken);

                    return WithResult(saved.ReturnUrl, "error", "scope");
                }

                await _repository.ConnectAsync(saved.UserId, authorization.GoogleEmail, protector.Protect(authorization.RefreshToken),
                    NewScan(saved.UserId, GmailScanKind.Initial, now.AddDays(-_options.InitialScanDays), now), now);

                return WithResult(saved.ReturnUrl, "connected", null);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Sem a exceção inteira: a resposta do Google pode trazer o code
                _logger.LogError("Falha ao concluir a conexão com o Gmail: {ExceptionType}.", ex.GetType().Name);
                return WithResult(saved.ReturnUrl, "error", "failed");
            }
        }

        public async Task<GmailStatusResponseDto> GetStatusAsync(ClaimsPrincipal userClaims)
        {
            var userId = GetCallerId(userClaims);
            var connection = await _repository.GetConnectionAsync(userId);

            return connection == null
                ? new GmailStatusResponseDto { Connected = false }
                : _mapper.Map<GmailStatusResponseDto>(connection);
        }

        public async Task DisconnectAsync(ClaimsPrincipal userClaims, CancellationToken cancellationToken)
        {
            var userId = GetCallerId(userClaims);
            var connection = await _repository.GetConnectionAsync(userId);
            if (connection == null)
                return;

            // Revoga no Google antes de apagar; se o Google falhar (token já revogado, rede), apaga do mesmo jeito
            var client = _services.GetService<IGmailApiClient>();
            var protector = _services.GetService<GmailTokenProtector>();
            if (client != null && protector != null)
            {
                try
                {
                    await TryRevokeAsync(client, protector.Unprotect(connection.RefreshTokenEncrypted), cancellationToken);
                }
                catch (Exception ex) when (ex is CryptographicException or FormatException)
                {
                    _logger.LogWarning("Token do Gmail ilegível ao desconectar: {ExceptionType}.", ex.GetType().Name);
                }
            }

            await _repository.DeleteConnectionAsync(userId);
        }

        public async Task<GmailScanCreatedResponseDto> StartScanAsync(ClaimsPrincipal userClaims)
        {
            var userId = GetCallerId(userClaims);
            RequireClient();

            var connection = await _repository.GetConnectionAsync(userId);
            if (connection == null)
                throw new BadRequestException("Conecte o Gmail antes de buscar contas.");
            if (connection.Status != GmailConnectionStatus.Active)
                throw new BadRequestException("O acesso ao Gmail expirou. Conecte o Gmail de novo.");

            if (await _repository.HasRunningScanAsync(userId))
                throw new ConflictException("Já existe uma verificação em andamento.");

            var now = Now();
            if (!await _repository.TryMarkScanRequestedAsync(userId, now.AddSeconds(-_options.ManualScanMinIntervalSeconds), now))
                throw new TooManyRequestsException("Aguarde um minuto entre as verificações.");

            // Sem verificação inicial concluída, o botão refaz a inicial
            var scan = connection.InitialScanCompletedAt == null
                ? NewScan(userId, GmailScanKind.Initial, now.AddDays(-_options.InitialScanDays), now)
                : NewScan(userId, GmailScanKind.Manual, ManualWindowStart(connection.SyncedUntil, now), now);

            scan = await _repository.AddScanAsync(scan);
            return new GmailScanCreatedResponseDto { ScanId = scan.GmailScanId };
        }

        public async Task<GmailScanResponseDto> GetScanAsync(int scanId, ClaimsPrincipal userClaims)
        {
            var userId = GetCallerId(userClaims);

            var scan = await _repository.GetScanAsync(scanId)
                ?? throw new NotFoundException("Verificação", scanId);

            if (scan.UserId != userId)
                throw new SecurityException("Você não tem permissão para acessar esta verificação.");

            return _mapper.Map<GmailScanResponseDto>(scan);
        }

        public async Task<List<GmailIgnoredSenderResponseDto>> ListIgnoredSendersAsync(ClaimsPrincipal userClaims)
        {
            var userId = GetCallerId(userClaims);
            var senders = await _repository.ListIgnoredSendersAsync(userId);
            return _mapper.Map<List<GmailIgnoredSenderResponseDto>>(senders);
        }

        public async Task DeleteIgnoredSenderAsync(int id, ClaimsPrincipal userClaims)
        {
            var userId = GetCallerId(userClaims);

            var sender = await _repository.GetIgnoredSenderAsync(id)
                ?? throw new NotFoundException("Remetente ignorado", id);

            if (sender.UserId != userId)
                throw new SecurityException("Você não tem permissão para acessar este remetente.");

            await _repository.DeleteIgnoredSenderAsync(id);
        }

        // Do ponto salvo menos a margem de atraso até agora, no máximo a janela do botão (30 dias)
        private DateTime ManualWindowStart(DateTime? syncedUntil, DateTime now)
        {
            var oldest = now.AddDays(-_options.ManualScanMaxDays);
            if (syncedUntil == null)
                return oldest;

            var start = syncedUntil.Value.AddMinutes(-_options.LookbackMinutes);
            return start < oldest ? oldest : start;
        }

        private static GmailScan NewScan(string userId, GmailScanKind kind, DateTime windowStart, DateTime now) => new()
        {
            UserId = userId,
            Kind = kind,
            WindowStart = windowStart,
            // O fim da janela é o início da verificação: vira o ponto salvo quando ela termina
            WindowEnd = now,
            Status = GmailScanStatus.Running,
            CreatedAt = now
        };

        private async Task TryRevokeAsync(IGmailApiClient client, string refreshToken, CancellationToken cancellationToken)
        {
            try
            {
                await client.RevokeAsync(refreshToken, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning("Não foi possível revogar o acesso ao Gmail no Google: {ExceptionType}.", ex.GetType().Name);
            }
        }

        private IGmailApiClient RequireClient() =>
            _services.GetService<IGmailApiClient>() ?? throw new BadRequestException(NotConfiguredMessage);

        private DateTime Now() => _timeProvider.GetUtcNow().UtcDateTime;

        private static string Hash(string value) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

        private static string WithResult(string returnUrl, string result, string? reason)
        {
            var url = QueryHelpers.AddQueryString(returnUrl, "gmail", result);
            return reason == null ? url : QueryHelpers.AddQueryString(url, "reason", reason);
        }

        private static string GetCallerId(ClaimsPrincipal userClaims)
        {
            var callerId = userClaims.FindFirst("uid")?.Value;
            if (string.IsNullOrEmpty(callerId))
                throw new SecurityException("Você não tem permissão para acessar o Gmail.");

            return callerId;
        }
    }
}
