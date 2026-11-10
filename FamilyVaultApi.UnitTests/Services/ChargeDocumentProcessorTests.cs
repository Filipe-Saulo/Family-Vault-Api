using System.Text;
using FamilyVaultApi.Data.Entities;
using FamilyVaultApi.Exceptions;
using FamilyVaultApi.Models.Internal;
using FamilyVaultApi.Models.Internal.Enums;
using FamilyVaultApi.Repositories.IRepository;
using FamilyVaultApi.Services.IService;
using FamilyVaultApi.Services.Service;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace FamilyVaultApi.UnitTests.Services
{
    public class ChargeDocumentProcessorTests
    {
        private const string UserId = "user-1";

        private readonly Mock<IChargeRepository> _repository = new();
        private readonly Mock<IChargeExtractor> _extractor = new();
        private readonly ChargeProcessingOptions _options = new();
        private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 10, 15, 0, 0, TimeSpan.Zero));
        private readonly ChargeDocumentProcessor _processor;

        public ChargeDocumentProcessorTests()
        {
            _processor = new ChargeDocumentProcessor(_repository.Object, _extractor.Object, Options.Create(_options), _time,
                NullLogger<ChargeDocumentProcessor>.Instance);
        }

        private static readonly List<ChargeCategoryOption> Categories = [new(2, "Moradia"), new(3, "Mercado")];

        private InboundDocument Document(int id = 1, int attempts = 1, string contentType = "application/pdf",
            byte[]? content = null, InboundDocumentSource source = InboundDocumentSource.Upload) => new()
        {
            InboundDocumentId = id,
            UserId = UserId,
            Source = source,
            Attempts = attempts,
            ContentType = contentType,
            TemporaryContent = content ?? Encoding.ASCII.GetBytes("%PDF-1.4 conteudo"),
            ReceivedAt = _time.Now.UtcDateTime,
            Status = InboundDocumentStatus.Processing
        };

        // Candidato único reservado com sucesso
        private void SetupReservedDocument(InboundDocument document)
        {
            _repository.Setup(r => r.GetPendingCandidateIdsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<int>()))
                .ReturnsAsync([document.InboundDocumentId]);
            _repository.Setup(r => r.TryReserveDocumentAsync(document.InboundDocumentId, It.IsAny<DateTime>())).ReturnsAsync(true);
            _repository.Setup(r => r.GetDocumentAsync(document.InboundDocumentId)).ReturnsAsync(document);
            _repository.Setup(r => r.GetExpenseCategoriesAsync(UserId)).ReturnsAsync(Categories);
        }

        private void SetupExtractor(ChargeExtractionResult result) =>
            _extractor.Setup(e => e.ExtractAsync(It.IsAny<ChargeExtractionInput>(), It.IsAny<CancellationToken>())).ReturnsAsync(result);

        private Charge? CapturedCharge;

        private void CaptureCompletion()
        {
            _repository.Setup(r => r.CompleteDocumentAsync(It.IsAny<int>(), It.IsAny<InboundDocumentStatus>(), It.IsAny<string?>(), It.IsAny<Charge?>()))
                .Callback((int _, InboundDocumentStatus _, string? _, Charge? c) => CapturedCharge = c)
                .Returns(Task.CompletedTask);
        }

        // ---------- Limites do dia ----------

        [Fact]
        public async Task ProcessNextAsync_TotalLimitReached_ShouldReturnFalseWithoutLookingForCandidates()
        {
            _repository.Setup(r => r.CountDocumentsStartedSinceAsync(It.IsAny<DateTime>())).ReturnsAsync(100);

            var processed = await _processor.ProcessNextAsync(CancellationToken.None);

            processed.Should().BeFalse();
            _repository.Verify(r => r.GetPendingCandidateIdsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task ProcessNextAsync_BelowTotalLimit_ShouldLookForCandidates()
        {
            _repository.Setup(r => r.CountDocumentsStartedSinceAsync(It.IsAny<DateTime>())).ReturnsAsync(99);
            _repository.Setup(r => r.GetPendingCandidateIdsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<int>())).ReturnsAsync([]);

            var processed = await _processor.ProcessNextAsync(CancellationToken.None);

            processed.Should().BeFalse();
            _repository.Verify(r => r.GetPendingCandidateIdsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), _options.DailyLimits.PerUser, It.IsAny<int>()), Times.Once);
        }

        [Fact]
        public async Task ProcessNextAsync_ShouldCountFromUtcDayStartAndFollowTheClock()
        {
            _time.Now = new DateTimeOffset(2026, 11, 10, 23, 59, 59, TimeSpan.Zero);
            _repository.Setup(r => r.GetPendingCandidateIdsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<int>())).ReturnsAsync([]);

            await _processor.ProcessNextAsync(CancellationToken.None);
            _time.Now = new DateTimeOffset(2026, 11, 11, 0, 0, 0, TimeSpan.Zero);
            await _processor.ProcessNextAsync(CancellationToken.None);

            var day10 = new DateTime(2026, 11, 10, 0, 0, 0, DateTimeKind.Utc);
            var day11 = new DateTime(2026, 11, 11, 0, 0, 0, DateTimeKind.Utc);
            _repository.Verify(r => r.CountDocumentsStartedSinceAsync(day10), Times.Once);
            _repository.Verify(r => r.CountDocumentsStartedSinceAsync(day11), Times.Once);
            _repository.Verify(r => r.GetPendingCandidateIdsAsync(day10, It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<int>()), Times.Once);
            _repository.Verify(r => r.GetPendingCandidateIdsAsync(day11, It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<int>()), Times.Once);
        }

        [Fact]
        public async Task ProcessNextAsync_NoCandidates_ShouldReturnFalse()
        {
            _repository.Setup(r => r.GetPendingCandidateIdsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<int>())).ReturnsAsync([]);

            (await _processor.ProcessNextAsync(CancellationToken.None)).Should().BeFalse();
        }

        // ---------- Reserva ----------

        [Fact]
        public async Task ProcessNextAsync_ReservationLostToAnother_ShouldTryTheNextCandidate()
        {
            var second = Document(id: 2);
            SetupReservedDocument(second);
            _repository.Setup(r => r.GetPendingCandidateIdsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<int>())).ReturnsAsync([1, 2]);
            _repository.Setup(r => r.TryReserveDocumentAsync(1, It.IsAny<DateTime>())).ReturnsAsync(false);
            SetupExtractor(new ChargeExtractionResult { IsCharge = false });

            var processed = await _processor.ProcessNextAsync(CancellationToken.None);

            processed.Should().BeTrue();
            _repository.Verify(r => r.GetDocumentAsync(1), Times.Never);
            _repository.Verify(r => r.CompleteDocumentAsync(2, InboundDocumentStatus.Done, null, null), Times.Once);
        }

        [Fact]
        public async Task ProcessNextAsync_ReservationLostForAllCandidates_ShouldReturnFalse()
        {
            _repository.Setup(r => r.GetPendingCandidateIdsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<int>())).ReturnsAsync([1, 2]);
            _repository.Setup(r => r.TryReserveDocumentAsync(It.IsAny<int>(), It.IsAny<DateTime>())).ReturnsAsync(false);

            (await _processor.ProcessNextAsync(CancellationToken.None)).Should().BeFalse();
            _extractor.Verify(e => e.ExtractAsync(It.IsAny<ChargeExtractionInput>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ProcessNextAsync_ReservesWithTheCurrentTime()
        {
            SetupReservedDocument(Document());
            SetupExtractor(new ChargeExtractionResult { IsCharge = false });

            await _processor.ProcessNextAsync(CancellationToken.None);

            _repository.Verify(r => r.TryReserveDocumentAsync(1, _time.Now.UtcDateTime), Times.Once);
        }

        [Fact]
        public async Task ProcessNextAsync_GmailDocumentWithoutGmailConfigured_ShouldReleaseAndReturnFalse()
        {
            SetupReservedDocument(Document(source: InboundDocumentSource.Gmail));

            var processed = await _processor.ProcessNextAsync(CancellationToken.None);

            processed.Should().BeFalse();
            _repository.Verify(r => r.ReleaseDocumentAsync(1), Times.Once);
            _extractor.Verify(e => e.ExtractAsync(It.IsAny<ChargeExtractionInput>(), It.IsAny<CancellationToken>()), Times.Never);
            _repository.Verify(r => r.CompleteDocumentAsync(It.IsAny<int>(), It.IsAny<InboundDocumentStatus>(), It.IsAny<string?>(), It.IsAny<Charge?>()), Times.Never);
        }

        // ---------- Extração e falhas ----------

        [Fact]
        public async Task ProcessNextAsync_EncryptedPdf_ShouldFailWithoutCallingExtractor()
        {
            SetupReservedDocument(Document(content: Encoding.ASCII.GetBytes("%PDF-1.7 << /Encrypt 5 0 R >>")));

            var processed = await _processor.ProcessNextAsync(CancellationToken.None);

            processed.Should().BeTrue();
            _repository.Verify(r => r.CompleteDocumentAsync(1, InboundDocumentStatus.Failed, "O PDF está protegido por senha.", null), Times.Once);
            _extractor.Verify(e => e.ExtractAsync(It.IsAny<ChargeExtractionInput>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Theory]
        [InlineData("%PDF-1.7 << /Encrypt << /Filter /Standard >> >>")]
        [InlineData("%PDF-1.7 trailer << /Encrypt\n12 0 R >>")]
        public async Task ProcessNextAsync_PdfWithEncryptDictionaryOrReference_ShouldFailAsPasswordProtected(string pdf)
        {
            SetupReservedDocument(Document(content: Encoding.ASCII.GetBytes(pdf)));

            await _processor.ProcessNextAsync(CancellationToken.None);

            _repository.Verify(r => r.CompleteDocumentAsync(1, InboundDocumentStatus.Failed, "O PDF está protegido por senha.", null), Times.Once);
        }

        [Fact]
        public async Task ProcessNextAsync_PdfOnlyMentioningEncryptInText_ShouldCallExtractor()
        {
            // A palavra solta no conteúdo não é a entrada /Encrypt do trailer
            SetupReservedDocument(Document(content: Encoding.ASCII.GetBytes("%PDF-1.7 (use /Encrypt para proteger)")));
            SetupExtractor(new ChargeExtractionResult { IsCharge = false });

            await _processor.ProcessNextAsync(CancellationToken.None);

            _extractor.Verify(e => e.ExtractAsync(It.IsAny<ChargeExtractionInput>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ProcessNextAsync_ShouldOnlyPickRetriesAfterTheRetryDelay()
        {
            _repository.Setup(r => r.GetPendingCandidateIdsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<int>())).ReturnsAsync([]);

            await _processor.ProcessNextAsync(CancellationToken.None);

            var expected = _time.GetUtcNow().UtcDateTime.AddSeconds(-_options.Worker.RetryDelaySeconds);
            _repository.Verify(r => r.GetPendingCandidateIdsAsync(It.IsAny<DateTime>(), expected, It.IsAny<int>(), It.IsAny<int>()), Times.Once);
        }

        [Fact]
        public async Task ProcessNextAsync_ImageContainingEncryptText_ShouldStillCallExtractor()
        {
            // O /Encrypt só vale para PDF
            SetupReservedDocument(Document(contentType: "image/png", content: [0x89, 0x50, 0x4E, 0x47, .. "/Encrypt"u8.ToArray()]));
            SetupExtractor(new ChargeExtractionResult { IsCharge = false });

            await _processor.ProcessNextAsync(CancellationToken.None);

            _extractor.Verify(e => e.ExtractAsync(It.IsAny<ChargeExtractionInput>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ProcessNextAsync_ShouldSendContentTypeAndVisibleExpenseCategoriesToExtractor()
        {
            var document = Document();
            SetupReservedDocument(document);
            ChargeExtractionInput? input = null;
            _extractor.Setup(e => e.ExtractAsync(It.IsAny<ChargeExtractionInput>(), It.IsAny<CancellationToken>()))
                .Callback((ChargeExtractionInput i, CancellationToken _) => input = i)
                .ReturnsAsync(new ChargeExtractionResult { IsCharge = false });

            await _processor.ProcessNextAsync(CancellationToken.None);

            input!.ContentType.Should().Be("application/pdf");
            input.Content.Should().BeEquivalentTo(document.TemporaryContent);
            input.ExpenseCategories.Should().BeEquivalentTo(Categories);
        }

        [Fact]
        public async Task ProcessNextAsync_ChargeExtractionException_ShouldFailWithItsMessageWithoutRetry()
        {
            SetupReservedDocument(Document(attempts: 1));
            _extractor.Setup(e => e.ExtractAsync(It.IsAny<ChargeExtractionInput>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ChargeExtractionException("Imagem ilegível."));

            var processed = await _processor.ProcessNextAsync(CancellationToken.None);

            processed.Should().BeTrue();
            _repository.Verify(r => r.CompleteDocumentAsync(1, InboundDocumentStatus.Failed, "Imagem ilegível.", null), Times.Once);
            _repository.Verify(r => r.ReturnDocumentToPendingAsync(It.IsAny<int>()), Times.Never);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        public async Task ProcessNextAsync_GenericErrorBelowMaxAttempts_ShouldReturnToPending(int attempts)
        {
            SetupReservedDocument(Document(attempts: attempts));
            _extractor.Setup(e => e.ExtractAsync(It.IsAny<ChargeExtractionInput>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("boom"));

            var processed = await _processor.ProcessNextAsync(CancellationToken.None);

            processed.Should().BeTrue();
            _repository.Verify(r => r.ReturnDocumentToPendingAsync(1), Times.Once);
            _repository.Verify(r => r.CompleteDocumentAsync(It.IsAny<int>(), It.IsAny<InboundDocumentStatus>(), It.IsAny<string?>(), It.IsAny<Charge?>()), Times.Never);
        }

        [Theory]
        [InlineData(3)]
        [InlineData(4)]
        public async Task ProcessNextAsync_GenericErrorAtMaxAttempts_ShouldFailWithGenericMessage(int attempts)
        {
            SetupReservedDocument(Document(attempts: attempts));
            _extractor.Setup(e => e.ExtractAsync(It.IsAny<ChargeExtractionInput>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("boom"));

            await _processor.ProcessNextAsync(CancellationToken.None);

            _repository.Verify(r => r.CompleteDocumentAsync(1, InboundDocumentStatus.Failed, "Não foi possível processar o documento.", null), Times.Once);
            _repository.Verify(r => r.ReturnDocumentToPendingAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task ProcessNextAsync_RespectsConfiguredMaxAttempts()
        {
            _options.Worker.MaxAttempts = 5;
            SetupReservedDocument(Document(attempts: 3));
            _extractor.Setup(e => e.ExtractAsync(It.IsAny<ChargeExtractionInput>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("boom"));

            await _processor.ProcessNextAsync(CancellationToken.None);

            _repository.Verify(r => r.ReturnDocumentToPendingAsync(1), Times.Once);
        }

        [Fact]
        public async Task ProcessNextAsync_OperationCanceled_ShouldPropagateWithoutChangingTheDocument()
        {
            SetupReservedDocument(Document());
            _extractor.Setup(e => e.ExtractAsync(It.IsAny<ChargeExtractionInput>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new OperationCanceledException());

            Func<Task> act = () => _processor.ProcessNextAsync(new CancellationToken(true));

            await act.Should().ThrowAsync<OperationCanceledException>();
            _repository.Verify(r => r.CompleteDocumentAsync(It.IsAny<int>(), It.IsAny<InboundDocumentStatus>(), It.IsAny<string?>(), It.IsAny<Charge?>()), Times.Never);
            _repository.Verify(r => r.ReturnDocumentToPendingAsync(It.IsAny<int>()), Times.Never);
        }

        // ---------- Resultado ----------

        [Fact]
        public async Task ProcessNextAsync_NotACharge_ShouldCompleteDoneWithoutCharge()
        {
            SetupReservedDocument(Document());
            SetupExtractor(new ChargeExtractionResult { IsCharge = false });

            await _processor.ProcessNextAsync(CancellationToken.None);

            _repository.Verify(r => r.CompleteDocumentAsync(1, InboundDocumentStatus.Done, null, null), Times.Once);
        }

        [Fact]
        public async Task ProcessNextAsync_SuggestedCategoryNotInVisibleList_ShouldBeNull()
        {
            SetupReservedDocument(Document());
            CaptureCompletion();
            SetupExtractor(new ChargeExtractionResult { IsCharge = true, Amount = 10m, SuggestedCategoryId = 99 });

            await _processor.ProcessNextAsync(CancellationToken.None);

            CapturedCharge!.SuggestedCategoryId.Should().BeNull();
        }

        [Fact]
        public async Task ProcessNextAsync_SuggestedCategoryInVisibleList_ShouldBeKept()
        {
            SetupReservedDocument(Document());
            CaptureCompletion();
            SetupExtractor(new ChargeExtractionResult { IsCharge = true, Amount = 10m, SuggestedCategoryId = 3 });

            await _processor.ProcessNextAsync(CancellationToken.None);

            CapturedCharge!.SuggestedCategoryId.Should().Be(3);
        }

        [Fact]
        public async Task ProcessNextAsync_ValidResult_ShouldBuildPendingReviewChargeWithNormalizedFields()
        {
            SetupReservedDocument(Document());
            CaptureCompletion();
            var due = new DateTime(2026, 11, 25, 14, 30, 0);
            SetupExtractor(new ChargeExtractionResult
            {
                IsCharge = true,
                Issuer = "  Energia SA  ",
                Description = new string('d', 600),
                Amount = 150.25m,
                DueDate = due
            });
            _repository.Setup(r => r.FindPossibleDuplicateTransactionIdAsync(UserId, 150.25m, new DateTime(2026, 11, 25))).ReturnsAsync(42);

            await _processor.ProcessNextAsync(CancellationToken.None);

            CapturedCharge.Should().NotBeNull();
            CapturedCharge!.UserId.Should().Be(UserId);
            CapturedCharge.Status.Should().Be(ChargeStatus.PendingReview);
            CapturedCharge.Issuer.Should().Be("Energia SA");
            CapturedCharge.Description!.Length.Should().Be(500);
            CapturedCharge.Amount.Should().Be(150.25m);
            CapturedCharge.DueDate.Should().Be(new DateTime(2026, 11, 25));
            CapturedCharge.PossibleDuplicateTransactionId.Should().Be(42);
            CapturedCharge.CreatedAt.Should().Be(_time.Now.UtcDateTime);
            _repository.Verify(r => r.CompleteDocumentAsync(1, InboundDocumentStatus.Done, null, CapturedCharge), Times.Once);
        }

        [Theory]
        [InlineData(null, false)]
        [InlineData(0.0, false)]
        [InlineData(10.0, true)]
        public async Task ProcessNextAsync_AmountOrDueDateInvalid_ShouldNotSearchDuplicate(double? amount, bool dueDateIsNull)
        {
            SetupReservedDocument(Document());
            CaptureCompletion();
            SetupExtractor(new ChargeExtractionResult
            {
                IsCharge = true,
                Amount = amount is null ? null : (decimal)amount,
                DueDate = dueDateIsNull ? null : new DateTime(2026, 11, 20)
            });

            await _processor.ProcessNextAsync(CancellationToken.None);

            _repository.Verify(r => r.FindPossibleDuplicateTransactionIdAsync(It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<DateTime>()), Times.Never);
            CapturedCharge.Should().NotBeNull();
        }

        [Fact]
        public async Task ProcessNextAsync_BarcodeAlreadyInUserCharges_ShouldCompleteDoneWithoutNewCharge()
        {
            SetupReservedDocument(Document());
            var barcode = ValidBankLine();
            SetupExtractor(new ChargeExtractionResult { IsCharge = true, Amount = 10m, Barcode = barcode });
            _repository.Setup(r => r.UserHasChargeWithBarcodeAsync(UserId, barcode)).ReturnsAsync(true);

            await _processor.ProcessNextAsync(CancellationToken.None);

            _repository.Verify(r => r.CompleteDocumentAsync(1, InboundDocumentStatus.Done, null, null), Times.Once);
        }

        [Fact]
        public async Task ProcessNextAsync_InvalidBarcode_ShouldKeepChargeWithNullBarcodeWithoutLookup()
        {
            SetupReservedDocument(Document());
            CaptureCompletion();
            SetupExtractor(new ChargeExtractionResult { IsCharge = true, Amount = 10m, Barcode = "123" });

            await _processor.ProcessNextAsync(CancellationToken.None);

            CapturedCharge!.Barcode.Should().BeNull();
            _repository.Verify(r => r.UserHasChargeWithBarcodeAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        // ---------- Documentos presos ----------

        [Fact]
        public async Task RequeueStuckAsync_ShouldPassCutoffFromClockAndMaxAttempts()
        {
            _options.Worker.StuckAfterMinutes = 10;
            _options.Worker.MaxAttempts = 3;

            await _processor.RequeueStuckAsync();

            _repository.Verify(r => r.RequeueStuckDocumentsAsync(_time.Now.UtcDateTime.AddMinutes(-10), 3), Times.Once);
        }

        // Linha digitável bancária de 47 dígitos válida (DVs calculados aqui)
        private static string ValidBankLine()
        {
            const string free25 = "1090000000123456789012345";
            const string factorAndValue = "9876" + "0000012345";
            var barcode43 = "3419" + factorAndValue + free25;
            var sum = 0;
            var weight = 2;
            for (var i = barcode43.Length - 1; i >= 0; i--)
            {
                sum += (barcode43[i] - '0') * weight;
                weight = weight == 9 ? 2 : weight + 1;
            }
            var general = 11 - sum % 11;
            if (general is 0 or 10 or 11) general = 1;
            var f1 = "3419" + free25[..5];
            var f2 = free25.Substring(5, 10);
            var f3 = free25.Substring(15, 10);
            return f1 + Mod10(f1) + f2 + Mod10(f2) + f3 + Mod10(f3) + general + factorAndValue;
        }

        private static int Mod10(string digits)
        {
            var sum = 0;
            var weight = 2;
            for (var i = digits.Length - 1; i >= 0; i--)
            {
                var p = (digits[i] - '0') * weight;
                sum += p > 9 ? p - 9 : p;
                weight = weight == 2 ? 1 : 2;
            }
            return (10 - sum % 10) % 10;
        }
    }
}
