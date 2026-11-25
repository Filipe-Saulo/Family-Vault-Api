using FamilyVaultApi.Data.Entities;
using FamilyVaultApi.Exceptions;
using FamilyVaultApi.Models.Internal;
using FamilyVaultApi.Models.Internal.Enums;
using FamilyVaultApi.Repositories.IRepository;
using FamilyVaultApi.Services.IService;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace FamilyVaultApi.Services.Service
{
    // Processa um documento por vez. O log nunca leva conteúdo, linha digitável ou valores extraídos.
    public class ChargeDocumentProcessor
    {
        private const int CandidatesPerCall = 10;
        // Corpo de e-mail maior que isso é cortado antes de ir para a IA (faturas trazem o resumo no começo)
        public const int EmailBodyMaxChars = 20_000;
        public const string EmailBodyContentType = "text/plain";
        public const string PasswordProtectedReason = "O PDF está protegido por senha.";
        private const string PasswordExpiredReason = "O prazo para informar a senha terminou.";
        private const string GenericFailureReason = "Não foi possível processar o documento.";
        private const string UnreadableReason = "O documento está ilegível.";
        private const string EmailGoneReason = "O e-mail não está mais disponível no Gmail.";

        private readonly IChargeRepository _repository;
        private readonly IChargeExtractor _extractor;
        private readonly ChargeProcessingOptions _options;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<ChargeDocumentProcessor> _logger;
        // Nulo quando o Gmail não está configurado: documento do Gmail volta à fila sem gastar tentativa
        private readonly GmailDocumentFetcher? _gmailFetcher;
        // Nulo sem o Gmail configurado: a senha lembrada por remetente só existe para documento do Gmail
        private readonly GmailTokenProtector? _protector;

        public ChargeDocumentProcessor(IChargeRepository repository, IChargeExtractor extractor, IOptions<ChargeProcessingOptions> options, TimeProvider timeProvider, ILogger<ChargeDocumentProcessor> logger,
            GmailDocumentFetcher? gmailFetcher = null, GmailTokenProtector? protector = null)
        {
            _repository = repository;
            _extractor = extractor;
            _options = options.Value;
            _timeProvider = timeProvider;
            _logger = logger;
            _gmailFetcher = gmailFetcher;
            _protector = protector;
        }

        // Upload que esperou a senha além do prazo vira Failed e perde o arquivo
        public async Task ExpirePasswordRequiredAsync()
        {
            var requiredBefore = _timeProvider.GetUtcNow().UtcDateTime.AddDays(-_options.PasswordRequired.RetentionDays);
            var expired = await _repository.ExpirePasswordRequiredUploadsAsync(requiredBefore, PasswordExpiredReason);

            if (expired > 0)
                _logger.LogInformation("{Count} upload(s) com senha passaram do prazo e terminaram com falha.", expired);
        }

        // Devolve o que ficou preso em Processing (reinício no meio do processamento)
        public async Task RequeueStuckAsync()
        {
            var startedBefore = _timeProvider.GetUtcNow().UtcDateTime.AddMinutes(-_options.Worker.StuckAfterMinutes);
            var requeued = await _repository.RequeueStuckDocumentsAsync(startedBefore, _options.Worker.MaxAttempts);

            if (requeued > 0)
                _logger.LogWarning("{Count} documento(s) preso(s) em processamento voltaram para a fila.", requeued);
        }

        // true se processou um documento
        public async Task<bool> ProcessNextAsync(CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var dayStart = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc);

            if (await _repository.CountDocumentsStartedSinceAsync(dayStart) >= _options.DailyLimits.Total)
                return false;

            var candidateIds = await _repository.GetPendingCandidateIdsAsync(dayStart, now.AddSeconds(-_options.Worker.RetryDelaySeconds), _options.DailyLimits.PerUser, CandidatesPerCall);

            foreach (var documentId in candidateIds)
            {
                // Outro worker pode ter pego este: tenta o próximo
                if (!await _repository.TryReserveDocumentAsync(documentId, now))
                    continue;

                var document = await _repository.GetDocumentAsync(documentId);
                if (document == null)
                    continue;

                // Gmail com o PDF já aberto pela senha informada: não baixa de novo
                if (document.Source == InboundDocumentSource.Gmail && document.TemporaryContent == null)
                {
                    if (_gmailFetcher == null)
                    {
                        await _repository.ReleaseDocumentAsync(documentId);
                        return false;
                    }

                    var content = await FetchFromGmailAsync(document, cancellationToken);
                    if (content != null)
                        await ProcessAsync(document, content, "application/pdf", now, cancellationToken);

                    return true;
                }

                await ProcessAsync(document, document.TemporaryContent ?? [], document.ContentType ?? "", now, cancellationToken);
                return true;
            }

            return false;
        }

        // null = o documento já foi tratado (devolvido à fila ou terminado com falha)
        private async Task<byte[]?> FetchFromGmailAsync(InboundDocument document, CancellationToken cancellationToken)
        {
            byte[] content;
            try
            {
                content = await _gmailFetcher!.FetchAsync(document, cancellationToken);
            }
            catch (GmailAccessRevokedException)
            {
                // Conexão revogada ou removida: espera a reconexão sem gastar tentativa
                await _repository.ReleaseDocumentAsync(document.InboundDocumentId);
                return null;
            }
            catch (GmailMessageNotFoundException)
            {
                await FailAsync(document, EmailGoneReason);
                return null;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError("Falha ao baixar do Gmail o documento {DocumentId} (tentativa {Attempt}): {ExceptionType}.", document.InboundDocumentId, document.Attempts, ex.GetType().Name);

                if (document.Attempts < _options.Worker.MaxAttempts)
                    await _repository.ReturnDocumentToPendingAsync(document.InboundDocumentId);
                else
                    await FailAsync(document, GenericFailureReason);

                return null;
            }

            if (!content.AsSpan().StartsWith("%PDF"u8))
            {
                await FailAsync(document, UnreadableReason);
                return null;
            }

            return content;
        }

        private async Task ProcessAsync(InboundDocument document, byte[] content, string contentType, DateTime now, CancellationToken cancellationToken)
        {
            if (contentType == "application/pdf" && IsPasswordProtected(content))
            {
                var unlocked = await TryUnlockWithoutAskingAsync(document, content);
                if (unlocked == null)
                {
                    // Gmail: antes de pedir a senha, tenta a conta pelo corpo do e-mail
                    if (document.Source == InboundDocumentSource.Gmail && await TryCompleteFromEmailBodyAsync(document, now, cancellationToken))
                        return;

                    // O upload guarda o arquivo até a senha (ou o prazo); o do Gmail é baixado de novo quando ela chegar
                    _logger.LogInformation("Documento {DocumentId} espera a senha do PDF.", document.InboundDocumentId);
                    await _repository.MarkPasswordRequiredAsync(document.InboundDocumentId, PasswordProtectedReason, now, keepContent: document.Source == InboundDocumentSource.Upload);
                    return;
                }

                content = unlocked;
            }

            var categories = await _repository.GetExpenseCategoriesAsync(document.UserId);

            ChargeExtractionResult result;
            try
            {
                result = await _extractor.ExtractAsync(new ChargeExtractionInput
                {
                    Content = content,
                    ContentType = contentType,
                    ExpenseCategories = categories
                }, cancellationToken);
            }
            catch (ChargeExtractionException ex)
            {
                await FailAsync(document, ex.Message);
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Sem a exceção inteira: a mensagem do extrator real pode trazer trecho da resposta da IA (valores, linha digitável)
                _logger.LogError("Falha ao interpretar o documento {DocumentId} (tentativa {Attempt}): {ExceptionType}.", document.InboundDocumentId, document.Attempts, ex.GetType().Name);

                if (document.Attempts < _options.Worker.MaxAttempts)
                    await _repository.ReturnDocumentToPendingAsync(document.InboundDocumentId);
                else
                    await FailAsync(document, GenericFailureReason);

                return;
            }

            if (!result.IsCharge)
            {
                await _repository.CompleteDocumentAsync(document.InboundDocumentId, InboundDocumentStatus.Done, null, null);
                return;
            }

            await CompleteWithChargeAsync(document, result, categories, ChargeExtractionSource.Document, now);
        }

        // PDF protegido do Gmail sem senha que abra: a IA lê o corpo do e-mail. true = o documento foi tratado
        // (conta criada, ou devolvido à fila por falha passageira da IA); false = segue para PasswordRequired.
        // O corpo é texto de terceiros: passa pelas mesmas validações da extração do PDF e nunca vai para o log.
        private async Task<bool> TryCompleteFromEmailBodyAsync(InboundDocument document, DateTime now, CancellationToken cancellationToken)
        {
            if (_gmailFetcher == null)
                return false;

            // O corpo descreve a fatura do e-mail: se outro anexo dele já virou conta (aberto ou pelo corpo), este espera a senha
            var messagePrefix = GmailDocumentFetcher.MessagePrefixOf(document.ExternalId);
            if (messagePrefix == null || await _repository.MessageHasChargeAsync(document.UserId, messagePrefix))
                return false;

            string? body;
            try
            {
                body = await _gmailFetcher.FetchBodyAsync(document, cancellationToken);
            }
            catch (GmailAccessRevokedException)
            {
                // Acesso retirado entre o download e a leitura do corpo: espera a reconexão sem gastar tentativa
                await _repository.ReleaseDocumentAsync(document.InboundDocumentId);
                return true;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // O corpo é um atalho: sem ele, a pessoa ainda informa a senha ou o valor
                _logger.LogWarning("Não foi possível ler o corpo do e-mail do documento {DocumentId}: {ExceptionType}.", document.InboundDocumentId, ex.GetType().Name);
                return false;
            }

            if (string.IsNullOrWhiteSpace(body))
                return false;

            if (body.Length > EmailBodyMaxChars)
                body = body[..(char.IsHighSurrogate(body[EmailBodyMaxChars - 1]) ? EmailBodyMaxChars - 1 : EmailBodyMaxChars)];

            var categories = await _repository.GetExpenseCategoriesAsync(document.UserId);

            ChargeExtractionResult result;
            try
            {
                result = await _extractor.ExtractAsync(new ChargeExtractionInput
                {
                    Content = Encoding.UTF8.GetBytes(body),
                    ContentType = EmailBodyContentType,
                    ExpenseCategories = categories
                }, cancellationToken);
            }
            catch (ChargeExtractionException)
            {
                // Corpo ilegível ou recusado: não é falha do documento, o PDF ainda pode ser aberto com a senha
                return false;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError("Falha ao interpretar o corpo do e-mail do documento {DocumentId} (tentativa {Attempt}): {ExceptionType}.", document.InboundDocumentId, document.Attempts, ex.GetType().Name);

                if (document.Attempts >= _options.Worker.MaxAttempts)
                    return false;

                await _repository.ReturnDocumentToPendingAsync(document.InboundDocumentId);
                return true;
            }

            // Só vira conta com valor e vencimento válidos; senão a pessoa informa a senha
            if (!result.IsCharge
                || ChargeValidator.ValidateAmount(result.Amount) == null
                || ChargeValidator.ValidateDueDate(result.DueDate, document.ReceivedAt) == null)
                return false;

            _logger.LogInformation("Documento {DocumentId} lido pelo corpo do e-mail.", document.InboundDocumentId);
            await CompleteWithChargeAsync(document, result, categories, ChargeExtractionSource.EmailBody, now);
            return true;
        }

        private async Task CompleteWithChargeAsync(InboundDocument document, ChargeExtractionResult result, IReadOnlyList<ChargeCategoryOption> categories,
            ChargeExtractionSource source, DateTime now)
        {
            var barcode = ChargeValidator.NormalizeBarcode(result.Barcode);
            if (barcode != null && await _repository.UserHasChargeWithBarcodeAsync(document.UserId, barcode))
            {
                // Mesma linha digitável não gera outra charge
                await _repository.CompleteDocumentAsync(document.InboundDocumentId, InboundDocumentStatus.Done, null, null);
                return;
            }

            var amount = ChargeValidator.ValidateAmount(result.Amount);
            var dueDate = ChargeValidator.ValidateDueDate(result.DueDate, document.ReceivedAt);

            int? suggestedCategoryId = result.SuggestedCategoryId.HasValue && categories.Any(c => c.Id == result.SuggestedCategoryId.Value)
                ? result.SuggestedCategoryId
                : null;

            int? possibleDuplicateId = null;
            if (amount.HasValue && dueDate.HasValue)
                possibleDuplicateId = await _repository.FindPossibleDuplicateTransactionIdAsync(document.UserId, amount.Value, dueDate.Value);

            int? possibleDuplicateChargeId = null;
            if (amount.HasValue && dueDate.HasValue)
                possibleDuplicateChargeId = await _repository.FindPossibleDuplicateChargeIdAsync(document.UserId, amount.Value, dueDate.Value,
                    document.Sender, ChargeValidator.NormalizeText(result.Issuer, 200), _options.DuplicateCharges.DueDateWindowDays);

            var charge = new Charge
            {
                UserId = document.UserId,
                Issuer = ChargeValidator.NormalizeText(result.Issuer, 200),
                Description = ChargeValidator.NormalizeText(result.Description, 500),
                Amount = amount,
                DueDate = dueDate,
                Barcode = barcode,
                SuggestedCategoryId = suggestedCategoryId,
                PossibleDuplicateTransactionId = possibleDuplicateId,
                PossibleDuplicateChargeId = possibleDuplicateChargeId,
                Status = ChargeStatus.PendingReview,
                ExtractionSource = source,
                CreatedAt = now
            };

            await _repository.CompleteDocumentAsync(document.InboundDocumentId, InboundDocumentStatus.Done, null, charge);
        }

        // Senha vazia (PDF só com senha de dono) e, no Gmail, a senha lembrada do remetente; null = precisa pedir
        private async Task<byte[]?> TryUnlockWithoutAskingAsync(InboundDocument document, byte[] content)
        {
            var unlocked = PdfUnlocker.TryUnlock(content, string.Empty);
            if (unlocked != null)
                return unlocked;

            if (document.Source != InboundDocumentSource.Gmail || _protector == null || string.IsNullOrEmpty(document.Sender))
                return null;

            var saved = await _repository.GetSenderPasswordAsync(document.UserId, document.Sender);
            if (saved == null)
                return null;

            string password;
            try
            {
                password = _protector.Unprotect(saved);
            }
            catch (Exception ex) when (ex is CryptographicException or FormatException)
            {
                // Chave trocada: a senha salva não serve mais; a pessoa informa de novo
                return null;
            }

            return PdfUnlocker.TryUnlock(content, password);
        }

        private async Task FailAsync(InboundDocument document, string reason)
        {
            _logger.LogWarning("Documento {DocumentId} terminou com falha.", document.InboundDocumentId);
            await _repository.CompleteDocumentAsync(document.InboundDocumentId, InboundDocumentStatus.Failed, reason, null);
        }

        // PDF criptografado traz no trailer (ou no dicionário do xref) a entrada /Encrypt apontando para o
        // dicionário de criptografia: referência "/Encrypt 5 0 R" ou dicionário direto "/Encrypt <<".
        // Exigir esse formato evita marcar como protegido um PDF que só cita a palavra no texto.
        private static readonly Regex EncryptEntry = new(@"/Encrypt\s*(\d+\s+\d+\s+R|<<)", RegexOptions.Compiled);

        private static bool IsPasswordProtected(byte[] content)
        {
            return EncryptEntry.IsMatch(Encoding.Latin1.GetString(content));
        }
    }
}
