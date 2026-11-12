using FamilyVaultApi.Data.Entities;
using FamilyVaultApi.Exceptions;
using FamilyVaultApi.Models.Dto.Requests.McpToken;
using FamilyVaultApi.Repositories.IRepository;
using FamilyVaultApi.Services.Service;
using FamilyVaultApi.UnitTests.Helpers;
using FluentAssertions;
using Moq;
using System.Security;
using System.Security.Cryptography;
using System.Text;

namespace FamilyVaultApi.UnitTests.Services
{
    public class McpTokenServiceTests
    {
        private const string UserId = "user-1";
        private const string RawToken = "fv_pat_token-recebido";

        private readonly Mock<IPersonalAccessTokenRepository> _repositoryMock = new();
        private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 11, 12, 0, 0, TimeSpan.Zero));
        private readonly List<PersonalAccessToken> _added = new();

        public McpTokenServiceTests()
        {
            _repositoryMock
                .Setup(x => x.AddAsync(It.IsAny<PersonalAccessToken>()))
                .Callback<PersonalAccessToken>(t => { t.PersonalAccessTokenId = 7; _added.Add(t); })
                .Returns(Task.CompletedTask);
        }

        private McpTokenService CreateService() => new(_repositoryMock.Object, _time);

        private DateTime Now => _time.Now.UtcDateTime;

        private static string HashOf(string token) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

        private PersonalAccessToken Stored(DateTime? revokedAt = null, DateTime? expiresAt = null, DateTime? lastUsedAt = null,
            string userId = UserId, string scope = "mcp:read") => new()
            {
                PersonalAccessTokenId = 3,
                UserId = userId,
                Name = "Claude Code",
                TokenHash = HashOf(RawToken),
                Scope = scope,
                CreatedAt = Now.AddDays(-1),
                ExpiresAt = expiresAt ?? Now.AddDays(10),
                LastUsedAt = lastUsedAt,
                RevokedAt = revokedAt
            };

        // ── CreateAsync ─────────────────────────────────────

        [Fact]
        public async Task CreateAsync_WithoutExpiry_ShouldStoreOnlyTheHashWithNinetyDays()
        {
            var result = await CreateService().CreateAsync(new CreateMcpTokenDto { Name = "  Claude Code  " }, ClaimsPrincipalTestHelper.CreateUser(UserId));

            var row = _added.Should().ContainSingle().Subject;
            result.Token.Should().StartWith("fv_pat_");
            row.TokenHash.Should().Be(HashOf(result.Token)).And.NotContain("fv_pat_");
            row.UserId.Should().Be(UserId);
            row.Name.Should().Be("Claude Code");
            row.Scope.Should().Be("mcp:read");
            row.ExpiresAt.Should().Be(Now.AddDays(90));
            result.Id.Should().Be(7);
        }

        [Theory]
        [InlineData(30)]
        [InlineData(365)]
        public async Task CreateAsync_WithAllowedExpiry_ShouldUseIt(int days)
        {
            await CreateService().CreateAsync(new CreateMcpTokenDto { Name = "x", ExpiresInDays = days }, ClaimsPrincipalTestHelper.CreateUser(UserId));

            _added.Single().ExpiresAt.Should().Be(Now.AddDays(days));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(7)]
        [InlineData(366)]
        public async Task CreateAsync_WithOtherExpiry_ShouldThrowBadRequest(int days)
        {
            var act = () => CreateService().CreateAsync(new CreateMcpTokenDto { Name = "x", ExpiresInDays = days }, ClaimsPrincipalTestHelper.CreateUser(UserId));

            await act.Should().ThrowAsync<BadRequestException>();
            _added.Should().BeEmpty();
        }

        [Fact]
        public async Task CreateAsync_WithBlankName_ShouldThrowBadRequest()
        {
            var act = () => CreateService().CreateAsync(new CreateMcpTokenDto { Name = "   " }, ClaimsPrincipalTestHelper.CreateUser(UserId));

            await act.Should().ThrowAsync<BadRequestException>();
        }

        [Fact]
        public async Task CreateAsync_WithFiveActive_ShouldThrowConflict()
        {
            _repositoryMock.Setup(x => x.CountActiveForUserAsync(UserId, Now)).ReturnsAsync(5);

            var act = () => CreateService().CreateAsync(new CreateMcpTokenDto { Name = "x" }, ClaimsPrincipalTestHelper.CreateUser(UserId));

            await act.Should().ThrowAsync<ConflictException>();
            _added.Should().BeEmpty();
        }

        [Fact]
        public async Task CreateAsync_WithoutUid_ShouldThrowSecurityException()
        {
            var act = () => CreateService().CreateAsync(new CreateMcpTokenDto { Name = "x" }, ClaimsPrincipalTestHelper.CreateUnauthenticated());

            await act.Should().ThrowAsync<SecurityException>();
        }

        // ── GetMineAsync ────────────────────────────────────

        [Fact]
        public async Task GetMineAsync_ShouldMarkExpiredAndNeverReturnTheValue()
        {
            _repositoryMock.Setup(x => x.GetNotRevokedForUserAsync(UserId))
                .ReturnsAsync([Stored(), Stored(expiresAt: Now.AddMinutes(-1))]);

            var result = await CreateService().GetMineAsync(ClaimsPrincipalTestHelper.CreateUser(UserId));

            result.Select(t => t.IsExpired).Should().Equal(false, true);
            result.Should().AllSatisfy(t => t.GetType().GetProperty("Token").Should().BeNull());
        }

        // ── RevokeAsync ─────────────────────────────────────

        [Fact]
        public async Task RevokeAsync_OwnToken_ShouldRevoke()
        {
            _repositoryMock.Setup(x => x.FindByIdAsync(3)).ReturnsAsync(Stored());

            await CreateService().RevokeAsync(3, ClaimsPrincipalTestHelper.CreateUser(UserId));

            _repositoryMock.Verify(x => x.RevokeAsync(3, Now), Times.Once);
        }

        [Fact]
        public async Task RevokeAsync_TokenOfAnotherUser_ShouldThrowNotFoundEvenForAdministrator()
        {
            _repositoryMock.Setup(x => x.FindByIdAsync(3)).ReturnsAsync(Stored(userId: "outra"));

            var act = () => CreateService().RevokeAsync(3, ClaimsPrincipalTestHelper.CreateAdmin("admin-1"));

            await act.Should().ThrowAsync<NotFoundException>();
            _repositoryMock.Verify(x => x.RevokeAsync(It.IsAny<int>(), It.IsAny<DateTime>()), Times.Never);
        }

        [Fact]
        public async Task RevokeAsync_AlreadyRevoked_ShouldNotWriteAgain()
        {
            _repositoryMock.Setup(x => x.FindByIdAsync(3)).ReturnsAsync(Stored(revokedAt: Now.AddDays(-1)));

            await CreateService().RevokeAsync(3, ClaimsPrincipalTestHelper.CreateUser(UserId));

            _repositoryMock.Verify(x => x.RevokeAsync(It.IsAny<int>(), It.IsAny<DateTime>()), Times.Never);
        }

        // ── ValidateAsync ───────────────────────────────────

        [Fact]
        public async Task ValidateAsync_ValidToken_ShouldReturnItAndRecordUse()
        {
            _repositoryMock.Setup(x => x.FindByHashAsync(HashOf(RawToken))).ReturnsAsync(Stored());

            var token = await CreateService().ValidateAsync(RawToken);

            token.Should().NotBeNull();
            _repositoryMock.Verify(x => x.UpdateLastUsedAsync(3, Now), Times.Once);
        }

        [Fact]
        public async Task ValidateAsync_UsedLessThanAMinuteAgo_ShouldNotWriteLastUse()
        {
            _repositoryMock.Setup(x => x.FindByHashAsync(HashOf(RawToken))).ReturnsAsync(Stored(lastUsedAt: Now.AddSeconds(-30)));

            (await CreateService().ValidateAsync(RawToken)).Should().NotBeNull();

            _repositoryMock.Verify(x => x.UpdateLastUsedAsync(It.IsAny<int>(), It.IsAny<DateTime>()), Times.Never);
        }

        [Fact]
        public async Task ValidateAsync_RevokedToken_ShouldReturnNull()
        {
            _repositoryMock.Setup(x => x.FindByHashAsync(HashOf(RawToken))).ReturnsAsync(Stored(revokedAt: Now.AddMinutes(-1)));

            (await CreateService().ValidateAsync(RawToken)).Should().BeNull();
        }

        [Fact]
        public async Task ValidateAsync_ExpiredToken_ShouldReturnNull()
        {
            _repositoryMock.Setup(x => x.FindByHashAsync(HashOf(RawToken))).ReturnsAsync(Stored(expiresAt: Now));

            (await CreateService().ValidateAsync(RawToken)).Should().BeNull();
        }

        [Fact]
        public async Task ValidateAsync_OtherScope_ShouldReturnNull()
        {
            _repositoryMock.Setup(x => x.FindByHashAsync(HashOf(RawToken))).ReturnsAsync(Stored(scope: "mcp:write"));

            (await CreateService().ValidateAsync(RawToken)).Should().BeNull();
        }

        [Theory]
        [InlineData("")]
        [InlineData("eyJhbGciOiJIUzI1NiJ9.jwt.qualquer")]
        public async Task ValidateAsync_WithoutPrefix_ShouldReturnNullWithoutQuerying(string raw)
        {
            (await CreateService().ValidateAsync(raw)).Should().BeNull();

            _repositoryMock.Verify(x => x.FindByHashAsync(It.IsAny<string>()), Times.Never);
        }
    }
}
