using System.Text;
using FamilyVaultApi.Data.Entities;
using FamilyVaultApi.Exceptions;
using FamilyVaultApi.Models.Internal;
using FamilyVaultApi.Models.Internal.Enums;
using FamilyVaultApi.Repositories.IRepository;
using FamilyVaultApi.Services.IService;
using FamilyVaultApi.Services.Service;
using FamilyVaultApi.UnitTests.Helpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace FamilyVaultApi.UnitTests.Services
{
    // Etapa 2 para documento do Gmail: o PDF vem do Gmail (cliente simulado) pelo id da mensagem e o partId
    public class ChargeDocumentProcessorGmailTests
    {
        private const string UserId = "user-1";

        private readonly Mock<IChargeRepository> _repository = new();
        private readonly Mock<IChargeExtractor> _extractor = new();
        private readonly Mock<IGmailRepository> _gmailRepository = new();
        private readonly Mock<IGmailApiClient> _client = new();
        private readonly ChargeProcessingOptions _options = new();
        private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 10, 15, 0, 0, TimeSpan.Zero));
        private readonly ChargeDocumentProcessor _processor;
        private readonly GmailTokenProtector _protector;

        public ChargeDocumentProcessorGmailTests()
        {
            var gmailOptions = Options.Create(new GmailOptions { TokenEncryptionKey = Convert.ToBase64String(new byte[32]) });
            var protector = new GmailTokenProtector(gmailOptions);
            _protector = protector;
            _gmailRepository.Setup(r => r.GetConnectionAsync(UserId)).ReturnsAsync(new GmailConnection
            {
                UserId = UserId,
                Status = GmailConnectionStatus.Active,
                RefreshTokenEncrypted = protector.Protect("rt")
            });
            _client.Setup(c => c.GetAccessTokenAsync("rt", It.IsAny<CancellationToken>())).ReturnsAsync("at");

            _processor = new ChargeDocumentProcessor(_repository.Object, _extractor.Object, Options.Create(_options), _time,
                NullLogger<ChargeDocumentProcessor>.Instance,
                new GmailDocumentFetcher(_gmailRepository.Object, _client.Object, protector), protector);

            _repository.Setup(r => r.GetPendingCandidateIdsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<int>())).ReturnsAsync([1]);
            _repository.Setup(r => r.TryReserveDocumentAsync(1, It.IsAny<DateTime>())).ReturnsAsync(true);
            _repository.Setup(r => r.GetExpenseCategoriesAsync(UserId)).ReturnsAsync(new List<ChargeCategoryOption>());
        }

        private void SetupDocument(int attempts = 1, string? sender = null, byte[]? temporaryContent = null) =>
            _repository.Setup(r => r.GetDocumentAsync(1)).ReturnsAsync(new InboundDocument
            {
                InboundDocumentId = 1,
                UserId = UserId,
                Source = InboundDocumentSource.Gmail,
                ExternalId = "gmail:msg-1:2",
                Sender = sender,
                TemporaryContent = temporaryContent,
                ContentType = "application/pdf",
                Attempts = attempts,
                ReceivedAt = _time.Now.UtcDateTime,
                Status = InboundDocumentStatus.Processing
            });

        [Fact]
        public async Task ProcessNextAsync_ShouldDownloadThePdfByMessageAndPartAndExtract()
        {
            SetupDocument();
            var pdf = Encoding.ASCII.GetBytes("%PDF-1.4 conta");
            _client.Setup(c => c.GetAttachmentAsync("at", "msg-1", "2", It.IsAny<CancellationToken>())).ReturnsAsync(pdf);
            _extractor.Setup(e => e.ExtractAsync(It.IsAny<ChargeExtractionInput>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ChargeExtractionResult { IsCharge = false });

            var processed = await _processor.ProcessNextAsync(CancellationToken.None);

            processed.Should().BeTrue();
            _extractor.Verify(e => e.ExtractAsync(It.Is<ChargeExtractionInput>(i => i.Content == pdf && i.ContentType == "application/pdf"), It.IsAny<CancellationToken>()), Times.Once);
            _repository.Verify(r => r.CompleteDocumentAsync(1, InboundDocumentStatus.Done, null, null), Times.Once);
        }

        [Fact]
        public async Task ProcessNextAsync_InvalidGrant_ShouldRevokeAndReleaseWithoutSpendingTheAttempt()
        {
            SetupDocument();
            _client.Setup(c => c.GetAccessTokenAsync("rt", It.IsAny<CancellationToken>())).ThrowsAsync(new GmailAccessRevokedException());

            await _processor.ProcessNextAsync(CancellationToken.None);

            _gmailRepository.Verify(r => r.MarkRevokedAsync(UserId), Times.Once);
            _repository.Verify(r => r.ReleaseDocumentAsync(1), Times.Once);
            _extractor.Verify(e => e.ExtractAsync(It.IsAny<ChargeExtractionInput>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ProcessNextAsync_ConnectionRemoved_ShouldReleaseTheDocument()
        {
            SetupDocument();
            _gmailRepository.Setup(r => r.GetConnectionAsync(UserId)).ReturnsAsync((GmailConnection?)null);

            await _processor.ProcessNextAsync(CancellationToken.None);

            _repository.Verify(r => r.ReleaseDocumentAsync(1), Times.Once);
        }

        [Fact]
        public async Task ProcessNextAsync_EmailDeleted_ShouldFailWithReason()
        {
            SetupDocument();
            _client.Setup(c => c.GetAttachmentAsync("at", "msg-1", "2", It.IsAny<CancellationToken>())).ThrowsAsync(new GmailMessageNotFoundException());

            await _processor.ProcessNextAsync(CancellationToken.None);

            _repository.Verify(r => r.CompleteDocumentAsync(1, InboundDocumentStatus.Failed, "O e-mail não está mais disponível no Gmail.", null), Times.Once);
        }

        [Fact]
        public async Task ProcessNextAsync_AttachmentIsNotAPdf_ShouldFailAsUnreadable()
        {
            SetupDocument();
            _client.Setup(c => c.GetAttachmentAsync("at", "msg-1", "2", It.IsAny<CancellationToken>())).ReturnsAsync(Encoding.ASCII.GetBytes("<html>"));

            await _processor.ProcessNextAsync(CancellationToken.None);

            _repository.Verify(r => r.CompleteDocumentAsync(1, InboundDocumentStatus.Failed, "O documento está ilegível.", null), Times.Once);
        }

        [Fact]
        public async Task ProcessNextAsync_TransientGmailError_ShouldReturnToPendingUntilTheLastAttempt()
        {
            SetupDocument(attempts: 1);
            _client.Setup(c => c.GetAttachmentAsync("at", "msg-1", "2", It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException("timeout"));

            await _processor.ProcessNextAsync(CancellationToken.None);

            _repository.Verify(r => r.ReturnDocumentToPendingAsync(1), Times.Once);
        }

        [Fact]
        public async Task ProcessNextAsync_TransientGmailErrorOnLastAttempt_ShouldFail()
        {
            SetupDocument(attempts: 3);
            _client.Setup(c => c.GetAttachmentAsync("at", "msg-1", "2", It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException("timeout"));

            await _processor.ProcessNextAsync(CancellationToken.None);

            _repository.Verify(r => r.CompleteDocumentAsync(1, InboundDocumentStatus.Failed, "Não foi possível processar o documento.", null), Times.Once);
        }

        // ---------- PDF com senha ----------

        private const string Sender = "faturas@banco.com.br";

        private void SetupEncryptedAttachment(byte[] pdf) =>
            _client.Setup(c => c.GetAttachmentAsync("at", "msg-1", "2", It.IsAny<CancellationToken>())).ReturnsAsync(pdf);

        [Fact]
        public async Task ProcessNextAsync_EncryptedPdfWithRememberedSenderPassword_ShouldExtractTheUnlockedCopy()
        {
            SetupDocument(sender: Sender);
            SetupEncryptedAttachment(TestPdfs.Create("fatura-gmail", userPassword: "12345"));
            _repository.Setup(r => r.GetSenderPasswordAsync(UserId, Sender)).ReturnsAsync(_protector.Protect("12345"));
            ChargeExtractionInput? received = null;
            _extractor.Setup(e => e.ExtractAsync(It.IsAny<ChargeExtractionInput>(), It.IsAny<CancellationToken>()))
                .Callback((ChargeExtractionInput input, CancellationToken _) => received = input)
                .ReturnsAsync(new ChargeExtractionResult { IsCharge = false });

            await _processor.ProcessNextAsync(CancellationToken.None);

            received.Should().NotBeNull();
            TestPdfs.IsEncrypted(received!.Content).Should().BeFalse();
            TestPdfs.ReadPageContent(received.Content).Should().Contain("fatura-gmail");
            _repository.Verify(r => r.MarkPasswordRequiredAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<bool>()), Times.Never);
        }

        [Fact]
        public async Task ProcessNextAsync_EncryptedPdfWithoutRememberedPassword_ShouldWaitForPasswordWithoutKeepingThePdf()
        {
            SetupDocument(sender: Sender);
            SetupEncryptedAttachment(TestPdfs.Create("fatura-gmail", userPassword: "12345"));

            await _processor.ProcessNextAsync(CancellationToken.None);

            _repository.Verify(r => r.MarkPasswordRequiredAsync(1, "O PDF está protegido por senha.", _time.Now.UtcDateTime, false), Times.Once);
            _extractor.Verify(e => e.ExtractAsync(It.IsAny<ChargeExtractionInput>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ProcessNextAsync_RememberedPasswordThatNoLongerOpens_ShouldWaitForPassword()
        {
            SetupDocument(sender: Sender);
            SetupEncryptedAttachment(TestPdfs.Create("fatura-gmail", userPassword: "nova"));
            _repository.Setup(r => r.GetSenderPasswordAsync(UserId, Sender)).ReturnsAsync(_protector.Protect("antiga"));

            await _processor.ProcessNextAsync(CancellationToken.None);

            _repository.Verify(r => r.MarkPasswordRequiredAsync(1, "O PDF está protegido por senha.", It.IsAny<DateTime>(), false), Times.Once);
        }

        [Fact]
        public async Task ProcessNextAsync_GmailDocumentAlreadyUnlockedByPassword_ShouldUseItWithoutDownloading()
        {
            var unlocked = TestPdfs.Create("ja-aberto");
            SetupDocument(sender: Sender, temporaryContent: unlocked);
            _extractor.Setup(e => e.ExtractAsync(It.IsAny<ChargeExtractionInput>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ChargeExtractionResult { IsCharge = false });

            await _processor.ProcessNextAsync(CancellationToken.None);

            _extractor.Verify(e => e.ExtractAsync(It.Is<ChargeExtractionInput>(i => i.Content == unlocked), It.IsAny<CancellationToken>()), Times.Once);
            _client.Verify(c => c.GetAttachmentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            _repository.Verify(r => r.CompleteDocumentAsync(1, InboundDocumentStatus.Done, null, null), Times.Once);
        }

        // ---------- Corpo do e-mail (PDF protegido sem senha que abra) ----------

        private const string WaitingReason = "O PDF está protegido por senha.";

        private void SetupBody(string? body) =>
            _client.Setup(c => c.GetMessageBodyAsync("at", "msg-1", It.IsAny<CancellationToken>())).ReturnsAsync(body);

        private void SetupBodyExtraction(ChargeExtractionResult result) =>
            _extractor.Setup(e => e.ExtractAsync(It.Is<ChargeExtractionInput>(i => i.ContentType == "text/plain"), It.IsAny<CancellationToken>()))
                .ReturnsAsync(result);

        private ChargeExtractionResult BodyCharge() => new()
        {
            IsCharge = true,
            Issuer = "Nio",
            Description = "Fatura Nio",
            Amount = 99.90m,
            DueDate = _time.Now.UtcDateTime.Date.AddDays(10)
        };

        private void VerifyWaitingForPassword() =>
            _repository.Verify(r => r.MarkPasswordRequiredAsync(1, WaitingReason, It.IsAny<DateTime>(), false), Times.Once);

        [Fact]
        public async Task ProcessNextAsync_EncryptedPdfWithChargeInEmailBody_ShouldCreateChargeMarkedAsEmailBody()
        {
            SetupDocument(sender: Sender);
            SetupEncryptedAttachment(TestPdfs.Create("fatura-gmail", userPassword: "12345"));
            SetupBody("Sua fatura Nio: R$ 99,90, vence em 20/11.");
            ChargeExtractionInput? received = null;
            _extractor.Setup(e => e.ExtractAsync(It.IsAny<ChargeExtractionInput>(), It.IsAny<CancellationToken>()))
                .Callback((ChargeExtractionInput input, CancellationToken _) => received = input)
                .ReturnsAsync(BodyCharge());

            await _processor.ProcessNextAsync(CancellationToken.None);

            received!.ContentType.Should().Be("text/plain");
            Encoding.UTF8.GetString(received.Content).Should().Be("Sua fatura Nio: R$ 99,90, vence em 20/11.");
            _repository.Verify(r => r.CompleteDocumentAsync(1, InboundDocumentStatus.Done, null, It.Is<Charge>(c =>
                c.ExtractionSource == ChargeExtractionSource.EmailBody && c.Amount == 99.90m && c.Status == ChargeStatus.PendingReview)), Times.Once);
            _repository.Verify(r => r.MarkPasswordRequiredAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<bool>()), Times.Never);
        }

        [Fact]
        public async Task ProcessNextAsync_RememberedPasswordOpens_ShouldNotReadTheEmailBody()
        {
            SetupDocument(sender: Sender);
            SetupEncryptedAttachment(TestPdfs.Create("fatura-gmail", userPassword: "12345"));
            _repository.Setup(r => r.GetSenderPasswordAsync(UserId, Sender)).ReturnsAsync(_protector.Protect("12345"));
            _extractor.Setup(e => e.ExtractAsync(It.IsAny<ChargeExtractionInput>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(BodyCharge());

            await _processor.ProcessNextAsync(CancellationToken.None);

            _client.Verify(c => c.GetMessageBodyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            _repository.Verify(r => r.CompleteDocumentAsync(1, InboundDocumentStatus.Done, null, It.Is<Charge>(c => c.ExtractionSource == ChargeExtractionSource.Document)), Times.Once);
        }

        [Fact]
        public async Task ProcessNextAsync_EmailBodyWithoutDueDate_ShouldWaitForPassword()
        {
            SetupDocument(sender: Sender);
            SetupEncryptedAttachment(TestPdfs.Create("fatura-gmail", userPassword: "12345"));
            SetupBody("Sua fatura de R$ 99,90 está no anexo.");
            SetupBodyExtraction(new ChargeExtractionResult { IsCharge = true, Amount = 99.90m });

            await _processor.ProcessNextAsync(CancellationToken.None);

            VerifyWaitingForPassword();
            _repository.Verify(r => r.CompleteDocumentAsync(It.IsAny<int>(), It.IsAny<InboundDocumentStatus>(), It.IsAny<string?>(), It.IsAny<Charge?>()), Times.Never);
        }

        [Fact]
        public async Task ProcessNextAsync_EmailBodyWithInvalidAmount_ShouldWaitForPassword()
        {
            SetupDocument(sender: Sender);
            SetupEncryptedAttachment(TestPdfs.Create("fatura-gmail", userPassword: "12345"));
            SetupBody("Fatura");
            var charge = BodyCharge();
            SetupBodyExtraction(new ChargeExtractionResult { IsCharge = true, Amount = -5m, DueDate = charge.DueDate });

            await _processor.ProcessNextAsync(CancellationToken.None);

            VerifyWaitingForPassword();
        }

        [Fact]
        public async Task ProcessNextAsync_EmailBodyIsNotACharge_ShouldWaitForPassword()
        {
            SetupDocument(sender: Sender);
            SetupEncryptedAttachment(TestPdfs.Create("fatura-gmail", userPassword: "12345"));
            SetupBody("Sua fatura está disponível no anexo.");
            SetupBodyExtraction(new ChargeExtractionResult { IsCharge = false });

            await _processor.ProcessNextAsync(CancellationToken.None);

            VerifyWaitingForPassword();
        }

        [Fact]
        public async Task ProcessNextAsync_EmptyEmailBody_ShouldWaitForPasswordWithoutCallingTheAi()
        {
            SetupDocument(sender: Sender);
            SetupEncryptedAttachment(TestPdfs.Create("fatura-gmail", userPassword: "12345"));
            SetupBody("   ");

            await _processor.ProcessNextAsync(CancellationToken.None);

            VerifyWaitingForPassword();
            _extractor.Verify(e => e.ExtractAsync(It.IsAny<ChargeExtractionInput>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ProcessNextAsync_EmailBodyCannotBeRead_ShouldWaitForPassword()
        {
            SetupDocument(sender: Sender);
            SetupEncryptedAttachment(TestPdfs.Create("fatura-gmail", userPassword: "12345"));
            _client.Setup(c => c.GetMessageBodyAsync("at", "msg-1", It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException("falha"));

            await _processor.ProcessNextAsync(CancellationToken.None);

            VerifyWaitingForPassword();
            _extractor.Verify(e => e.ExtractAsync(It.IsAny<ChargeExtractionInput>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ProcessNextAsync_EmailBodyUnreadableForTheAi_ShouldWaitForPasswordInsteadOfFailing()
        {
            SetupDocument(sender: Sender);
            SetupEncryptedAttachment(TestPdfs.Create("fatura-gmail", userPassword: "12345"));
            SetupBody("???");
            _extractor.Setup(e => e.ExtractAsync(It.IsAny<ChargeExtractionInput>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ChargeExtractionException("O documento está ilegível."));

            await _processor.ProcessNextAsync(CancellationToken.None);

            VerifyWaitingForPassword();
            _repository.Verify(r => r.CompleteDocumentAsync(1, InboundDocumentStatus.Failed, It.IsAny<string?>(), null), Times.Never);
        }

        [Fact]
        public async Task ProcessNextAsync_TransientAiErrorOnEmailBody_ShouldReturnToPendingUntilTheLastAttempt()
        {
            SetupDocument(attempts: 1, sender: Sender);
            SetupEncryptedAttachment(TestPdfs.Create("fatura-gmail", userPassword: "12345"));
            SetupBody("Fatura R$ 99,90");
            _extractor.Setup(e => e.ExtractAsync(It.IsAny<ChargeExtractionInput>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new TimeoutException());

            await _processor.ProcessNextAsync(CancellationToken.None);

            _repository.Verify(r => r.ReturnDocumentToPendingAsync(1), Times.Once);
            _repository.Verify(r => r.MarkPasswordRequiredAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<bool>()), Times.Never);
        }

        [Fact]
        public async Task ProcessNextAsync_TransientAiErrorOnEmailBodyOnLastAttempt_ShouldWaitForPassword()
        {
            SetupDocument(attempts: _options.Worker.MaxAttempts, sender: Sender);
            SetupEncryptedAttachment(TestPdfs.Create("fatura-gmail", userPassword: "12345"));
            SetupBody("Fatura R$ 99,90");
            _extractor.Setup(e => e.ExtractAsync(It.IsAny<ChargeExtractionInput>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new TimeoutException());

            await _processor.ProcessNextAsync(CancellationToken.None);

            VerifyWaitingForPassword();
            _repository.Verify(r => r.ReturnDocumentToPendingAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task ProcessNextAsync_AnotherAttachmentOfTheSameEmailAlreadyReadFromTheBody_ShouldWaitForPassword()
        {
            SetupDocument(sender: Sender);
            SetupEncryptedAttachment(TestPdfs.Create("fatura-gmail", userPassword: "12345"));
            SetupBody("Fatura R$ 99,90");
            _repository.Setup(r => r.MessageHasChargeAsync(UserId, "gmail:msg-1:")).ReturnsAsync(true);

            await _processor.ProcessNextAsync(CancellationToken.None);

            VerifyWaitingForPassword();
            _client.Verify(c => c.GetMessageBodyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ProcessNextAsync_GmailRevokedWhileReadingTheBody_ShouldReleaseTheDocument()
        {
            SetupDocument(sender: Sender);
            SetupEncryptedAttachment(TestPdfs.Create("fatura-gmail", userPassword: "12345"));
            _client.Setup(c => c.GetMessageBodyAsync("at", "msg-1", It.IsAny<CancellationToken>())).ThrowsAsync(new GmailAccessRevokedException());

            await _processor.ProcessNextAsync(CancellationToken.None);

            _repository.Verify(r => r.ReleaseDocumentAsync(1), Times.Once);
            _repository.Verify(r => r.MarkPasswordRequiredAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<bool>()), Times.Never);
        }

        [Fact]
        public async Task ProcessNextAsync_LongEmailBody_ShouldBeCutBeforeTheAi()
        {
            SetupDocument(sender: Sender);
            SetupEncryptedAttachment(TestPdfs.Create("fatura-gmail", userPassword: "12345"));
            SetupBody(new string('a', ChargeDocumentProcessor.EmailBodyMaxChars + 500));
            ChargeExtractionInput? received = null;
            _extractor.Setup(e => e.ExtractAsync(It.IsAny<ChargeExtractionInput>(), It.IsAny<CancellationToken>()))
                .Callback((ChargeExtractionInput input, CancellationToken _) => received = input)
                .ReturnsAsync(new ChargeExtractionResult { IsCharge = false });

            await _processor.ProcessNextAsync(CancellationToken.None);

            received!.Content.Length.Should().Be(ChargeDocumentProcessor.EmailBodyMaxChars);
        }

        [Fact]
        public async Task ProcessNextAsync_EmailBodyBarcodeAlreadyKnown_ShouldFinishWithoutNewCharge()
        {
            SetupDocument(sender: Sender);
            SetupEncryptedAttachment(TestPdfs.Create("fatura-gmail", userPassword: "12345"));
            SetupBody("Fatura com linha");
            var barcode = "99991309429095073376727370404561116410000011990";
            var charge = BodyCharge();
            SetupBodyExtraction(new ChargeExtractionResult { IsCharge = true, Amount = charge.Amount, DueDate = charge.DueDate, Barcode = barcode });
            _repository.Setup(r => r.UserHasChargeWithBarcodeAsync(UserId, barcode)).ReturnsAsync(true);

            await _processor.ProcessNextAsync(CancellationToken.None);

            _repository.Verify(r => r.CompleteDocumentAsync(1, InboundDocumentStatus.Done, null, null), Times.Once);
        }
    }
}
