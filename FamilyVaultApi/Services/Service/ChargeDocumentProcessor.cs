using FamilyVaultApi.Data.Entities;
using FamilyVaultApi.Exceptions;
using FamilyVaultApi.Models.Internal;
using FamilyVaultApi.Models.Internal.Enums;
using FamilyVaultApi.Repositories.IRepository;
using FamilyVaultApi.Services.IService;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.RegularExpressions;

namespace FamilyVaultApi.Services.Service
{
    // Processa um documento por vez. O log nunca leva conteúdo, linha digitável ou valores extraídos.
    public class ChargeDocumentProcessor
    {
        private const int CandidatesPerCall = 10;
        private const string PasswordProtectedReason = "O PDF está protegido por senha.";
        private const string GenericFailureReason = "Não foi possível processar o documento.";

        private readonly IChargeRepository _repository;
        private readonly IChargeExtractor _extractor;
        private readonly ChargeProcessingOptions _options;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<ChargeDocumentProcessor> _logger;

        public ChargeDocumentProcessor(IChargeRepository repository, IChargeExtractor extractor, IOptions<ChargeProcessingOptions> options, TimeProvider timeProvider, ILogger<ChargeDocumentProcessor> logger)
        {
            _repository = repository;
            _extractor = extractor;
            _options = options.Value;
            _timeProvider = timeProvider;
            _logger = logger;
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

                if (document.Source != InboundDocumentSource.Upload)
                {
                    await _repository.ReturnDocumentToPendingAsync(documentId);
                    return false;
                }

                await ProcessAsync(document, now, cancellationToken);
                return true;
            }

            return false;
        }

        private async Task ProcessAsync(InboundDocument document, DateTime now, CancellationToken cancellationToken)
        {
            var content = document.TemporaryContent ?? [];
            var contentType = document.ContentType ?? "";

            if (contentType == "application/pdf" && IsPasswordProtected(content))
            {
                await FailAsync(document, PasswordProtectedReason);
                return;
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
                Status = ChargeStatus.PendingReview,
                CreatedAt = now
            };

            await _repository.CompleteDocumentAsync(document.InboundDocumentId, InboundDocumentStatus.Done, null, charge);
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
