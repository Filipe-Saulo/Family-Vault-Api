using FamilyVaultApi.Data.Entities;
using FamilyVaultApi.Exceptions;
using FamilyVaultApi.Models.Dto.Requests.Account;
using FamilyVaultApi.Models.Dto.Requests.User;
using FamilyVaultApi.Models.Internal.Enums;
using FamilyVaultApi.Services.IService;
using FamilyVaultApi.Services.Service;
using FamilyVaultApi.UnitTests.Helpers;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Moq;

namespace FamilyVaultApi.UnitTests.Services
{
    public class PasswordServiceTests
    {
        private const string Phone = "+5511987654312";
        private const string InvalidCodeMessage = "Código inválido ou expirado.";

        private readonly Mock<IIdentityService> _identityMock = new();
        private readonly Mock<IPhoneNumberService> _phoneMock = new();
        private readonly Mock<IVerificationCodeSender> _senderMock = new();
        private readonly Mock<IVerificationSendLimiter> _limiterMock = new();
        private readonly User _user = new() { Id = "user-1", PhoneNumber = Phone };

        public PasswordServiceTests()
        {
            SetUpPhone(valid: true);
            _limiterMock.Setup(x => x.TryAcquire(Phone)).Returns(true);
        }

        private PasswordService CreateService() => new(_identityMock.Object, _phoneMock.Object, _senderMock.Object, _limiterMock.Object);

        private void SetUpPhone(bool valid)
        {
            string? formatted = valid ? Phone : null;
            _phoneMock
                .Setup(x => x.TryValidateAndFormat(It.IsAny<string?>(), It.IsAny<string>(), out formatted))
                .Returns(valid);
        }

        private void SetUpUser(User? user) =>
            _identityMock.Setup(x => x.FindByPhoneAsync(Phone)).ReturnsAsync(user);

        private static ForgotPasswordRequestDto Forgot() => new() { Phone = "11987654312" };

        private static ResetPasswordWithCodeRequestDto Reset(string password = "Senha@123", string confirm = "Senha@123") =>
            new() { Phone = "11987654312", Code = "123456", Password = password, PasswordConfirm = confirm };

        private static IEnumerable<IdentityError> Ok() => Enumerable.Empty<IdentityError>();

        private static IEnumerable<IdentityError> Fail() => new[] { new IdentityError { Code = "X", Description = "Senha fraca." } };

        // ── ForgotPasswordAsync ──────────────────────────────────────────────

        [Fact]
        public async Task ForgotPasswordAsync_WhenAccountExists_ShouldGenerateCodeAndSendToE164Phone()
        {
            SetUpUser(_user);
            _identityMock.Setup(x => x.IsLockedOutAsync(_user)).ReturnsAsync(false);
            _identityMock.Setup(x => x.GeneratePasswordResetCodeAsync(_user, VerificationChannel.Sms)).ReturnsAsync("654321");

            await CreateService().ForgotPasswordAsync(Forgot());

            _senderMock.Verify(x => x.SendAsync(new VerificationCodeMessage(VerificationChannel.Sms, Phone, "654321", VerificationPurpose.PasswordReset)), Times.Once);
        }

        [Fact]
        public async Task ForgotPasswordAsync_WhenAccountDoesNotExist_ShouldNotGenerateNorSendAndNotThrow()
        {
            SetUpUser(null);

            Func<Task> act = () => CreateService().ForgotPasswordAsync(Forgot());

            await act.Should().NotThrowAsync();
            _identityMock.Verify(x => x.GeneratePasswordResetCodeAsync(It.IsAny<User>(), It.IsAny<VerificationChannel>()), Times.Never);
            _senderMock.Verify(x => x.SendAsync(It.IsAny<VerificationCodeMessage>()), Times.Never);
        }

        [Fact]
        public async Task ForgotPasswordAsync_WhenPhoneIsInvalid_ShouldNotSearchNorSendAndNotThrow()
        {
            SetUpPhone(valid: false);

            Func<Task> act = () => CreateService().ForgotPasswordAsync(Forgot());

            await act.Should().NotThrowAsync();
            _identityMock.Verify(x => x.FindByPhoneAsync(It.IsAny<string>()), Times.Never);
            _senderMock.Verify(x => x.SendAsync(It.IsAny<VerificationCodeMessage>()), Times.Never);
        }

        [Fact]
        public async Task ForgotPasswordAsync_WhenAccountIsLockedOut_ShouldNotGenerateNorSendAndNotThrow()
        {
            SetUpUser(_user);
            _identityMock.Setup(x => x.IsLockedOutAsync(_user)).ReturnsAsync(true);

            Func<Task> act = () => CreateService().ForgotPasswordAsync(Forgot());

            await act.Should().NotThrowAsync();
            _identityMock.Verify(x => x.GeneratePasswordResetCodeAsync(It.IsAny<User>(), It.IsAny<VerificationChannel>()), Times.Never);
            _senderMock.Verify(x => x.SendAsync(It.IsAny<VerificationCodeMessage>()), Times.Never);
        }

        [Fact]
        public async Task ForgotPasswordAsync_WhenPhoneLimitIsReached_ShouldNotGenerateNorSendAndNotThrow()
        {
            SetUpUser(_user);
            _limiterMock.Setup(x => x.TryAcquire(Phone)).Returns(false);

            Func<Task> act = () => CreateService().ForgotPasswordAsync(Forgot());

            await act.Should().NotThrowAsync();
            _identityMock.Verify(x => x.GeneratePasswordResetCodeAsync(It.IsAny<User>(), It.IsAny<VerificationChannel>()), Times.Never);
            _senderMock.Verify(x => x.SendAsync(It.IsAny<VerificationCodeMessage>()), Times.Never);
        }

        [Fact]
        public async Task ForgotPasswordAsync_WhenAccountDoesNotExist_ShouldNotConsumeThePhoneLimit()
        {
            SetUpUser(null);

            await CreateService().ForgotPasswordAsync(Forgot());

            _limiterMock.Verify(x => x.TryAcquire(It.IsAny<string>()), Times.Never);
        }

        // ── ResetPasswordAsync ───────────────────────────────────────────────

        [Fact]
        public async Task ResetPasswordAsync_WhenPasswordsDoNotMatch_ShouldThrowBadRequestWithoutTouchingAccount()
        {
            Func<Task> act = () => CreateService().ResetPasswordAsync(Reset(confirm: "Outra@123"));

            await act.Should().ThrowAsync<BadRequestException>();
            _identityMock.Verify(x => x.FindByPhoneAsync(It.IsAny<string>()), Times.Never);
            _identityMock.Verify(x => x.ResetPasswordAsync(It.IsAny<User>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task ResetPasswordAsync_WhenPhoneIsInvalid_ShouldThrowGenericBadRequest()
        {
            SetUpPhone(valid: false);

            Func<Task> act = () => CreateService().ResetPasswordAsync(Reset());

            (await act.Should().ThrowAsync<BadRequestException>()).WithMessage(InvalidCodeMessage);
        }

        [Fact]
        public async Task ResetPasswordAsync_WhenAccountDoesNotExist_ShouldThrowGenericBadRequest()
        {
            SetUpUser(null);

            Func<Task> act = () => CreateService().ResetPasswordAsync(Reset());

            (await act.Should().ThrowAsync<BadRequestException>()).WithMessage(InvalidCodeMessage);
            _identityMock.Verify(x => x.AccessFailedAsync(It.IsAny<User>()), Times.Never);
        }

        [Fact]
        public async Task ResetPasswordAsync_WhenAccountIsLockedOut_ShouldThrowGenericBadRequestWithoutVerifyingCode()
        {
            SetUpUser(_user);
            _identityMock.Setup(x => x.IsLockedOutAsync(_user)).ReturnsAsync(true);

            Func<Task> act = () => CreateService().ResetPasswordAsync(Reset());

            (await act.Should().ThrowAsync<BadRequestException>()).WithMessage(InvalidCodeMessage);
            _identityMock.Verify(x => x.VerifyPasswordResetCodeAsync(It.IsAny<User>(), It.IsAny<string>(), It.IsAny<VerificationChannel>()), Times.Never);
            _identityMock.Verify(x => x.ResetPasswordAsync(It.IsAny<User>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task ResetPasswordAsync_WhenCodeIsInvalid_ShouldCountFailureAndNotResetPassword()
        {
            SetUpUser(_user);
            _identityMock.Setup(x => x.IsLockedOutAsync(_user)).ReturnsAsync(false);
            _identityMock.Setup(x => x.VerifyPasswordResetCodeAsync(_user, "123456", VerificationChannel.Sms)).ReturnsAsync(false);

            Func<Task> act = () => CreateService().ResetPasswordAsync(Reset());

            (await act.Should().ThrowAsync<BadRequestException>()).WithMessage(InvalidCodeMessage);
            _identityMock.Verify(x => x.AccessFailedAsync(_user), Times.Once);
            _identityMock.Verify(x => x.ResetPasswordAsync(It.IsAny<User>(), It.IsAny<string>()), Times.Never);
            _identityMock.Verify(x => x.RevokeRefreshTokensAsync(It.IsAny<User>()), Times.Never);
        }

        [Fact]
        public async Task ResetPasswordAsync_WhenIdentityRejectsPassword_ShouldThrowBadRequestAndNotRevoke()
        {
            SetUpUser(_user);
            _identityMock.Setup(x => x.IsLockedOutAsync(_user)).ReturnsAsync(false);
            _identityMock.Setup(x => x.VerifyPasswordResetCodeAsync(_user, "123456", VerificationChannel.Sms)).ReturnsAsync(true);
            _identityMock.Setup(x => x.ResetPasswordAsync(_user, "Senha@123")).ReturnsAsync(Fail());

            Func<Task> act = () => CreateService().ResetPasswordAsync(Reset());

            (await act.Should().ThrowAsync<BadRequestException>()).WithMessage("*Senha fraca.*");
            _identityMock.Verify(x => x.RevokeRefreshTokensAsync(It.IsAny<User>()), Times.Never);
            _identityMock.Verify(x => x.UnlockAsync(It.IsAny<User>()), Times.Never);
        }

        [Fact]
        public async Task ResetPasswordAsync_WhenCodeIsValid_ShouldResetUnlockAndRevokeRefreshTokens()
        {
            SetUpUser(_user);
            _identityMock.Setup(x => x.IsLockedOutAsync(_user)).ReturnsAsync(false);
            _identityMock.Setup(x => x.VerifyPasswordResetCodeAsync(_user, "123456", VerificationChannel.Sms)).ReturnsAsync(true);
            _identityMock.Setup(x => x.ResetPasswordAsync(_user, "Senha@123")).ReturnsAsync(Ok());

            await CreateService().ResetPasswordAsync(Reset());

            _identityMock.Verify(x => x.ResetPasswordAsync(_user, "Senha@123"), Times.Once);
            _identityMock.Verify(x => x.UnlockAsync(_user), Times.Once);
            _identityMock.Verify(x => x.RevokeRefreshTokensAsync(_user), Times.Once);
            _identityMock.Verify(x => x.AccessFailedAsync(It.IsAny<User>()), Times.Never);
        }

        // ── Por e-mail ───────────────────────────────────────────────────────

        private const string Email = "fulano@exemplo.com";

        private static ForgotPasswordRequestDto ForgotByEmail(string email = " Fulano@Exemplo.com ") => new() { Email = email };

        private static ResetPasswordWithCodeRequestDto ResetByEmail() =>
            new() { Email = " Fulano@Exemplo.com ", Code = "123456", Password = "Senha@123", PasswordConfirm = "Senha@123" };

        private void SetUpEmailAccount(User? user) =>
            _identityMock.Setup(x => x.FindByNameAsync(Email)).ReturnsAsync(user);

        private static User EmailUser() => new() { Id = "user-2", UserName = Email, Email = Email };

        [Fact]
        public async Task ForgotPasswordAsync_WhenEmailIsTheUserName_ShouldGenerateEmailCodeAndSendToNormalizedEmail()
        {
            var user = EmailUser();
            SetUpEmailAccount(user);
            _limiterMock.Setup(x => x.TryAcquire(Email)).Returns(true);
            _identityMock.Setup(x => x.IsLockedOutAsync(user)).ReturnsAsync(false);
            _identityMock.Setup(x => x.GeneratePasswordResetCodeAsync(user, VerificationChannel.Email)).ReturnsAsync("654321");

            await CreateService().ForgotPasswordAsync(ForgotByEmail());

            _senderMock.Verify(x => x.SendAsync(new VerificationCodeMessage(VerificationChannel.Email, Email, "654321", VerificationPurpose.PasswordReset)), Times.Once);
        }

        [Fact]
        public async Task ForgotPasswordAsync_WhenEmailIsOnlyTheOptionalEmailOfAPhoneAccount_ShouldNotSendNorThrow()
        {
            SetUpEmailAccount(new User { Id = "user-3", UserName = Phone, Email = Email });

            Func<Task> act = () => CreateService().ForgotPasswordAsync(ForgotByEmail());

            await act.Should().NotThrowAsync();
            _limiterMock.Verify(x => x.TryAcquire(It.IsAny<string>()), Times.Never);
            _senderMock.Verify(x => x.SendAsync(It.IsAny<VerificationCodeMessage>()), Times.Never);
        }

        [Fact]
        public async Task ForgotPasswordAsync_WhenEmailIsInvalid_ShouldNotSearchNorSendAndNotThrow()
        {
            Func<Task> act = () => CreateService().ForgotPasswordAsync(ForgotByEmail("nao-e-email"));

            await act.Should().NotThrowAsync();
            _identityMock.Verify(x => x.FindByNameAsync(It.IsAny<string>()), Times.Never);
            _senderMock.Verify(x => x.SendAsync(It.IsAny<VerificationCodeMessage>()), Times.Never);
        }

        [Fact]
        public async Task ForgotPasswordAsync_WhenEmailLimitIsReached_ShouldNotSendNorThrow()
        {
            var user = EmailUser();
            SetUpEmailAccount(user);
            _limiterMock.Setup(x => x.TryAcquire(Email)).Returns(false);

            Func<Task> act = () => CreateService().ForgotPasswordAsync(ForgotByEmail());

            await act.Should().NotThrowAsync();
            _senderMock.Verify(x => x.SendAsync(It.IsAny<VerificationCodeMessage>()), Times.Never);
        }

        [Theory]
        [InlineData(true, true)]
        [InlineData(false, false)]
        public async Task ForgotPasswordAsync_WhenBothOrNeitherIsInformed_ShouldThrowBadRequest(bool phone, bool email)
        {
            var dto = new ForgotPasswordRequestDto { Phone = phone ? "11987654312" : null, Email = email ? Email : null };

            Func<Task> act = () => CreateService().ForgotPasswordAsync(dto);

            await act.Should().ThrowAsync<BadRequestException>();
        }

        [Fact]
        public async Task ResetPasswordAsync_WhenBothPhoneAndEmailAreInformed_ShouldThrowBadRequest()
        {
            var dto = ResetByEmail();
            dto.Phone = "11987654312";

            Func<Task> act = () => CreateService().ResetPasswordAsync(dto);

            await act.Should().ThrowAsync<BadRequestException>();
        }

        [Fact]
        public async Task ResetPasswordAsync_WhenEmailCodeIsValid_ShouldVerifyWithEmailChannelAndReset()
        {
            var user = EmailUser();
            SetUpEmailAccount(user);
            _identityMock.Setup(x => x.IsLockedOutAsync(user)).ReturnsAsync(false);
            _identityMock.Setup(x => x.VerifyPasswordResetCodeAsync(user, "123456", VerificationChannel.Email)).ReturnsAsync(true);
            _identityMock.Setup(x => x.ResetPasswordAsync(user, "Senha@123")).ReturnsAsync(Ok());

            await CreateService().ResetPasswordAsync(ResetByEmail());

            _identityMock.Verify(x => x.ResetPasswordAsync(user, "Senha@123"), Times.Once);
            _identityMock.Verify(x => x.UnlockAsync(user), Times.Once);
            _identityMock.Verify(x => x.RevokeRefreshTokensAsync(user), Times.Once);
        }

        [Fact]
        public async Task ResetPasswordAsync_WhenEmailCodeIsInvalid_ShouldCountFailureAndThrowGenericBadRequest()
        {
            var user = EmailUser();
            SetUpEmailAccount(user);
            _identityMock.Setup(x => x.IsLockedOutAsync(user)).ReturnsAsync(false);
            _identityMock.Setup(x => x.VerifyPasswordResetCodeAsync(user, "123456", VerificationChannel.Email)).ReturnsAsync(false);

            Func<Task> act = () => CreateService().ResetPasswordAsync(ResetByEmail());

            (await act.Should().ThrowAsync<BadRequestException>()).WithMessage(InvalidCodeMessage);
            _identityMock.Verify(x => x.AccessFailedAsync(user), Times.Once);
            _identityMock.Verify(x => x.ResetPasswordAsync(It.IsAny<User>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task ResetPasswordAsync_WhenEmailIsNotTheUserName_ShouldThrowGenericBadRequestWithoutVerifyingCode()
        {
            SetUpEmailAccount(new User { Id = "user-3", UserName = Phone, Email = Email });

            Func<Task> act = () => CreateService().ResetPasswordAsync(ResetByEmail());

            (await act.Should().ThrowAsync<BadRequestException>()).WithMessage(InvalidCodeMessage);
            _identityMock.Verify(x => x.VerifyPasswordResetCodeAsync(It.IsAny<User>(), It.IsAny<string>(), It.IsAny<VerificationChannel>()), Times.Never);
        }

        // ── ChangePasswordAsync ──────────────────────────────────────────────

        private static ChangePasswordRequestDto Change(string password = "Nova@123", string confirm = "Nova@123") =>
            new() { CurrentPassword = "Atual@123", Password = password, PasswordConfirm = confirm };

        [Fact]
        public async Task ChangePasswordAsync_WhenPasswordsDoNotMatch_ShouldThrowBadRequest()
        {
            Func<Task> act = () => CreateService().ChangePasswordAsync(Change(confirm: "x"), ClaimsPrincipalTestHelper.CreateUser("user-1"));

            await act.Should().ThrowAsync<BadRequestException>();
            _identityMock.Verify(x => x.ChangePasswordAsync(It.IsAny<User>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task ChangePasswordAsync_WhenTokenHasNoUid_ShouldThrowUnauthorized()
        {
            Func<Task> act = () => CreateService().ChangePasswordAsync(Change(), ClaimsPrincipalTestHelper.CreateUnauthenticated());

            await act.Should().ThrowAsync<UnauthorizedAccessException>();
        }

        [Fact]
        public async Task ChangePasswordAsync_WhenUserFromTokenDoesNotExist_ShouldThrowUnauthorized()
        {
            _identityMock.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync((User?)null);

            Func<Task> act = () => CreateService().ChangePasswordAsync(Change(), ClaimsPrincipalTestHelper.CreateUser("user-1"));

            await act.Should().ThrowAsync<UnauthorizedAccessException>();
        }

        [Fact]
        public async Task ChangePasswordAsync_WhenCurrentPasswordIsWrong_ShouldCountFailureThrowBadRequestAndNotRevoke()
        {
            _identityMock.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(_user);
            _identityMock.Setup(x => x.IsLockedOutAsync(_user)).ReturnsAsync(false);
            _identityMock.Setup(x => x.ChangePasswordAsync(_user, "Atual@123", "Nova@123"))
                .ReturnsAsync(new[] { new IdentityError { Code = "PasswordMismatch", Description = "Senha incorreta." } });

            Func<Task> act = () => CreateService().ChangePasswordAsync(Change(), ClaimsPrincipalTestHelper.CreateUser("user-1"));

            await act.Should().ThrowAsync<BadRequestException>();
            _identityMock.Verify(x => x.AccessFailedAsync(_user), Times.Once);
            _identityMock.Verify(x => x.RevokeRefreshTokensAsync(It.IsAny<User>()), Times.Never);
            _identityMock.Verify(x => x.ResetAccessFailedCountAsync(It.IsAny<User>()), Times.Never);
        }

        [Fact]
        public async Task ChangePasswordAsync_WhenNewPasswordIsWeak_ShouldThrowBadRequestWithoutCountingFailure()
        {
            _identityMock.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(_user);
            _identityMock.Setup(x => x.IsLockedOutAsync(_user)).ReturnsAsync(false);
            _identityMock.Setup(x => x.ChangePasswordAsync(_user, "Atual@123", "Nova@123")).ReturnsAsync(Fail());

            Func<Task> act = () => CreateService().ChangePasswordAsync(Change(), ClaimsPrincipalTestHelper.CreateUser("user-1"));

            await act.Should().ThrowAsync<BadRequestException>();
            _identityMock.Verify(x => x.AccessFailedAsync(It.IsAny<User>()), Times.Never);
            _identityMock.Verify(x => x.RevokeRefreshTokensAsync(It.IsAny<User>()), Times.Never);
        }

        [Fact]
        public async Task ChangePasswordAsync_WhenAccountIsLockedOut_ShouldThrowBadRequestWithoutCallingIdentityToChange()
        {
            _identityMock.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(_user);
            _identityMock.Setup(x => x.IsLockedOutAsync(_user)).ReturnsAsync(true);

            Func<Task> act = () => CreateService().ChangePasswordAsync(Change(), ClaimsPrincipalTestHelper.CreateUser("user-1"));

            (await act.Should().ThrowAsync<BadRequestException>()).WithMessage("Senha atual incorreta.");
            _identityMock.Verify(x => x.ChangePasswordAsync(It.IsAny<User>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            _identityMock.Verify(x => x.RevokeRefreshTokensAsync(It.IsAny<User>()), Times.Never);
        }

        [Fact]
        public async Task ChangePasswordAsync_WhenSucceeds_ShouldUseUidFromTokenResetFailedCountAndRevokeRefreshTokens()
        {
            _identityMock.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(_user);
            _identityMock.Setup(x => x.IsLockedOutAsync(_user)).ReturnsAsync(false);
            _identityMock.Setup(x => x.ChangePasswordAsync(_user, "Atual@123", "Nova@123")).ReturnsAsync(Ok());

            await CreateService().ChangePasswordAsync(Change(), ClaimsPrincipalTestHelper.CreateUser("user-1"));

            _identityMock.Verify(x => x.ChangePasswordAsync(_user, "Atual@123", "Nova@123"), Times.Once);
            _identityMock.Verify(x => x.ResetAccessFailedCountAsync(_user), Times.Once);
            _identityMock.Verify(x => x.RevokeRefreshTokensAsync(_user), Times.Once);
            _identityMock.Verify(x => x.AccessFailedAsync(It.IsAny<User>()), Times.Never);
        }

        // ── SetUserPasswordAsync ─────────────────────────────────────────────

        private static SetUserPasswordRequestDto SetPwd(string password = "Nova@123", string confirm = "Nova@123") =>
            new() { Password = password, PasswordConfirm = confirm };

        [Fact]
        public async Task SetUserPasswordAsync_WhenPasswordsDoNotMatch_ShouldThrowBadRequest()
        {
            Func<Task> act = () => CreateService().SetUserPasswordAsync("user-1", SetPwd(confirm: "x"));

            await act.Should().ThrowAsync<BadRequestException>();
            _identityMock.Verify(x => x.ResetPasswordAsync(It.IsAny<User>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task SetUserPasswordAsync_WhenUserDoesNotExist_ShouldThrowNotFound()
        {
            _identityMock.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync((User?)null);

            Func<Task> act = () => CreateService().SetUserPasswordAsync("user-1", SetPwd());

            await act.Should().ThrowAsync<NotFoundException>();
        }

        [Fact]
        public async Task SetUserPasswordAsync_WhenIdentityRejectsPassword_ShouldThrowBadRequestAndNotRevoke()
        {
            _identityMock.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(_user);
            _identityMock.Setup(x => x.ResetPasswordAsync(_user, "Nova@123")).ReturnsAsync(Fail());

            Func<Task> act = () => CreateService().SetUserPasswordAsync("user-1", SetPwd());

            await act.Should().ThrowAsync<BadRequestException>();
            _identityMock.Verify(x => x.RevokeRefreshTokensAsync(It.IsAny<User>()), Times.Never);
        }

        [Fact]
        public async Task SetUserPasswordAsync_WhenSucceeds_ShouldResetAndRevokeRefreshTokens()
        {
            _identityMock.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(_user);
            _identityMock.Setup(x => x.ResetPasswordAsync(_user, "Nova@123")).ReturnsAsync(Ok());

            await CreateService().SetUserPasswordAsync("user-1", SetPwd());

            _identityMock.Verify(x => x.ResetPasswordAsync(_user, "Nova@123"), Times.Once);
            _identityMock.Verify(x => x.RevokeRefreshTokensAsync(_user), Times.Once);
        }
    }
}
