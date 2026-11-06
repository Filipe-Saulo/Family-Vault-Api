using FamilyVaultApi.Data.Entities;
using FamilyVaultApi.Models.Internal.Enums;
using FamilyVaultApi.Repositories.IRepository;
using FamilyVaultApi.Services.Service;
using FluentAssertions;
using Moq;
using System.Security.Cryptography;
using System.Text;

namespace FamilyVaultApi.UnitTests.Services
{
    public class RefreshTokenServiceTests
    {
        private const string UserId = "user-1";
        private const string Family = "family-1";
        private const string Token = "token-recebido";

        private readonly Mock<IRefreshTokenRepository> _repositoryMock = new();
        private readonly List<RefreshToken> _added = new();

        public RefreshTokenServiceTests()
        {
            _repositoryMock
                .Setup(x => x.AddAsync(It.IsAny<RefreshToken>()))
                .Callback<RefreshToken>(_added.Add)
                .Returns(Task.CompletedTask);
        }

        private RefreshTokenService CreateService() => new(_repositoryMock.Object);

        private static string HashOf(string token) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

        private static RefreshToken Stored(DateTime? consumedAt = null, DateTime? revokedAt = null, DateTime? expiresAt = null,
            string userId = UserId, AuthClient client = AuthClient.App) => new()
            {
                RefreshTokenId = 10,
                TokenHash = HashOf(Token),
                FamilyId = Family,
                UserId = userId,
                Client = client,
                ConsumedAt = consumedAt,
                RevokedAt = revokedAt,
                ExpiresAt = expiresAt ?? DateTime.UtcNow.AddDays(1),
            };

        private void SetUpStored(RefreshToken? stored) =>
            _repositoryMock.Setup(x => x.FindByHashAsync(HashOf(Token))).ReturnsAsync(stored);

        // ── CreateSessionAsync ──────────────────────────────

        [Fact]
        public async Task CreateSessionAsync_ShouldStoreOnlyTheHashWithSevenDaysAndANewFamily()
        {
            var token = await CreateService().CreateSessionAsync(UserId, AuthClient.Web);

            var row = _added.Should().ContainSingle().Subject;
            token.Should().NotBeNullOrWhiteSpace();
            row.TokenHash.Should().Be(HashOf(token)).And.NotBe(token);
            row.UserId.Should().Be(UserId);
            row.Client.Should().Be(AuthClient.Web);
            Guid.TryParse(row.FamilyId, out _).Should().BeTrue();
            row.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddDays(7), TimeSpan.FromMinutes(1));
        }

        [Fact]
        public async Task CreateSessionAsync_ShouldNotRevokeOtherSessions()
        {
            await CreateService().CreateSessionAsync(UserId, AuthClient.App);

            _repositoryMock.Verify(x => x.RevokeAllForUserAsync(It.IsAny<string>(), It.IsAny<DateTime>()), Times.Never);
            _repositoryMock.Verify(x => x.RevokeFamilyAsync(It.IsAny<string>(), It.IsAny<DateTime>()), Times.Never);
            _repositoryMock.Verify(x => x.DeleteExpiredForUserAsync(UserId, It.IsAny<DateTime>()), Times.Once);
        }

        // ── RotateAsync ─────────────────────────────────────

        [Fact]
        public async Task RotateAsync_WhenTokenIsActive_ShouldConsumeAndIssueInSameFamily()
        {
            SetUpStored(Stored());
            _repositoryMock.Setup(x => x.TryConsumeAsync(10, It.IsAny<DateTime>())).ReturnsAsync(true);

            var token = await CreateService().RotateAsync(UserId, AuthClient.App, Token);

            token.Should().NotBeNullOrWhiteSpace().And.NotBe(Token);
            _added.Should().ContainSingle().Which.FamilyId.Should().Be(Family);
        }

        [Fact]
        public async Task RotateAsync_WhenTokenIsUnknown_ShouldReturnNullWithoutRevoking()
        {
            SetUpStored(null);

            var token = await CreateService().RotateAsync(UserId, AuthClient.App, Token);

            token.Should().BeNull();
            _repositoryMock.Verify(x => x.RevokeFamilyAsync(It.IsAny<string>(), It.IsAny<DateTime>()), Times.Never);
        }

        public static TheoryData<string> InvalidCases => new() { "outro-usuario", "outro-cliente", "revogado", "vencido" };

        [Theory]
        [MemberData(nameof(InvalidCases))]
        public async Task RotateAsync_WhenTokenDoesNotApply_ShouldReturnNullWithoutConsuming(string invalidCase)
        {
            SetUpStored(invalidCase switch
            {
                "outro-usuario" => Stored(userId: "user-2"),
                "outro-cliente" => Stored(client: AuthClient.Web),
                "revogado" => Stored(revokedAt: DateTime.UtcNow.AddMinutes(-1)),
                _ => Stored(expiresAt: DateTime.UtcNow.AddSeconds(-1)),
            });

            var token = await CreateService().RotateAsync(UserId, AuthClient.App, Token);

            token.Should().BeNull();
            _added.Should().BeEmpty();
            _repositoryMock.Verify(x => x.TryConsumeAsync(It.IsAny<long>(), It.IsAny<DateTime>()), Times.Never);
        }

        [Fact]
        public async Task RotateAsync_WhenReusedInsideGraceWindow_ShouldIssueInSameFamilyWithoutRevoking()
        {
            SetUpStored(Stored(consumedAt: DateTime.UtcNow.AddSeconds(-2)));

            var token = await CreateService().RotateAsync(UserId, AuthClient.App, Token);

            token.Should().NotBeNullOrWhiteSpace();
            _added.Should().ContainSingle().Which.FamilyId.Should().Be(Family);
            _repositoryMock.Verify(x => x.RevokeFamilyAsync(It.IsAny<string>(), It.IsAny<DateTime>()), Times.Never);
        }

        [Fact]
        public async Task RotateAsync_WhenReusedAfterGraceWindow_ShouldRevokeOnlyThatFamily()
        {
            SetUpStored(Stored(consumedAt: DateTime.UtcNow.AddMinutes(-1)));

            var token = await CreateService().RotateAsync(UserId, AuthClient.App, Token);

            token.Should().BeNull();
            _added.Should().BeEmpty();
            _repositoryMock.Verify(x => x.RevokeFamilyAsync(Family, It.IsAny<DateTime>()), Times.Once);
            _repositoryMock.Verify(x => x.RevokeAllForUserAsync(It.IsAny<string>(), It.IsAny<DateTime>()), Times.Never);
        }

        [Fact]
        public async Task RotateAsync_WhenLosesRaceToAnotherRefresh_ShouldIssueInSameFamily()
        {
            _repositoryMock.SetupSequence(x => x.FindByHashAsync(HashOf(Token)))
                .ReturnsAsync(Stored())
                .ReturnsAsync(Stored(consumedAt: DateTime.UtcNow));
            _repositoryMock.Setup(x => x.TryConsumeAsync(10, It.IsAny<DateTime>())).ReturnsAsync(false);

            var token = await CreateService().RotateAsync(UserId, AuthClient.App, Token);

            token.Should().NotBeNullOrWhiteSpace();
            _added.Should().ContainSingle().Which.FamilyId.Should().Be(Family);
        }

        [Fact]
        public async Task RotateAsync_WhenLosesRaceToLogout_ShouldReturnNullWithoutIssuing()
        {
            _repositoryMock.SetupSequence(x => x.FindByHashAsync(HashOf(Token)))
                .ReturnsAsync(Stored())
                .ReturnsAsync(Stored(revokedAt: DateTime.UtcNow));
            _repositoryMock.Setup(x => x.TryConsumeAsync(10, It.IsAny<DateTime>())).ReturnsAsync(false);

            var token = await CreateService().RotateAsync(UserId, AuthClient.App, Token);

            token.Should().BeNull();
            _added.Should().BeEmpty();
        }

        // ── RevokeSessionAsync ──────────────────────────────

        [Fact]
        public async Task RevokeSessionAsync_ShouldRevokeOnlyTheFamilyOfTheToken()
        {
            SetUpStored(Stored());

            await CreateService().RevokeSessionAsync(Token);

            _repositoryMock.Verify(x => x.RevokeFamilyAsync(Family, It.IsAny<DateTime>()), Times.Once);
            _repositoryMock.Verify(x => x.RevokeAllForUserAsync(It.IsAny<string>(), It.IsAny<DateTime>()), Times.Never);
        }

        [Fact]
        public async Task RevokeSessionAsync_WhenTokenIsUnknown_ShouldDoNothing()
        {
            SetUpStored(null);

            await CreateService().RevokeSessionAsync(Token);

            _repositoryMock.Verify(x => x.RevokeFamilyAsync(It.IsAny<string>(), It.IsAny<DateTime>()), Times.Never);
        }

        [Fact]
        public async Task RevokeAllSessionsAsync_ShouldRevokeEverySessionOfTheUser()
        {
            await CreateService().RevokeAllSessionsAsync(UserId);

            _repositoryMock.Verify(x => x.RevokeAllForUserAsync(UserId, It.IsAny<DateTime>()), Times.Once);
        }
    }
}
