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
    public class GmailScanProcessorTests
    {
        private const string UserId = "user-1";

        private readonly Mock<IGmailRepository> _repository = new();
        private readonly Mock<IGmailApiClient> _client = new();
        private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 10, 15, 0, 0, TimeSpan.Zero));
        private readonly GmailScanProcessor _processor;
        private readonly GmailScan _scan;

        public GmailScanProcessorTests()
        {
            var gmailOptions = Options.Create(new GmailOptions { TokenEncryptionKey = Convert.ToBase64String(new byte[32]) });
            var protector = new GmailTokenProtector(gmailOptions);
            _processor = new GmailScanProcessor(_repository.Object, _client.Object, protector, gmailOptions,
                Options.Create(new ChargeProcessingOptions()), _time, NullLogger<GmailScanProcessor>.Instance);

            _scan = new GmailScan
            {
                GmailScanId = 5,
                UserId = UserId,
                Kind = GmailScanKind.Manual,
                WindowStart = _time.Now.UtcDateTime.AddHours(-1),
                WindowEnd = _time.Now.UtcDateTime,
                Status = GmailScanStatus.Running
            };
            _repository.Setup(r => r.GetNextRunningScanAsync()).ReturnsAsync(_scan);
            _repository.Setup(r => r.GetConnectionAsync(UserId)).ReturnsAsync(new GmailConnection
            {
                UserId = UserId,
                Status = GmailConnectionStatus.Active,
                RefreshTokenEncrypted = protector.Protect("rt")
            });
            _repository.Setup(r => r.ListIgnoredSendersAsync(UserId)).ReturnsAsync(new List<GmailIgnoredSender>());
            _client.Setup(c => c.GetAccessTokenAsync("rt", It.IsAny<CancellationToken>())).ReturnsAsync("at");
            _client.Setup(c => c.ListMessageIdsAsync("at", It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<string> { "m1", "m2" });
            _client.Setup(c => c.GetMessageAsync("at", It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string _, string id, CancellationToken _) => new GmailMessageInfo
                {
                    MessageId = id,
                    Sender = "a@b.test",
                    ReceivedAt = _time.Now.UtcDateTime,
                    PdfAttachments = new List<GmailPdfAttachment> { new() { PartId = "1", Size = 100 } }
                });
            _repository.Setup(r => r.AddDocumentsAsync(It.IsAny<List<InboundDocument>>())).ReturnsAsync((List<InboundDocument> d) => d.Count);
        }

        [Fact]
        public async Task ProcessNextScanAsync_ScanStoppedRunningMidway_ShouldStopCreatingDocuments()
        {
            _repository.SetupSequence(r => r.IsScanRunningAsync(5)).ReturnsAsync(true).ReturnsAsync(false);

            await _processor.ProcessNextScanAsync(CancellationToken.None);

            _repository.Verify(r => r.AddDocumentsAsync(It.IsAny<List<InboundDocument>>()), Times.Once);
            _client.Verify(c => c.GetMessageAsync("at", "m2", It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ProcessNextScanAsync_ShouldUpdateTheCountsAfterEachMessageAndComplete()
        {
            _repository.Setup(r => r.IsScanRunningAsync(5)).ReturnsAsync(true);

            await _processor.ProcessNextScanAsync(CancellationToken.None);

            _repository.Verify(r => r.UpdateScanCountsAsync(5), Times.Exactly(2));
            _repository.Verify(r => r.CompleteScanAsync(_scan, _time.Now.UtcDateTime), Times.Once);
        }
    }
}
