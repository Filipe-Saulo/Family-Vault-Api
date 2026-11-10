using AutoMapper;
using FamilyVaultApi.Data.Entities;
using FamilyVaultApi.Exceptions;
using FamilyVaultApi.Mapping;
using FamilyVaultApi.Models.Internal;
using FamilyVaultApi.Models.Internal.Enums;
using FamilyVaultApi.Repositories.IRepository;
using FamilyVaultApi.Services.IService;
using FamilyVaultApi.Services.Service;
using FamilyVaultApi.UnitTests.Helpers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace FamilyVaultApi.UnitTests.Services
{
    public class GmailConnectionServiceTests
    {
        private const string UserId = "user-1";

        private readonly Mock<IGmailRepository> _repository = new();
        private readonly Mock<IGmailApiClient> _client = new();
        private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 10, 15, 0, 0, TimeSpan.Zero));
        private readonly GmailOptions _options = new()
        {
            ClientId = "id",
            ClientSecret = "secret",
            TokenEncryptionKey = Convert.ToBase64String(new byte[32]),
            RedirectUri = "https://api.test/api/gmail/callback",
            AllowedReturnUrls = new List<string> { "familyvault://gmail" }
        };

        private DateTime Now => _time.Now.UtcDateTime;

        private GmailConnectionService Create(bool configured = true)
        {
            var services = new ServiceCollection();
            if (configured)
            {
                services.AddSingleton(_client.Object);
                services.AddSingleton(new GmailTokenProtector(Options.Create(_options)));
            }

            var mapper = new MapperConfiguration(c => c.AddProfile<AutoMapperProfile>()).CreateMapper();
            return new GmailConnectionService(_repository.Object, services.BuildServiceProvider(), Options.Create(_options), _time, mapper,
                NullLogger<GmailConnectionService>.Instance);
        }

        private void SetupConnection(DateTime? initialCompletedAt, DateTime? syncedUntil, GmailConnectionStatus status = GmailConnectionStatus.Active)
        {
            _repository.Setup(r => r.GetConnectionAsync(UserId)).ReturnsAsync(new GmailConnection
            {
                UserId = UserId,
                Status = status,
                InitialScanCompletedAt = initialCompletedAt,
                SyncedUntil = syncedUntil
            });
            _repository.Setup(r => r.TryMarkScanRequestedAsync(UserId, It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync(true);
            _repository.Setup(r => r.AddScanAsync(It.IsAny<GmailScan>())).ReturnsAsync((GmailScan s) => { s.GmailScanId = 7; return s; });
        }

        private GmailScan? CapturedScan() =>
            _repository.Invocations.Where(i => i.Method.Name == nameof(IGmailRepository.AddScanAsync)).Select(i => (GmailScan)i.Arguments[0]).SingleOrDefault();

        [Fact]
        public async Task StartScanAsync_AfterInitial_ShouldStartAnHourBeforeTheSavedPoint()
        {
            SetupConnection(Now.AddDays(-1), Now.AddMinutes(-10));

            var result = await Create().StartScanAsync(ClaimsPrincipalTestHelper.CreateUser(UserId));

            result.ScanId.Should().Be(7);
            var scan = CapturedScan()!;
            scan.Kind.Should().Be(GmailScanKind.Manual);
            scan.Status.Should().Be(GmailScanStatus.Running);
            scan.WindowStart.Should().Be(Now.AddMinutes(-70));
            scan.WindowEnd.Should().Be(Now);
        }

        [Fact]
        public async Task StartScanAsync_SavedPointOlderThan30Days_ShouldSearchOnlyTheLast30Days()
        {
            SetupConnection(Now.AddDays(-60), Now.AddDays(-45));

            await Create().StartScanAsync(ClaimsPrincipalTestHelper.CreateUser(UserId));

            CapturedScan()!.WindowStart.Should().Be(Now.AddDays(-30));
        }

        [Fact]
        public async Task StartScanAsync_WithoutInitialCompleted_ShouldRedoTheInitial()
        {
            SetupConnection(null, null);

            await Create().StartScanAsync(ClaimsPrincipalTestHelper.CreateUser(UserId));

            var scan = CapturedScan()!;
            scan.Kind.Should().Be(GmailScanKind.Initial);
            scan.WindowStart.Should().Be(Now.AddDays(-90));
        }

        [Fact]
        public async Task StartScanAsync_RequestedLessThanAMinuteAgo_ShouldThrowTooManyRequests()
        {
            SetupConnection(Now.AddDays(-1), Now.AddMinutes(-10));
            _repository.Setup(r => r.TryMarkScanRequestedAsync(UserId, Now.AddSeconds(-60), Now)).ReturnsAsync(false);

            Func<Task> act = () => Create().StartScanAsync(ClaimsPrincipalTestHelper.CreateUser(UserId));

            await act.Should().ThrowAsync<TooManyRequestsException>();
            _repository.Verify(r => r.AddScanAsync(It.IsAny<GmailScan>()), Times.Never);
        }

        [Fact]
        public async Task StartScanAsync_WithScanRunning_ShouldThrowConflictBeforeTakingTheMinute()
        {
            SetupConnection(Now.AddDays(-1), Now.AddMinutes(-10));
            _repository.Setup(r => r.HasRunningScanAsync(UserId)).ReturnsAsync(true);

            Func<Task> act = () => Create().StartScanAsync(ClaimsPrincipalTestHelper.CreateUser(UserId));

            await act.Should().ThrowAsync<ConflictException>();
            _repository.Verify(r => r.TryMarkScanRequestedAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()), Times.Never);
        }

        [Fact]
        public async Task StartScanAsync_RevokedConnection_ShouldThrowBadRequest()
        {
            SetupConnection(Now.AddDays(-1), Now.AddMinutes(-10), GmailConnectionStatus.Revoked);

            Func<Task> act = () => Create().StartScanAsync(ClaimsPrincipalTestHelper.CreateUser(UserId));

            await act.Should().ThrowAsync<BadRequestException>().WithMessage("*Conecte o Gmail de novo*");
        }

        [Fact]
        public async Task ConnectAsync_GmailNotConfigured_ShouldThrowBadRequest()
        {
            Func<Task> act = () => Create(configured: false).ConnectAsync(null, ClaimsPrincipalTestHelper.CreateUser(UserId));

            await act.Should().ThrowAsync<BadRequestException>().WithMessage("*não está configurada*");
        }

        [Fact]
        public async Task ConnectAsync_ShouldStoreOnlyTheHashOfTheStateWithTenMinutes()
        {
            string? sentState = null;
            _client.Setup(c => c.BuildAuthorizationUrl(It.IsAny<string>())).Callback((string s) => sentState = s).Returns("https://google/auth");
            GmailOAuthState? stored = null;
            _repository.Setup(r => r.AddStateAsync(It.IsAny<GmailOAuthState>(), Now)).Callback((GmailOAuthState s, DateTime _) => stored = s).Returns(Task.CompletedTask);

            var result = await Create().ConnectAsync(null, ClaimsPrincipalTestHelper.CreateUser(UserId));

            result.AuthorizationUrl.Should().Be("https://google/auth");
            stored!.UserId.Should().Be(UserId);
            stored.ReturnUrl.Should().Be("familyvault://gmail");
            stored.ExpiresAt.Should().Be(Now.AddMinutes(10));
            stored.StateHash.Should().NotBe(sentState).And.HaveLength(64);
        }

        [Fact]
        public async Task HandleCallbackAsync_ExchangeFails_ShouldRedirectWithFailedWithoutConnecting()
        {
            _repository.Setup(r => r.ConsumeStateAsync(It.IsAny<string>(), Now))
                .ReturnsAsync(new GmailOAuthState { UserId = UserId, ReturnUrl = "familyvault://gmail" });
            _client.Setup(c => c.ExchangeCodeAsync("code", It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException("boom"));

            var url = await Create().HandleCallbackAsync("code", "state", null, CancellationToken.None);

            url.Should().Be("familyvault://gmail?gmail=error&reason=failed");
            _repository.Verify(r => r.ConnectAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<GmailScan>(), It.IsAny<DateTime>()), Times.Never);
        }

        [Fact]
        public async Task DisconnectAsync_RevokeFails_ShouldStillDeleteTheConnection()
        {
            var protector = new GmailTokenProtector(Options.Create(_options));
            _repository.Setup(r => r.GetConnectionAsync(UserId))
                .ReturnsAsync(new GmailConnection { UserId = UserId, RefreshTokenEncrypted = protector.Protect("rt") });
            _client.Setup(c => c.RevokeAsync("rt", It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException("offline"));

            await Create().DisconnectAsync(ClaimsPrincipalTestHelper.CreateUser(UserId), CancellationToken.None);

            _client.Verify(c => c.RevokeAsync("rt", It.IsAny<CancellationToken>()), Times.Once);
            _repository.Verify(r => r.DeleteConnectionAsync(UserId), Times.Once);
        }

        [Fact]
        public void BuildQuery_ShouldFilterPdfChargesInTheWindowAndExcludeIgnoredSenders()
        {
            var start = new DateTime(2026, 11, 10, 14, 0, 0, DateTimeKind.Utc);
            var end = new DateTime(2026, 11, 10, 15, 0, 0, DateTimeKind.Utc);

            var query = GmailScanProcessor.BuildQuery(start, end, new[] { "Spam@Loja.test", "\"estranho\"@x.test" });

            query.Should().Be("has:attachment filename:pdf (boleto OR fatura OR vencimento OR \"segunda via\") "
                + "after:1794319200 before:1794322801 -from:spam@loja.test");
        }
    }
}
