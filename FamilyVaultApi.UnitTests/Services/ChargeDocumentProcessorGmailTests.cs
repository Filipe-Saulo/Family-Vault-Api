using System.Text;
using FamilyVaultApi.Data.Entities;
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

        public ChargeDocumentProcessorGmailTests()
        {
            var gmailOptions = Options.Create(new GmailOptions { TokenEncryptionKey = Convert.ToBase64String(new byte[32]) });
            var protector = new GmailTokenProtector(gmailOptions);
            _gmailRepository.Setup(r => r.GetConnectionAsync(UserId)).ReturnsAsync(new GmailConnection
            {
                UserId = UserId,
                Status = GmailConnectionStatus.Active,
                RefreshTokenEncrypted = protector.Protect("rt")
            });
            _client.Setup(c => c.GetAccessTokenAsync("rt", It.IsAny<CancellationToken>())).ReturnsAsync("at");

            _processor = new ChargeDocumentProcessor(_repository.Object, _extractor.Object, Options.Create(_options), _time,
                NullLogger<ChargeDocumentProcessor>.Instance,
                new GmailDocumentFetcher(_gmailRepository.Object, _client.Object, protector));

            _repository.Setup(r => r.GetPendingCandidateIdsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<int>())).ReturnsAsync([1]);
            _repository.Setup(r => r.TryReserveDocumentAsync(1, It.IsAny<DateTime>())).ReturnsAsync(true);
            _repository.Setup(r => r.GetExpenseCategoriesAsync(UserId)).ReturnsAsync(new List<ChargeCategoryOption>());
        }

        private void SetupDocument(int attempts = 1) =>
            _repository.Setup(r => r.GetDocumentAsync(1)).ReturnsAsync(new InboundDocument
            {
                InboundDocumentId = 1,
                UserId = UserId,
                Source = InboundDocumentSource.Gmail,
                ExternalId = "gmail:msg-1:2",
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
    }
}
