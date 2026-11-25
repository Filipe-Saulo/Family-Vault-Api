using FamilyVaultApi.Data.Entities;
using FamilyVaultApi.Exceptions;
using FamilyVaultApi.Models.Dto;
using FamilyVaultApi.Models.Dto.Requests.Charge;
using FamilyVaultApi.Models.Dto.Requests.Transaction;
using FamilyVaultApi.Models.Dto.Responses.Charge;
using FamilyVaultApi.Models.Dto.Responses.TransactionResponse;
using FamilyVaultApi.Models.Internal;
using FamilyVaultApi.Models.Internal.Enums;
using FamilyVaultApi.Repositories.IRepository;
using FamilyVaultApi.Services.IService;
using Microsoft.Extensions.Options;
using System.Security;
using System.Security.Claims;
using System.Security.Cryptography;

namespace FamilyVaultApi.Services.Service
{
    public class ChargeService : IChargeService
    {
        private readonly IChargeRepository _repository;
        private readonly IGmailRepository _gmailRepository;
        private readonly ITransactionService _transactionService;
        private readonly ChargeProcessingOptions _options;
        private readonly TimeProvider _timeProvider;
        // Nulos sem o Gmail configurado: documento do Gmail não recebe senha e nenhuma senha é lembrada
        private readonly GmailDocumentFetcher? _gmailFetcher;
        private readonly GmailTokenProtector? _protector;

        public ChargeService(IChargeRepository repository, IGmailRepository gmailRepository, ITransactionService transactionService, IOptions<ChargeProcessingOptions> options, TimeProvider timeProvider,
            GmailDocumentFetcher? gmailFetcher = null, GmailTokenProtector? protector = null)
        {
            _repository = repository;
            _gmailRepository = gmailRepository;
            _transactionService = transactionService;
            _options = options.Value;
            _timeProvider = timeProvider;
            _gmailFetcher = gmailFetcher;
            _protector = protector;
        }

        public async Task<ChargeUploadResponseDto> UploadAsync(byte[] content, ClaimsPrincipal userClaims)
        {
            var userId = GetCallerId(userClaims);

            if (content == null || content.Length == 0)
                throw new BadRequestException("Envie um arquivo.");

            if (content.Length > _options.MaxUploadBytes)
                throw new BadRequestException($"O arquivo excede o tamanho máximo de {_options.MaxUploadBytes / (1024 * 1024)} MB.");

            // O tipo vem dos bytes iniciais, não do header enviado pelo cliente
            var contentType = DetectContentType(content)
                ?? throw new BadRequestException("Tipo de arquivo não suportado. Envie um PDF, JPEG ou PNG.");

            var externalId = "upload:" + Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

            var existing = await _repository.FindDocumentByExternalIdAsync(userId, externalId);
            if (existing != null)
            {
                // Mesmo arquivo de um documento que falhou: tenta de novo em vez de devolver a falha
                if (existing.Status == InboundDocumentStatus.Failed)
                    await _repository.TryRequeueFailedDocumentAsync(existing.InboundDocumentId, content, contentType);

                return new ChargeUploadResponseDto { DocumentId = existing.InboundDocumentId };
            }

            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var dayStart = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc);

            if (await _repository.CountDocumentsCreatedSinceAsync(userId, dayStart) >= _options.DailyLimits.PerUser)
                throw new TooManyRequestsException("Limite diário de documentos atingido. Tente novamente amanhã.");

            var document = await _repository.AddDocumentAsync(new InboundDocument
            {
                UserId = userId,
                Source = InboundDocumentSource.Upload,
                ExternalId = externalId,
                ReceivedAt = now,
                Status = InboundDocumentStatus.Pending,
                TemporaryContent = content,
                ContentType = contentType,
                CreatedAt = now
            });

            return new ChargeUploadResponseDto { DocumentId = document.InboundDocumentId };
        }

        public async Task<PagedResult<ChargeResponseDto>> GetAllAsync(ChargeQueryRequestDto query, ClaimsPrincipal userClaims)
        {
            var userId = GetCallerId(userClaims);

            query ??= new ChargeQueryRequestDto();
            NormalizePaging(query);

            return await _repository.GetAllAsync(userId, query);
        }

        public async Task<ChargeResponseDto> GetByIdAsync(int id, ClaimsPrincipal userClaims)
        {
            await EnsureOwnerAsync(id, userClaims);

            return await _repository.GetResponseByIdAsync(id)
                ?? throw new NotFoundException("Conta", id);
        }

        public async Task<PagedResult<ChargeFailureResponseDto>> GetFailuresAsync(PagedFilterDto query, ClaimsPrincipal userClaims)
        {
            var userId = GetCallerId(userClaims);

            query ??= new PagedFilterDto();
            NormalizePaging(query);

            return await _repository.GetFailuresAsync(userId, query, _options.PasswordRequired.MaxAttempts);
        }

        public async Task<TransactionResponseDto> ConfirmAsync(int id, ConfirmChargeDto dto, ClaimsPrincipal userClaims)
        {
            if (dto == null)
                throw new ArgumentNullException(nameof(dto));

            var ownerUserId = await EnsureOwnerAsync(id, userClaims);
            await EnsurePendingReviewAsync(id);

            TransactionResponseDto? transaction = null;

            // Despesa e charge Confirmed na mesma transação de banco; se outro confirmou antes, desfaz tudo
            await _repository.ExecuteInTransactionAsync(async () =>
            {
                transaction = await _transactionService.CreateAsync(new CreateTransactionDto
                {
                    UserId = ownerUserId,
                    CategoryId = dto.CategoryId,
                    TransactionTypeId = (int)TransactionTypeCode.Expense,
                    Description = dto.Description,
                    Amount = dto.Amount,
                    TransactionDate = dto.DueDate!.Value
                }, userClaims);

                if (!await _repository.TryConfirmAsync(id, transaction.TransactionId))
                    throw new ConflictException("Esta conta já foi confirmada ou descartada.");
            });

            return transaction!;
        }

        public async Task DismissAsync(int id, DismissChargeDto? dto, ClaimsPrincipal userClaims)
        {
            var ownerUserId = await EnsureOwnerAsync(id, userClaims);

            if (!await _repository.TryDismissAsync(id))
                throw new ConflictException("Esta conta já foi confirmada ou descartada.");

            // Só e-mail tem remetente; no upload, ignoreSender não tem efeito
            if (dto?.IgnoreSender == true)
            {
                var document = await _repository.GetDocumentSenderAsync(id);
                if (document is { Source: InboundDocumentSource.Gmail, Sender: { Length: > 0 and <= GmailIgnoredSender.MaxSenderLength } })
                    await _gmailRepository.AddIgnoredSenderAsync(ownerUserId, document.Value.Sender!, _timeProvider.GetUtcNow().UtcDateTime);
            }
        }

        // Informar a senha: abre o PDF na hora; certa, o documento volta à fila com o PDF já aberto
        public async Task<ChargeUploadResponseDto> SubmitDocumentPasswordAsync(int documentId, SubmitDocumentPasswordDto dto, ClaimsPrincipal userClaims, CancellationToken cancellationToken)
        {
            if (dto == null)
                throw new ArgumentNullException(nameof(dto));

            var document = await GetWaitingPasswordDocumentAsync(documentId, userClaims);
            var maxAttempts = _options.PasswordRequired.MaxAttempts;

            // Conferido antes de baixar do Gmail; a reserva abaixo decide de verdade
            if (document.PasswordAttempts >= maxAttempts)
                throw new TooManyRequestsException(PasswordAttemptsExhaustedMessage);

            var locked = await GetLockedContentAsync(document, cancellationToken);

            if (!await _repository.TryReservePasswordAttemptAsync(documentId, maxAttempts))
            {
                await GetWaitingPasswordDocumentAsync(documentId, userClaims);
                throw new TooManyRequestsException(PasswordAttemptsExhaustedMessage);
            }

            var unlocked = PdfUnlocker.TryUnlock(locked, dto.Password);
            if (unlocked == null)
            {
                // Relido depois da reserva: com pedidos em paralelo, o número lido antes estaria velho
                var attempts = (await _repository.GetDocumentAsync(documentId))?.PasswordAttempts ?? maxAttempts;
                var remaining = Math.Max(0, maxAttempts - attempts);
                throw new BadRequestException(remaining > 0
                    ? $"Senha incorreta. Restam {remaining} tentativa(s)."
                    : "Senha incorreta. " + PasswordAttemptsExhaustedMessage);
            }

            // Antes de devolver à fila: se gravar falhar, o documento continua esperando a senha e a pessoa tenta de novo.
            // Só e-mail tem remetente; no upload, rememberForSender não tem efeito
            if (dto.RememberForSender && _protector != null
                && document is { Source: InboundDocumentSource.Gmail, Sender: { Length: > 0 and <= GmailIgnoredSender.MaxSenderLength } })
            {
                await _repository.SaveSenderPasswordAsync(document.UserId, document.Sender, _protector.Protect(dto.Password), _timeProvider.GetUtcNow().UtcDateTime);
            }

            if (!await _repository.TryRequeueUnlockedDocumentAsync(documentId, unlocked))
                throw new ConflictException(NotWaitingPasswordMessage);

            return new ChargeUploadResponseDto { DocumentId = documentId };
        }

        // Informar o valor: despesa e charge já confirmada na mesma transação de banco; o documento termina Done
        public async Task<TransactionResponseDto> CreateManualChargeAsync(int documentId, ManualChargeDto dto, ClaimsPrincipal userClaims)
        {
            if (dto == null)
                throw new ArgumentNullException(nameof(dto));

            var document = await GetWaitingPasswordDocumentAsync(documentId, userClaims);
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            TransactionResponseDto? transaction = null;

            await _repository.ExecuteInTransactionAsync(async () =>
            {
                transaction = await _transactionService.CreateAsync(new CreateTransactionDto
                {
                    UserId = document.UserId,
                    CategoryId = dto.CategoryId,
                    TransactionTypeId = (int)TransactionTypeCode.Expense,
                    Description = dto.Description,
                    Amount = dto.Amount,
                    TransactionDate = dto.DueDate!.Value
                }, userClaims);

                var charge = new Charge
                {
                    UserId = document.UserId,
                    Issuer = ChargeValidator.NormalizeText(string.IsNullOrWhiteSpace(dto.Issuer) ? document.Sender : dto.Issuer, 200),
                    Description = ChargeValidator.NormalizeText(dto.Description, 500),
                    Amount = dto.Amount,
                    DueDate = dto.DueDate.Value,
                    SuggestedCategoryId = dto.CategoryId,
                    Status = ChargeStatus.Confirmed,
                    ExtractionSource = ChargeExtractionSource.Manual,
                    TransactionId = transaction.TransactionId,
                    CreatedAt = now
                };

                if (!await _repository.TryCompleteManuallyAsync(documentId, charge))
                    throw new ConflictException(NotWaitingPasswordMessage);
            });

            return transaction!;
        }

        private const string NotWaitingPasswordMessage = "Este documento não está esperando a senha.";
        private const string PasswordAttemptsExhaustedMessage = "Limite de tentativas de senha atingido. Informe o valor da conta.";
        private const string EmailGoneReason = "O e-mail não está mais disponível no Gmail.";

        // Documento é pessoal como a charge: nem o Administrator mexe no de outra pessoa
        private async Task<InboundDocument> GetWaitingPasswordDocumentAsync(int documentId, ClaimsPrincipal userClaims)
        {
            var callerId = GetCallerId(userClaims);

            var document = await _repository.GetDocumentAsync(documentId)
                ?? throw new NotFoundException("Documento", documentId);

            if (document.UserId != callerId)
                throw new SecurityException("Você não tem permissão para acessar este documento.");

            if (document.Status != InboundDocumentStatus.PasswordRequired)
                throw new ConflictException(NotWaitingPasswordMessage);

            return document;
        }

        // O PDF ainda trancado: o arquivo guardado do upload ou o anexo baixado de novo do Gmail
        private async Task<byte[]> GetLockedContentAsync(InboundDocument document, CancellationToken cancellationToken)
        {
            if (document.Source == InboundDocumentSource.Upload)
                return document.TemporaryContent is { Length: > 0 } content
                    ? content
                    : throw new ConflictException("O arquivo não está mais disponível. Envie o PDF de novo.");

            if (_gmailFetcher == null)
                throw new BadRequestException("A integração com o Gmail não está configurada.");

            try
            {
                return await _gmailFetcher.FetchAsync(document, cancellationToken);
            }
            catch (GmailAccessRevokedException)
            {
                throw new BadRequestException("Reconecte o Gmail para informar a senha.");
            }
            catch (GmailMessageNotFoundException)
            {
                await _repository.CompleteDocumentAsync(document.InboundDocumentId, InboundDocumentStatus.Failed, EmailGoneReason, null);
                throw new ConflictException(EmailGoneReason);
            }
        }

        // Charge é pessoal: nem o Administrator mexe na de outro usuário
        private async Task<string> EnsureOwnerAsync(int chargeId, ClaimsPrincipal userClaims)
        {
            var callerId = GetCallerId(userClaims);

            var ownerUserId = await _repository.GetOwnerUserIdAsync(chargeId);
            if (ownerUserId == null)
                throw new NotFoundException("Conta", chargeId);

            if (ownerUserId != callerId)
                throw new SecurityException("Você não tem permissão para acessar esta conta.");

            return ownerUserId;
        }

        private async Task EnsurePendingReviewAsync(int chargeId)
        {
            var charge = await _repository.GetResponseByIdAsync(chargeId)
                ?? throw new NotFoundException("Conta", chargeId);

            if (charge.Status != nameof(ChargeStatus.PendingReview))
                throw new ConflictException("Esta conta já foi confirmada ou descartada.");
        }

        private static string GetCallerId(ClaimsPrincipal userClaims)
        {
            var callerId = userClaims.FindFirst("uid")?.Value;
            if (string.IsNullOrEmpty(callerId))
                throw new SecurityException("Você não tem permissão para acessar contas.");

            return callerId;
        }

        private static void NormalizePaging(PagedFilterDto query)
        {
            if (query.PageNumber <= 0) query.PageNumber = 1;
            if (query.PageSize <= 0) query.PageSize = 20;
            else if (query.PageSize > 100) query.PageSize = 100;
        }

        // %PDF, FF D8 FF (JPEG) e 89 50 4E 47 (PNG)
        private static string? DetectContentType(byte[] content)
        {
            if (content.AsSpan().StartsWith("%PDF"u8))
                return "application/pdf";
            if (content.AsSpan().StartsWith(new byte[] { 0xFF, 0xD8, 0xFF }))
                return "image/jpeg";
            if (content.AsSpan().StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47 }))
                return "image/png";

            return null;
        }
    }
}
