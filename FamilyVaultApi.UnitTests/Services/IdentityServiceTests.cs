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
        private readonly Mock<IRefreshTokenService> _refreshTokenServiceMock = new();
        private readonly User _user = new() { Id = "user-1", SecurityStamp = "stamp-1" };

        public IdentityServiceTests()
        {
            _userManagerMock = new Mock<UserManager<User>>(Mock.Of<IUserStore<User>>(), null, null, null, null, null, null, null, null);
            _roleManagerMock = new Mock<RoleManager<IdentityRole>>(Mock.Of<IRoleStore<IdentityRole>>(), null, null, null, null);
        }

        private IdentityService CreateService() => new(_userManagerMock.Object, _roleManagerMock.Object, _phoneMock.Object, _refreshTokenServiceMock.Object);

        // ── RevokeRefreshTokensAsync ─────────────────────────────────────────

        [Fact]
        public async Task RevokeRefreshTokensAsync_ShouldRevokeAllSessionsAndUpdateSecurityStamp()
        {
            await CreateService().RevokeRefreshTokensAsync(_user);

            _refreshTokenServiceMock.Verify(x => x.RevokeAllSessionsAsync(_user.Id), Times.Once);
            _userManagerMock.Verify(x => x.UpdateSecurityStampAsync(_user), Times.Once);
        }

        // ── FindByIdAsync ────────────────────────────────────────────────────

        [Fact]
        public async Task FindByIdAsync_ShouldDelegateToUserManager()
        {
            _userManagerMock.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(_user);

            var result = await CreateService().FindByIdAsync("user-1");

            result.Should().BeSameAs(_user);
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

            var result = await CreateService().GeneratePasswordResetCodeAsync(_user, VerificationChannel.Sms);

            result.Should().Be("123456");
        }

        [Fact]
        public async Task GeneratePasswordResetCodeAsync_WhenChannelIsEmail_ShouldUseDefaultEmailProvider()
        {
            _userManagerMock.Setup(x => x.GenerateUserTokenAsync(_user, TokenOptions.DefaultEmailProvider, "ResetPasswordCode")).ReturnsAsync("654321");

            var result = await CreateService().GeneratePasswordResetCodeAsync(_user, VerificationChannel.Email);

            result.Should().Be("654321");
        }

        [Fact]
        public async Task VerifyPasswordResetCodeAsync_WhenChannelIsEmail_ShouldUseDefaultEmailProvider()
        {
            _userManagerMock.Setup(x => x.VerifyUserTokenAsync(_user, TokenOptions.DefaultEmailProvider, "ResetPasswordCode", "654321")).ReturnsAsync(true);

            var result = await CreateService().VerifyPasswordResetCodeAsync(_user, "654321", VerificationChannel.Email);

            result.Should().BeTrue();
        }

        [Theory]
        [InlineData(VerificationChannel.Sms, "+5511987654312")]
        [InlineData(VerificationChannel.Email, "fulano@exemplo.com")]
        public async Task RegisterVerifiedUser_ShouldUseDestinationAsUserNameAndConfirmOnlyThatChannel(VerificationChannel channel, string destination)
        {
            User? created = null;
            _userManagerMock.Setup(x => x.CreateAsync(It.IsAny<User>(), "Senha@123"))
                .Callback<User, string>((u, _) => created = u)
                .ReturnsAsync(IdentityResult.Success);
            _userManagerMock.Setup(x => x.AddToRoleAsync(It.IsAny<User>(), "User")).ReturnsAsync(IdentityResult.Success);
            var dto = new CreateAccountRequestDto { FirstName = "Fulano", LastName = "Silva", Password = "Senha@123", PasswordConfirm = "Senha@123", Age = 30 };

            var errors = await CreateService().RegisterVerifiedUser(dto, destination, channel);

            errors.Should().BeEmpty();
            created!.UserName.Should().Be(destination);
            if (channel == VerificationChannel.Email)
            {
                created.Email.Should().Be(destination);
                created.EmailConfirmed.Should().BeTrue();
                created.PhoneNumber.Should().BeNull();
                created.PhoneNumberConfirmed.Should().BeFalse();
            }
            else
            {
                created.PhoneNumber.Should().Be(destination);
                created.PhoneNumberConfirmed.Should().BeTrue();
                created.Email.Should().BeNull();
                created.EmailConfirmed.Should().BeFalse();
            }
            _userManagerMock.Verify(x => x.AddToRoleAsync(created, "User"), Times.Once);
        }

        [Fact]
        public async Task RegisterVerifiedUser_WhenIdentityRejects_ShouldReturnErrorsWithoutAddingRole()
        {
            _userManagerMock.Setup(x => x.CreateAsync(It.IsAny<User>(), It.IsAny<string>()))
                .ReturnsAsync(IdentityResult.Failed(new IdentityError { Code = "PasswordTooShort", Description = "curta" }));
            var dto = new CreateAccountRequestDto { FirstName = "Fulano", LastName = "Silva", Password = "x", PasswordConfirm = "x", Age = 30 };

            var errors = await CreateService().RegisterVerifiedUser(dto, "fulano@exemplo.com", VerificationChannel.Email);

            errors.Should().ContainSingle(e => e.Code == "PasswordTooShort");
            _userManagerMock.Verify(x => x.AddToRoleAsync(It.IsAny<User>(), It.IsAny<string>()), Times.Never);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task VerifyPasswordResetCodeAsync_ShouldReturnWhatUserManagerSaysForSamePurpose(bool valid)
        {
            _userManagerMock.Setup(x => x.VerifyUserTokenAsync(_user, TokenOptions.DefaultPhoneProvider, "ResetPasswordCode", "123456")).ReturnsAsync(valid);

            var result = await CreateService().VerifyPasswordResetCodeAsync(_user, "123456", VerificationChannel.Sms);

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
