using FamilyVaultApi.Data.Entities;
using FamilyVaultApi.Models.Internal;
using FamilyVaultApi.Models.Internal.Enums;
using FamilyVaultApi.Repositories.IRepository;
using FamilyVaultApi.Services.IService;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace FamilyVaultApi.Services.Service
{
    // Etapa 1 do worker: pega uma verificação Running, lista os e-mails da janela no Gmail e cria um documento
    // Pending por PDF de e-mail novo. Nada é baixado nem enviado à IA aqui. O log nunca leva remetente, assunto ou token.
    public class GmailScanProcessor
    {
        // Termos de cobrança da busca; o Gmail não diferencia maiúsculas nem acentos
        private const string ChargeTerms = "(boleto OR fatura OR vencimento OR \"segunda via\")";

        // Só endereços simples entram no -from: da busca; os demais são filtrados no código
        private static readonly Regex SimpleAddress = new(@"^[a-z0-9._%+\-]+@[a-z0-9.\-]+$", RegexOptions.Compiled);

        private readonly IGmailRepository _repository;
        private readonly IGmailApiClient _client;
        private readonly GmailTokenProtector _protector;
        private readonly GmailOptions _options;
        private readonly ChargeProcessingOptions _chargeOptions;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<GmailScanProcessor> _logger;

        public GmailScanProcessor(IGmailRepository repository, IGmailApiClient client, GmailTokenProtector protector,
            IOptions<GmailOptions> options, IOptions<ChargeProcessingOptions> chargeOptions, TimeProvider timeProvider,
            ILogger<GmailScanProcessor> logger)
        {
            _repository = repository;
            _client = client;
            _protector = protector;
            _options = options.Value;
            _chargeOptions = chargeOptions.Value;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        // true se tratou uma verificação
        public async Task<bool> ProcessNextScanAsync(CancellationToken cancellationToken)
        {
            var scan = await _repository.GetNextRunningScanAsync();
            if (scan == null)
                return false;

            var connection = await _repository.GetConnectionAsync(scan.UserId);
            if (connection == null || connection.Status != GmailConnectionStatus.Active)
            {
                await _repository.FailScanAsync(scan.GmailScanId);
                return true;
            }

            try
            {
                await ScanAsync(scan, connection, cancellationToken);
                await _repository.CompleteScanAsync(scan, _timeProvider.GetUtcNow().UtcDateTime);
            }
            catch (Exception ex) when (ex is GmailAccessRevokedException or CryptographicException or FormatException)
            {
                // invalid_grant, ou chave de criptografia trocada: a pessoa precisa reconectar
                _logger.LogWarning("Acesso ao Gmail indisponível na verificação {ScanId}: {ExceptionType}.", scan.GmailScanId, ex.GetType().Name);
                await _repository.MarkRevokedAsync(scan.UserId);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Falha não avança o ponto salvo; a pessoa pode pedir de novo
                _logger.LogError("Falha na verificação {ScanId} do Gmail: {ExceptionType}.", scan.GmailScanId, ex.GetType().Name);
                await _repository.FailScanAsync(scan.GmailScanId);
            }

            return true;
        }

        private async Task ScanAsync(GmailScan scan, GmailConnection connection, CancellationToken cancellationToken)
        {
            var accessToken = await _client.GetAccessTokenAsync(_protector.Unprotect(connection.RefreshTokenEncrypted), cancellationToken);

            var ignored = (await _repository.ListIgnoredSendersAsync(scan.UserId))
                .Select(s => s.Sender)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var messageIds = await _client.ListMessageIdsAsync(accessToken, BuildQuery(scan.WindowStart, scan.WindowEnd, ignored),
                _options.MaxMessagesPerScan, cancellationToken);

            if (messageIds.Count >= _options.MaxMessagesPerScan)
                _logger.LogWarning("A verificação {ScanId} chegou ao teto de {Max} e-mails; os mais antigos da janela ficaram de fora.", scan.GmailScanId, _options.MaxMessagesPerScan);

            // Initial: os 50 documentos mais recentes (o Gmail lista do mais novo para o mais antigo)
            var limit = scan.Kind == GmailScanKind.Initial ? _options.InitialScanMaxDocuments : int.MaxValue;
            var created = 0;

            foreach (var messageId in messageIds)
            {
                if (created >= limit)
                    break;

                // Desconectou ou reconectou no meio: para, sem criar documento órfão
                if (!await _repository.IsScanRunningAsync(scan.GmailScanId))
                    return;

                // E-mail já lido é pulado pelo id, sem baixar de novo nem gastar IA
                if (await _repository.HasDocumentForMessageAsync(scan.UserId, GmailDocumentFetcher.MessagePrefix(messageId)))
                    continue;

                GmailMessageInfo message;
                try
                {
                    message = await _client.GetMessageAsync(accessToken, messageId, cancellationToken);
                }
                catch (GmailMessageNotFoundException)
                {
                    continue;
                }

                if (message.Sender != null && ignored.Contains(message.Sender))
                    continue;

                var now = _timeProvider.GetUtcNow().UtcDateTime;
                var documents = message.PdfAttachments
                    .Take(limit - created)
                    .Select(attachment => NewDocument(scan, message, attachment, now))
                    .ToList();

                var added = await _repository.AddDocumentsAsync(documents);
                created += added;

                if (added > 0)
                    await _repository.UpdateScanCountsAsync(scan.GmailScanId);
            }
        }

        private InboundDocument NewDocument(GmailScan scan, GmailMessageInfo message, GmailPdfAttachment attachment, DateTime now)
        {
            var oversize = attachment.Size > _chargeOptions.MaxUploadBytes;

            return new InboundDocument
            {
                UserId = scan.UserId,
                Source = InboundDocumentSource.Gmail,
                ExternalId = GmailDocumentFetcher.BuildExternalId(message.MessageId, attachment.PartId),
                Sender = message.Sender,
                ReceivedAt = message.ReceivedAt,
                // PDF grande demais nem é baixado: aparece em /api/charge/failures
                Status = oversize ? InboundDocumentStatus.Failed : InboundDocumentStatus.Pending,
                FailureReason = oversize ? $"O PDF excede o tamanho máximo de {_chargeOptions.MaxUploadBytes / (1024 * 1024)} MB." : null,
                ContentType = "application/pdf",
                GmailScanId = scan.GmailScanId,
                CreatedAt = now
            };
        }

        public static string BuildQuery(DateTime windowStart, DateTime windowEnd, IEnumerable<string> ignoredSenders)
        {
            var after = new DateTimeOffset(DateTime.SpecifyKind(windowStart, DateTimeKind.Utc)).ToUnixTimeSeconds();
            // before: é exclusivo; +1 s inclui o que chegou no último segundo da janela
            var before = new DateTimeOffset(DateTime.SpecifyKind(windowEnd, DateTimeKind.Utc)).ToUnixTimeSeconds() + 1;

            var parts = new List<string>
            {
                "has:attachment",
                "filename:pdf",
                ChargeTerms,
                "after:" + after.ToString(CultureInfo.InvariantCulture),
                "before:" + before.ToString(CultureInfo.InvariantCulture)
            };

            parts.AddRange(ignoredSenders
                .Select(s => s.ToLowerInvariant())
                .Where(s => SimpleAddress.IsMatch(s))
                .Select(s => "-from:" + s));

            return string.Join(' ', parts);
        }
    }
}
