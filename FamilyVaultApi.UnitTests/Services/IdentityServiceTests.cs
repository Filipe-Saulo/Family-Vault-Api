using FamilyVaultApi.Data.Entities;
using FamilyVaultApi.Exceptions;
using FamilyVaultApi.Models.Internal.Enums;
using FamilyVaultApi.Models.Dto.Requests.Account;
using FamilyVaultApi.Services.IService;
using FamilyVaultApi.Services.Service;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Moq;

namespace FamilyVaultApi.UnitTests.Services
{
    public class IdentityServiceTests
    {
        private const string Provider = "RefreshTokenProvider";
        private static readonly string LegacyProvider = TokenOptions.DefaultProvider;

        private readonly Mock<UserManager<User>> _userManagerMock;
        private readonly Mock<RoleManager<IdentityRole>> _roleManagerMock;
        private readonly Mock<IPhoneNumberService> _phoneMock = new();
        private readonly User _user = new() { Id = "user-1", SecurityStamp = "stamp-1" };

        public IdentityServiceTests()
        {
            _userManagerMock = new Mock<UserManager<User>>(Mock.Of<IUserStore<User>>(), null, null, null, null, null, null, null, null);
            _roleManagerMock = new Mock<RoleManager<IdentityRole>>(Mock.Of<IRoleStore<IdentityRole>>(), null, null, null, null);
        }

        private IdentityService CreateService() => new(_userManagerMock.Object, _roleManagerMock.Object, _phoneMock.Object);

        private static string PurposeOf(AuthClient client) => client == AuthClient.Web ? "RefreshToken-web" : "RefreshToken-app";

        private static AuthClient Other(AuthClient client) => client == AuthClient.Web ? AuthClient.App : AuthClient.Web;

        // ── CreateRefreshTokenAsync ──────────────────────────────────────────

        [Theory]
        [InlineData(AuthClient.Web)]
        [InlineData(AuthClient.App)]
        public async Task CreateRefreshTokenAsync_ShouldRemoveAndStoreOnlyClientPurpose(AuthClient client)
        {
            var purpose = PurposeOf(client);
            _userManagerMock.Setup(x => x.GenerateUserTokenAsync(_user, Provider, purpose)).ReturnsAsync("novo-token");

            var result = await CreateService().CreateRefreshTokenAsync(_user, client);

            result.Should().Be("novo-token");
            _userManagerMock.Verify(x => x.RemoveAuthenticationTokenAsync(_user, Provider, purpose), Times.Once);
            _userManagerMock.Verify(x => x.SetAuthenticationTokenAsync(_user, Provider, purpose, "novo-token"), Times.Once);
            _userManagerMock.Verify(x => x.RemoveAuthenticationTokenAsync(_user, Provider, PurposeOf(Other(client))), Times.Never);
            _userManagerMock.Verify(x => x.SetAuthenticationTokenAsync(_user, Provider, PurposeOf(Other(client)), It.IsAny<string>()), Times.Never);
            _userManagerMock.Verify(x => x.GenerateUserTokenAsync(_user, Provider, PurposeOf(Other(client))), Times.Never);
        }

        // ── VerifyRefreshTokenAsync ──────────────────────────────────────────

        [Theory]
        [InlineData(AuthClient.Web)]
        [InlineData(AuthClient.App)]
        public async Task VerifyRefreshTokenAsync_WhenStoredEqualsReceivedAndVerifyIsTrue_ShouldReturnTrue(AuthClient client)
        {
            var purpose = PurposeOf(client);
            _userManagerMock.Setup(x => x.GetAuthenticationTokenAsync(_user, Provider, purpose)).ReturnsAsync("token-x");
            _userManagerMock.Setup(x => x.VerifyUserTokenAsync(_user, Provider, purpose, "token-x")).ReturnsAsync(true);

            var result = await CreateService().VerifyRefreshTokenAsync(_user, client, "token-x");

            result.Should().BeTrue();
        }

        [Theory]
        [InlineData(AuthClient.Web)]
        [InlineData(AuthClient.App)]
        public async Task VerifyRefreshTokenAsync_WhenStoredDiffersFromReceived_ShouldReturnFalseEvenIfVerifyIsTrue(AuthClient client)
        {
            var purpose = PurposeOf(client);
            _userManagerMock.Setup(x => x.GetAuthenticationTokenAsync(_user, Provider, purpose)).ReturnsAsync("token-atual");
            _userManagerMock.Setup(x => x.VerifyUserTokenAsync(_user, Provider, purpose, It.IsAny<string>())).ReturnsAsync(true);

            var result = await CreateService().VerifyRefreshTokenAsync(_user, client, "token-antigo-reusado");

            result.Should().BeFalse();
        }

        [Theory]
        [InlineData(AuthClient.Web)]
        [InlineData(AuthClient.App)]
        public async Task VerifyRefreshTokenAsync_WhenStoredIsNull_ShouldReturnFalse(AuthClient client)
        {
            var purpose = PurposeOf(client);
            _userManagerMock.Setup(x => x.GetAuthenticationTokenAsync(_user, Provider, purpose)).ReturnsAsync((string?)null);
            _userManagerMock.Setup(x => x.VerifyUserTokenAsync(_user, Provider, purpose, It.IsAny<string>())).ReturnsAsync(true);

            var result = await CreateService().VerifyRefreshTokenAsync(_user, client, "token-x");

            result.Should().BeFalse();
        }

        [Theory]
        [InlineData(AuthClient.Web)]
        [InlineData(AuthClient.App)]
        public async Task VerifyRefreshTokenAsync_WhenVerifyUserTokenIsFalse_ShouldReturnFalse(AuthClient client)
        {
            var purpose = PurposeOf(client);
            _userManagerMock.Setup(x => x.GetAuthenticationTokenAsync(_user, Provider, purpose)).ReturnsAsync("token-x");
            _userManagerMock.Setup(x => x.VerifyUserTokenAsync(_user, Provider, purpose, "token-x")).ReturnsAsync(false);

            var result = await CreateService().VerifyRefreshTokenAsync(_user, client, "token-x");

            result.Should().BeFalse();
        }

        [Theory]
        [InlineData(AuthClient.Web)]
        [InlineData(AuthClient.App)]
        public async Task VerifyRefreshTokenAsync_ShouldOnlyUseThePurposeOfTheClient(AuthClient client)
        {
            // Só o purpose do OUTRO cliente tem o token guardado: não pode validar.
            var otherPurpose = PurposeOf(Other(client));
            _userManagerMock.Setup(x => x.GetAuthenticationTokenAsync(_user, Provider, otherPurpose)).ReturnsAsync("token-x");
            _userManagerMock.Setup(x => x.VerifyUserTokenAsync(_user, Provider, otherPurpose, "token-x")).ReturnsAsync(true);

            var result = await CreateService().VerifyRefreshTokenAsync(_user, client, "token-x");

            result.Should().BeFalse();
            _userManagerMock.Verify(x => x.GetAuthenticationTokenAsync(_user, Provider, PurposeOf(client)), Times.Once);
            _userManagerMock.Verify(x => x.GetAuthenticationTokenAsync(_user, Provider, otherPurpose), Times.Never);
        }

        // ── RevokeRefreshTokensAsync ─────────────────────────────────────────

        [Fact]
        public async Task RevokeRefreshTokensAsync_ShouldRemoveAllThreePurposesAndUpdateSecurityStamp()
        {
            await CreateService().RevokeRefreshTokensAsync(_user);

            VerifyAllPurposesRemovedFromBothProviders();
            _userManagerMock.Verify(x => x.UpdateSecurityStampAsync(_user), Times.Once);
        }

        [Fact]
        public async Task RevokeRefreshTokensAsync_ShouldAlsoRemoveTokensStoredUnderTheLegacyDefaultProvider()
        {
            await CreateService().RevokeRefreshTokensAsync(_user);

            foreach (var purpose in new[] { "RefreshToken-web", "RefreshToken-app", "RefreshToken" })
                _userManagerMock.Verify(x => x.RemoveAuthenticationTokenAsync(_user, LegacyProvider, purpose), Times.Once);
        }

        private void VerifyAllPurposesRemovedFromBothProviders()
        {
            foreach (var provider in new[] { Provider, LegacyProvider })
                foreach (var purpose in new[] { "RefreshToken-web", "RefreshToken-app", "RefreshToken" })
                    _userManagerMock.Verify(x => x.RemoveAuthenticationTokenAsync(_user, provider, purpose), Times.Once);
        }

        // ── FindByIdAsync ────────────────────────────────────────────────────

        [Fact]
        public async Task FindByIdAsync_ShouldDelegateToUserManager()
        {
            _userManagerMock.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(_user);

            var result = await CreateService().FindByIdAsync("user-1");

            result.Should().BeSameAs(_user);
        }

        // ── LogoutAsync ──────────────────────────────────────────────────────

        [Fact]
        public async Task LogoutAsync_WhenUserExists_ShouldRevokeAllRefreshTokens()
        {
            _userManagerMock.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(_user);

            await CreateService().LogoutAsync("user-1");

            VerifyAllPurposesRemovedFromBothProviders();
            _userManagerMock.Verify(x => x.UpdateSecurityStampAsync(_user), Times.Once);
        }

        [Fact]
        public async Task LogoutAsync_WhenUserNotFound_ShouldThrowNotFoundException()
        {
            _userManagerMock.Setup(x => x.FindByIdAsync("nope")).ReturnsAsync((User?)null);

            Func<Task> act = () => CreateService().LogoutAsync("nope");

            await act.Should().ThrowAsync<NotFoundException>();
            _userManagerMock.Verify(x => x.UpdateSecurityStampAsync(It.IsAny<User>()), Times.Never);
        }

        // ── Senha: códigos, reset, troca e desbloqueio ───────────────────────

        [Fact]
        public async Task FindUserByLoginAsync_WhenPhoneIsInvalid_ShouldReturnNullWithoutQueryingUsers()
        {
            string? formatted = null;
            _phoneMock.Setup(x => x.TryValidateAndFormat("abc", "BR", out formatted)).Returns(false);

            var result = await CreateService().FindUserByLoginAsync(new LoginRequestDto { Phone = "abc", Password = "x" });

            result.Should().BeNull();
            _userManagerMock.Verify(x => x.Users, Times.Never);
        }

        [Fact]
        public async Task GeneratePasswordResetCodeAsync_ShouldUseDefaultPhoneProviderAndResetPasswordCodePurpose()
        {
            _userManagerMock.Setup(x => x.GenerateUserTokenAsync(_user, TokenOptions.DefaultPhoneProvider, "ResetPasswordCode")).ReturnsAsync("123456");

            var result = await CreateService().GeneratePasswordResetCodeAsync(_user);

            result.Should().Be("123456");
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task VerifyPasswordResetCodeAsync_ShouldReturnWhatUserManagerSaysForSamePurpose(bool valid)
        {
            _userManagerMock.Setup(x => x.VerifyUserTokenAsync(_user, TokenOptions.DefaultPhoneProvider, "ResetPasswordCode", "123456")).ReturnsAsync(valid);

            var result = await CreateService().VerifyPasswordResetCodeAsync(_user, "123456");

            result.Should().Be(valid);
        }

        [Fact]
        public async Task ResetPasswordAsync_WhenIdentitySucceeds_ShouldGenerateTokenResetAndReturnNoErrors()
        {
            _userManagerMock.Setup(x => x.GeneratePasswordResetTokenAsync(_user)).ReturnsAsync("reset-token");
            _userManagerMock.Setup(x => x.ResetPasswordAsync(_user, "reset-token", "Nova@123")).ReturnsAsync(IdentityResult.Success);

            var errors = await CreateService().ResetPasswordAsync(_user, "Nova@123");

            errors.Should().BeEmpty();
        }

        [Fact]
        public async Task ResetPasswordAsync_WhenIdentityFails_ShouldReturnItsErrors()
        {
            _userManagerMock.Setup(x => x.GeneratePasswordResetTokenAsync(_user)).ReturnsAsync("reset-token");
            _userManagerMock.Setup(x => x.ResetPasswordAsync(_user, "reset-token", "fraca"))
                .ReturnsAsync(IdentityResult.Failed(new IdentityError { Code = "PasswordTooShort", Description = "curta" }));

            var errors = await CreateService().ResetPasswordAsync(_user, "fraca");

            errors.Should().ContainSingle(e => e.Code == "PasswordTooShort");
        }

        [Fact]
        public async Task ChangePasswordAsync_WhenCurrentPasswordIsWrong_ShouldReturnErrors()
        {
            _userManagerMock.Setup(x => x.ChangePasswordAsync(_user, "errada", "Nova@123"))
                .ReturnsAsync(IdentityResult.Failed(new IdentityError { Code = "PasswordMismatch", Description = "errada" }));

            var errors = await CreateService().ChangePasswordAsync(_user, "errada", "Nova@123");

            errors.Should().ContainSingle(e => e.Code == "PasswordMismatch");
        }

        [Fact]
        public async Task ChangePasswordAsync_WhenSucceeds_ShouldReturnNoErrors()
        {
            _userManagerMock.Setup(x => x.ChangePasswordAsync(_user, "Atual@123", "Nova@123")).ReturnsAsync(IdentityResult.Success);

            var errors = await CreateService().ChangePasswordAsync(_user, "Atual@123", "Nova@123");

            errors.Should().BeEmpty();
        }

        [Fact]
        public async Task UnlockAsync_ShouldResetFailedCountAndClearLockoutEnd()
        {
            await CreateService().UnlockAsync(_user);

            _userManagerMock.Verify(x => x.ResetAccessFailedCountAsync(_user), Times.Once);
            _userManagerMock.Verify(x => x.SetLockoutEndDateAsync(_user, null), Times.Once);
        }
    }
}
