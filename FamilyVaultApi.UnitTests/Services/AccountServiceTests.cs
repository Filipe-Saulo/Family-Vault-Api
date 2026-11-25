using FamilyVaultApi.Data.Entities;
using FamilyVaultApi.Exceptions;
using FamilyVaultApi.Models.Dto.Requests.Account;
using FamilyVaultApi.Models.Dto.Responses.Account;
using FamilyVaultApi.Models.Internal;
using FamilyVaultApi.Models.Internal.Enums;
using FamilyVaultApi.Repositories.IRepository;
using FamilyVaultApi.Services.IService;
using FamilyVaultApi.Services.Service;
using FamilyVaultApi.UnitTests.Builders.Account;
using FamilyVaultApi.UnitTests.Helpers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Moq;
using System.Security;
using System.Security.Claims;

namespace FamilyVaultApi.UnitTests.Services
{
    public class AccountServiceTests
    {
        private readonly Mock<IAccountRepository> _repositoryMock;
        private readonly Mock<IIdentityService> _identityServiceMock;
        private readonly Mock<IRefreshTokenService> _refreshTokenServiceMock = new();
        private readonly Mock<ITokenService> _tokenServiceMock;
        private readonly Mock<IPhoneNumberService> _phoneNumberServiceMock;
        private readonly Mock<IRegistrationCodeStore> _registrationCodeStoreMock = new();
        private readonly Mock<IVerificationSendLimiter> _sendLimiterMock = new();
        private readonly Mock<IVerificationCodeSender> _codeSenderMock = new();
        private readonly Mock<IGoogleIdTokenValidator> _googleValidatorMock = new();
        private Mock<IHttpContextAccessor> _httpContextAccessorMock;

        public AccountServiceTests()
        {
            _repositoryMock = new Mock<IAccountRepository>();
            _identityServiceMock = new Mock<IIdentityService>();
            _tokenServiceMock = new Mock<ITokenService>();
            _phoneNumberServiceMock = new Mock<IPhoneNumberService>();
            _httpContextAccessorMock = HttpContextAccessorTestHelper.WithNoHttpContext();

            SetUpHappyPathRepositoryDefaults();
        }

        private void SetUpHappyPathRepositoryDefaults()
        {
            _repositoryMock.Setup(x => x.EmailUserExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
            _repositoryMock.Setup(x => x.PhoneExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
            _tokenServiceMock.Setup(x => x.GenerateAccessToken(It.IsAny<TokenClaimsData>())).Returns("fake-jwt-token");
            _sendLimiterMock.Setup(x => x.TryAcquireAsync(It.IsAny<VerificationChannel>(), It.IsAny<string>())).ReturnsAsync(true);
            _identityServiceMock.Setup(x => x.HasPasswordAsync(It.IsAny<User>())).ReturnsAsync(true);
            _googleValidatorMock.Setup(x => x.IsConfigured).Returns(true);

            string defaultFormatted = "+5511987654312";
            _phoneNumberServiceMock
                .Setup(x => x.TryValidateAndFormat(It.IsAny<string>(), It.IsAny<string>(), out defaultFormatted))
                .Returns(true);
        }

        private AccountService CreateService() => new(_repositoryMock.Object, _identityServiceMock.Object, _tokenServiceMock.Object, _phoneNumberServiceMock.Object, _refreshTokenServiceMock.Object,
            _registrationCodeStoreMock.Object, _sendLimiterMock.Object, _codeSenderMock.Object, _googleValidatorMock.Object);

        private static User CreateUserEntity(string id = "user-1", string? email = null, string? phoneNumber = null) => new()
        {
            Id = id,
            Email = email,
            PhoneNumber = phoneNumber,
            SecurityStamp = "stamp-1"
        };

        // ── SendRegistrationCodeAsync ────────────────────────────────────────

        private const string E164 = "+5511987654312";
        private const string Email = "fulano@exemplo.com";

        private void SetUpPhoneAsInvalid()
        {
            string? formatted = null;
            _phoneNumberServiceMock
                .Setup(x => x.TryValidateAndFormat(It.IsAny<string>(), It.IsAny<string>(), out formatted))
                .Returns(false);
        }

        [Theory]
        [InlineData(true, true)]
        [InlineData(false, false)]
        public async Task SendRegistrationCodeAsync_WhenBothOrNeitherIsInformed_ShouldThrowBadRequest(bool phone, bool email)
        {
            var dto = new RegistrationCodeRequestDto { PhoneNumber = phone ? "11987654312" : null, Email = email ? Email : null };

            Func<Task> act = () => CreateService().SendRegistrationCodeAsync(dto);

            await act.Should().ThrowAsync<BadRequestException>();
            _codeSenderMock.Verify(x => x.SendAsync(It.IsAny<VerificationCodeMessage>()), Times.Never);
        }

        [Fact]
        public async Task SendRegistrationCodeAsync_WhenPhoneIsNew_ShouldIssueCodeAndSendSms()
        {
            _registrationCodeStoreMock.Setup(x => x.IssueAsync(E164, VerificationPurpose.Registration)).ReturnsAsync("654321");

            await CreateService().SendRegistrationCodeAsync(new RegistrationCodeRequestDto { PhoneNumber = "11987654312" });

            _codeSenderMock.Verify(x => x.SendAsync(new VerificationCodeMessage(VerificationChannel.Sms, E164, "654321", VerificationPurpose.Registration)), Times.Once);
        }

        [Fact]
        public async Task SendRegistrationCodeAsync_WhenEmailIsNew_ShouldIssueCodeForNormalizedEmailAndSendEmail()
        {
            _registrationCodeStoreMock.Setup(x => x.IssueAsync(Email, VerificationPurpose.Registration)).ReturnsAsync("654321");

            await CreateService().SendRegistrationCodeAsync(new RegistrationCodeRequestDto { Email = "  Fulano@Exemplo.com " });

            _codeSenderMock.Verify(x => x.SendAsync(new VerificationCodeMessage(VerificationChannel.Email, Email, "654321", VerificationPurpose.Registration)), Times.Once);
        }

        [Fact]
        public async Task SendRegistrationCodeAsync_WhenPhoneIsInvalid_ShouldNotSendNorThrow()
        {
            SetUpPhoneAsInvalid();

            Func<Task> act = () => CreateService().SendRegistrationCodeAsync(new RegistrationCodeRequestDto { PhoneNumber = "123" });

            await act.Should().NotThrowAsync();
            _registrationCodeStoreMock.Verify(x => x.IssueAsync(It.IsAny<string>(), It.IsAny<VerificationPurpose>()), Times.Never);
            _codeSenderMock.Verify(x => x.SendAsync(It.IsAny<VerificationCodeMessage>()), Times.Never);
        }

        [Fact]
        public async Task SendRegistrationCodeAsync_WhenEmailIsInvalid_ShouldNotSendNorThrow()
        {
            Func<Task> act = () => CreateService().SendRegistrationCodeAsync(new RegistrationCodeRequestDto { Email = "nao-e-email" });

            await act.Should().NotThrowAsync();
            _codeSenderMock.Verify(x => x.SendAsync(It.IsAny<VerificationCodeMessage>()), Times.Never);
        }

        [Fact]
        public async Task SendRegistrationCodeAsync_WhenPhoneAlreadyExists_ShouldNotSendNorConsumeLimitNorThrow()
        {
            _repositoryMock.Setup(x => x.PhoneExistsAsync(E164)).ReturnsAsync(true);

            Func<Task> act = () => CreateService().SendRegistrationCodeAsync(new RegistrationCodeRequestDto { PhoneNumber = "11987654312" });

            await act.Should().NotThrowAsync();
            _sendLimiterMock.Verify(x => x.TryAcquireAsync(It.IsAny<VerificationChannel>(), It.IsAny<string>()), Times.Never);
            _codeSenderMock.Verify(x => x.SendAsync(It.IsAny<VerificationCodeMessage>()), Times.Never);
        }

        [Fact]
        public async Task SendRegistrationCodeAsync_WhenEmailExistsAsEmailUser_ShouldNotSend()
        {
            _repositoryMock.Setup(x => x.EmailUserExistsAsync(Email)).ReturnsAsync(true);

            await CreateService().SendRegistrationCodeAsync(new RegistrationCodeRequestDto { Email = Email });

            _codeSenderMock.Verify(x => x.SendAsync(It.IsAny<VerificationCodeMessage>()), Times.Never);
        }

        [Fact]
        public async Task SendRegistrationCodeAsync_WhenEmailIsAlreadyAUserName_ShouldNotSend()
        {
            _identityServiceMock.Setup(x => x.FindByNameAsync(Email)).ReturnsAsync(new User { UserName = Email });

            await CreateService().SendRegistrationCodeAsync(new RegistrationCodeRequestDto { Email = Email });

            _codeSenderMock.Verify(x => x.SendAsync(It.IsAny<VerificationCodeMessage>()), Times.Never);
        }

        [Fact]
        public async Task SendRegistrationCodeAsync_WhenLimitIsReached_ShouldNotIssueNorSendAndNotThrow()
        {
            _sendLimiterMock.Setup(x => x.TryAcquireAsync(It.IsAny<VerificationChannel>(), E164)).ReturnsAsync(false);

            Func<Task> act = () => CreateService().SendRegistrationCodeAsync(new RegistrationCodeRequestDto { PhoneNumber = "11987654312" });

            await act.Should().NotThrowAsync();
            _registrationCodeStoreMock.Verify(x => x.IssueAsync(It.IsAny<string>(), It.IsAny<VerificationPurpose>()), Times.Never);
            _codeSenderMock.Verify(x => x.SendAsync(It.IsAny<VerificationCodeMessage>()), Times.Never);
        }

        // ── RegisterAsync ────────────────────────────────────────────────────

        [Fact]
        public async Task RegisterAsync_WhenNoCredentialsProvided_ShouldReturnError()
        {
            var dto = RegisterAccountRequestBuilder.New().Build();

            var errors = await CreateService().RegisterAsync(dto);

            errors.Should().ContainSingle(e => e.Code == "NoCredentials");
        }

        [Fact]
        public async Task RegisterAsync_WhenPhoneAndEmailAreProvided_ShouldReturnErrorWithoutVerifyingCode()
        {
            var dto = RegisterAccountRequestBuilder.New().WithPhoneNumber("5511987654312").WithEmail(Email).Build();

            var errors = await CreateService().RegisterAsync(dto);

            errors.Should().ContainSingle(e => e.Code == "NoCredentials");
            _registrationCodeStoreMock.Verify(x => x.VerifyAsync(It.IsAny<string>(), It.IsAny<VerificationPurpose>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task RegisterAsync_WhenBrazilianPhoneIsInvalid_ShouldReturnError()
        {
            var dto = RegisterAccountRequestBuilder.New().WithPhoneNumber("5511123").Build();
            SetUpPhoneAsInvalid();

            var errors = await CreateService().RegisterAsync(dto);

            errors.Should().Contain(e => e.Code == "InvalidPhone");
        }

        [Fact]
        public async Task RegisterAsync_WhenEmailIsInvalid_ShouldReturnError()
        {
            var dto = RegisterAccountRequestBuilder.New().WithEmail("nao-e-email").Build();

            var errors = await CreateService().RegisterAsync(dto);

            errors.Should().Contain(e => e.Code == "InvalidEmail");
        }

        [Fact]
        public async Task RegisterAsync_WhenCodeIsValidAndPhoneAlreadyExists_ShouldReturnError()
        {
            var dto = RegisterAccountRequestBuilder.New().WithPhoneNumber("5511987654312").Build();
            _registrationCodeStoreMock.Setup(x => x.VerifyAsync(It.IsAny<string>(), It.IsAny<VerificationPurpose>(), It.IsAny<string>())).ReturnsAsync(true);
            _repositoryMock.Setup(x => x.PhoneExistsAsync(It.IsAny<string>())).ReturnsAsync(true);

            var errors = await CreateService().RegisterAsync(dto);

            errors.Should().Contain(e => e.Code == "DuplicatePhoneNumber");
        }

        [Fact]
        public async Task RegisterAsync_WhenCodeIsValidAndEmailAlreadyExists_ShouldReturnError()
        {
            var dto = RegisterAccountRequestBuilder.New().WithEmail(Email).Build();
            _registrationCodeStoreMock.Setup(x => x.VerifyAsync(It.IsAny<string>(), It.IsAny<VerificationPurpose>(), It.IsAny<string>())).ReturnsAsync(true);
            _repositoryMock.Setup(x => x.EmailUserExistsAsync(Email)).ReturnsAsync(true);

            var errors = await CreateService().RegisterAsync(dto);

            errors.Should().Contain(e => e.Code == "DuplicateEmail");
        }

        [Fact]
        public async Task RegisterAsync_WhenCodeIsInvalidAndEmailAlreadyExists_ShouldNotRevealTheAccount()
        {
            var dto = RegisterAccountRequestBuilder.New().WithEmail(Email).Build();
            _registrationCodeStoreMock.Setup(x => x.VerifyAsync(It.IsAny<string>(), It.IsAny<VerificationPurpose>(), It.IsAny<string>())).ReturnsAsync(false);
            _repositoryMock.Setup(x => x.EmailUserExistsAsync(Email)).ReturnsAsync(true);

            Func<Task> act = () => CreateService().RegisterAsync(dto);

            await act.Should().ThrowAsync<BadRequestException>().WithMessage("Código inválido ou expirado.");
            _repositoryMock.Verify(x => x.EmailUserExistsAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task RegisterAsync_WhenPasswordsDoNotMatch_ShouldReturnErrorWithoutVerifyingCode()
        {
            var dto = RegisterAccountRequestBuilder.New()
                .WithPhoneNumber("5511987654312")
                .WithPassword("Senha123")
                .WithPasswordConfirm("Diferente123")
                .Build();

            var errors = await CreateService().RegisterAsync(dto);

            errors.Should().Contain(e => e.Code == "PasswordMismatch");
            _registrationCodeStoreMock.Verify(x => x.VerifyAsync(It.IsAny<string>(), It.IsAny<VerificationPurpose>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task RegisterAsync_WhenPhoneCodeIsWrong_ShouldThrowBadRequestAndNotCreateUser()
        {
            var dto = RegisterAccountRequestBuilder.New().WithPhoneNumber("5511987654312").WithCode("000000").Build();
            _registrationCodeStoreMock.Setup(x => x.VerifyAsync(E164, VerificationPurpose.Registration, "000000")).ReturnsAsync(false);

            Func<Task> act = () => CreateService().RegisterAsync(dto);

            (await act.Should().ThrowAsync<BadRequestException>()).WithMessage("Código inválido ou expirado.");
            _identityServiceMock.Verify(x => x.RegisterVerifiedUser(It.IsAny<CreateAccountRequestDto>(), It.IsAny<string>(), It.IsAny<VerificationChannel>()), Times.Never);
        }

        [Fact]
        public async Task RegisterAsync_WhenEmailCodeIsWrong_ShouldThrowBadRequestAndNotCreateUser()
        {
            var dto = RegisterAccountRequestBuilder.New().WithEmail(Email).WithCode("000000").Build();
            _registrationCodeStoreMock.Setup(x => x.VerifyAsync(Email, VerificationPurpose.Registration, "000000")).ReturnsAsync(false);

            Func<Task> act = () => CreateService().RegisterAsync(dto);

            (await act.Should().ThrowAsync<BadRequestException>()).WithMessage("Código inválido ou expirado.");
            _identityServiceMock.Verify(x => x.RegisterVerifiedUser(It.IsAny<CreateAccountRequestDto>(), It.IsAny<string>(), It.IsAny<VerificationChannel>()), Times.Never);
        }

        [Fact]
        public async Task RegisterAsync_WhenValidPhoneRegistration_ShouldRegisterVerifiedUserWithSmsAndRemoveCode()
        {
            var dto = RegisterAccountRequestBuilder.New().WithPhoneNumber("5511987654312").Build();
            _registrationCodeStoreMock.Setup(x => x.VerifyAsync(E164, VerificationPurpose.Registration, dto.Code)).ReturnsAsync(true);
            _identityServiceMock
                .Setup(x => x.RegisterVerifiedUser(It.IsAny<CreateAccountRequestDto>(), E164, VerificationChannel.Sms))
                .ReturnsAsync(Enumerable.Empty<IdentityError>());

            var errors = await CreateService().RegisterAsync(dto);

            errors.Should().BeEmpty();
            _identityServiceMock.Verify(x => x.RegisterVerifiedUser(
                It.Is<CreateAccountRequestDto>(a => a.FirstName == dto.FirstName && a.LastName == dto.LastName && a.Password == dto.Password && a.Age == dto.Age),
                E164, VerificationChannel.Sms), Times.Once);
            _registrationCodeStoreMock.Verify(x => x.RemoveAsync(E164, VerificationPurpose.Registration), Times.Once);
        }

        [Fact]
        public async Task RegisterAsync_WhenValidEmailRegistration_ShouldRegisterVerifiedUserWithNormalizedEmailAndRemoveCode()
        {
            var dto = RegisterAccountRequestBuilder.New().WithEmail(" Fulano@Exemplo.com ").Build();
            _registrationCodeStoreMock.Setup(x => x.VerifyAsync(Email, VerificationPurpose.Registration, dto.Code)).ReturnsAsync(true);
            _identityServiceMock
                .Setup(x => x.RegisterVerifiedUser(It.IsAny<CreateAccountRequestDto>(), Email, VerificationChannel.Email))
                .ReturnsAsync(Enumerable.Empty<IdentityError>());

            var errors = await CreateService().RegisterAsync(dto);

            errors.Should().BeEmpty();
            _identityServiceMock.Verify(x => x.RegisterVerifiedUser(It.IsAny<CreateAccountRequestDto>(), Email, VerificationChannel.Email), Times.Once);
            _registrationCodeStoreMock.Verify(x => x.RemoveAsync(Email, VerificationPurpose.Registration), Times.Once);
        }

        [Fact]
        public async Task RegisterAsync_WhenIdentityRejectsPassword_ShouldReturnErrorsAndKeepTheCode()
        {
            var dto = RegisterAccountRequestBuilder.New().WithEmail(Email).Build();
            _registrationCodeStoreMock.Setup(x => x.VerifyAsync(Email, VerificationPurpose.Registration, dto.Code)).ReturnsAsync(true);
            _identityServiceMock
                .Setup(x => x.RegisterVerifiedUser(It.IsAny<CreateAccountRequestDto>(), Email, VerificationChannel.Email))
                .ReturnsAsync(new[] { new IdentityError { Code = "PasswordTooShort", Description = "Senha curta." } });

            var errors = await CreateService().RegisterAsync(dto);

            errors.Should().ContainSingle(e => e.Code == "PasswordTooShort");
            _registrationCodeStoreMock.Verify(x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<VerificationPurpose>()), Times.Never);
        }

        // ── LoginAsync ───────────────────────────────────────────────────────

        [Fact]
        public async Task LoginAsync_WhenNoCredentialsProvided_ShouldThrowBadRequestException()
        {
            var dto = LoginRequestBuilder.New().WithEmail(null).WithPhone(null).Build();

            Func<Task> act = () => CreateService().LoginAsync(dto, AuthClient.Web);

            await act.Should().ThrowAsync<BadRequestException>();
        }

        [Fact]
        public async Task LoginAsync_WhenBothEmailAndPhoneProvided_ShouldThrowBadRequestException()
        {
            var dto = LoginRequestBuilder.New().WithEmail("a@b.com").WithPhone("5511987654321").Build();

            Func<Task> act = () => CreateService().LoginAsync(dto, AuthClient.Web);

            await act.Should().ThrowAsync<BadRequestException>();
        }

        [Fact]
        public async Task LoginAsync_WhenUserNotFound_ShouldThrowUnauthorizedWithGenericMessage()
        {
            var dto = LoginRequestBuilder.New().WithPhone(null).Build();
            _identityServiceMock.Setup(x => x.FindUserByLoginAsync(dto)).ReturnsAsync((User?)null);

            Func<Task> act = () => CreateService().LoginAsync(dto, AuthClient.Web);

            var ex = (await act.Should().ThrowAsync<UnauthorizedAccessException>()).Which;
            ex.Message.Should().Be("Usuário ou senha inválidos.");
            ex.Message.Should().NotContain(dto.Email!);
        }

        [Fact]
        public async Task LoginAsync_WhenUserNotFoundByPhone_ShouldNotLeakPhoneInMessage()
        {
            var dto = LoginRequestBuilder.New().WithEmail(null).WithPhone("11987654321").Build();
            _identityServiceMock.Setup(x => x.FindUserByLoginAsync(dto)).ReturnsAsync((User?)null);

            Func<Task> act = () => CreateService().LoginAsync(dto, AuthClient.Web);

            var ex = (await act.Should().ThrowAsync<UnauthorizedAccessException>()).Which;
            ex.Message.Should().Be("Usuário ou senha inválidos.");
            ex.Message.Should().NotContain(dto.Phone!);
        }

        [Fact]
        public async Task LoginAsync_WhenPasswordIsInvalid_ShouldThrowUnauthorizedWithSameGenericMessage()
        {
            var dto = LoginRequestBuilder.New().WithPhone(null).Build();
            var user = CreateUserEntity(email: dto.Email);
            _identityServiceMock.Setup(x => x.FindUserByLoginAsync(dto)).ReturnsAsync(user);
            _identityServiceMock.Setup(x => x.CheckPasswordAsync(user, dto.Password)).ReturnsAsync(false);

            Func<Task> act = () => CreateService().LoginAsync(dto, AuthClient.Web);

            var ex = (await act.Should().ThrowAsync<UnauthorizedAccessException>()).Which;
            ex.Message.Should().Be("Usuário ou senha inválidos.");
            ex.Message.Should().NotContain(dto.Email!);
        }

        [Fact]
        public async Task LoginAsync_WhenAccountIsLockedOut_ShouldThrowGenericUnauthorizedWithoutCheckingPassword()
        {
            var dto = LoginRequestBuilder.New().WithPhone(null).Build();
            var user = CreateUserEntity(email: dto.Email);
            _identityServiceMock.Setup(x => x.FindUserByLoginAsync(dto)).ReturnsAsync(user);
            _identityServiceMock.Setup(x => x.IsLockedOutAsync(user)).ReturnsAsync(true);

            Func<Task> act = () => CreateService().LoginAsync(dto, AuthClient.Web);

            var ex = (await act.Should().ThrowAsync<UnauthorizedAccessException>()).Which;
            ex.Message.Should().Be("Usuário ou senha inválidos.");
            _identityServiceMock.Verify(x => x.CheckPasswordAsync(It.IsAny<User>(), It.IsAny<string>()), Times.Never);
            _identityServiceMock.Verify(x => x.AccessFailedAsync(It.IsAny<User>()), Times.Never);
        }

        [Fact]
        public async Task LoginAsync_WhenPasswordIsWrong_ShouldRegisterAccessFailure()
        {
            var dto = LoginRequestBuilder.New().WithPhone(null).Build();
            var user = CreateUserEntity(email: dto.Email);
            _identityServiceMock.Setup(x => x.FindUserByLoginAsync(dto)).ReturnsAsync(user);
            _identityServiceMock.Setup(x => x.IsLockedOutAsync(user)).ReturnsAsync(false);
            _identityServiceMock.Setup(x => x.CheckPasswordAsync(user, dto.Password)).ReturnsAsync(false);

            Func<Task> act = () => CreateService().LoginAsync(dto, AuthClient.Web);

            await act.Should().ThrowAsync<UnauthorizedAccessException>();
            _identityServiceMock.Verify(x => x.AccessFailedAsync(user), Times.Once);
            _identityServiceMock.Verify(x => x.ResetAccessFailedCountAsync(It.IsAny<User>()), Times.Never);
        }

        [Fact]
        public async Task LoginAsync_WhenPasswordIsCorrect_ShouldResetAccessFailedCount()
        {
            var dto = LoginRequestBuilder.New().WithPhone(null).Build();
            var user = CreateUserEntity(email: dto.Email);
            _identityServiceMock.Setup(x => x.FindUserByLoginAsync(dto)).ReturnsAsync(user);
            _identityServiceMock.Setup(x => x.IsLockedOutAsync(user)).ReturnsAsync(false);
            _identityServiceMock.Setup(x => x.CheckPasswordAsync(user, dto.Password)).ReturnsAsync(true);
            _identityServiceMock.Setup(x => x.GetRolesAsync(user)).ReturnsAsync((true, false));
            _identityServiceMock.Setup(x => x.GetUserClaimsAsync(user)).ReturnsAsync(new List<Claim>());
            _identityServiceMock.Setup(x => x.GetRoleClaimsAsync("Administrator")).ReturnsAsync(new List<Claim>());
            _refreshTokenServiceMock.Setup(x => x.CreateSessionAsync(user.Id, AuthClient.Web)).ReturnsAsync("refresh");

            await CreateService().LoginAsync(dto, AuthClient.Web);

            _identityServiceMock.Verify(x => x.ResetAccessFailedCountAsync(user), Times.Once);
            _identityServiceMock.Verify(x => x.AccessFailedAsync(It.IsAny<User>()), Times.Never);
        }

        [Fact]
        public async Task LoginAsync_WhenUserHasNoValidRole_ShouldThrowSecurityException()
        {
            var dto = LoginRequestBuilder.New().WithPhone(null).Build();
            var user = CreateUserEntity(email: dto.Email);
            _identityServiceMock.Setup(x => x.FindUserByLoginAsync(dto)).ReturnsAsync(user);
            _identityServiceMock.Setup(x => x.CheckPasswordAsync(user, dto.Password)).ReturnsAsync(true);
            _identityServiceMock.Setup(x => x.GetRolesAsync(user)).ReturnsAsync((false, false));

            Func<Task> act = () => CreateService().LoginAsync(dto, AuthClient.Web);

            await act.Should().ThrowAsync<SecurityException>();
        }

        [Theory]
        [InlineData(AuthClient.Web)]
        [InlineData(AuthClient.App)]
        public async Task LoginAsync_WhenCredentialsAreValid_ShouldReturnAuthResponseWithGeneratedToken(AuthClient client)
        {
            var dto = LoginRequestBuilder.New().WithPhone(null).Build();
            var user = CreateUserEntity(id: "user-1", email: dto.Email);
            _identityServiceMock.Setup(x => x.FindUserByLoginAsync(dto)).ReturnsAsync(user);
            _identityServiceMock.Setup(x => x.CheckPasswordAsync(user, dto.Password)).ReturnsAsync(true);
            _identityServiceMock.Setup(x => x.GetRolesAsync(user)).ReturnsAsync((true, false));
            _identityServiceMock.Setup(x => x.GetUserClaimsAsync(user)).ReturnsAsync(new List<Claim>());
            _identityServiceMock.Setup(x => x.GetRoleClaimsAsync("Administrator")).ReturnsAsync(new List<Claim>());
            _refreshTokenServiceMock.Setup(x => x.CreateSessionAsync(user.Id, client)).ReturnsAsync("refresh-token");
            _tokenServiceMock.Setup(x => x.GenerateAccessToken(It.Is<TokenClaimsData>(d => d.UserId == "user-1" && d.IsAdmin)))
                .Returns("access-token");

            var result = await CreateService().LoginAsync(dto, client);

            result.UserId.Should().Be("user-1");
            result.Token.Should().Be("access-token");
            result.RefreshToken.Should().Be("refresh-token");
            _refreshTokenServiceMock.Verify(x => x.CreateSessionAsync(user.Id, client), Times.Once);
            _identityServiceMock.Verify(x => x.UpdateLastLoginAsync(user), Times.Once);
        }

        [Fact]
        public async Task LoginAsync_WhenUserRegisteredByEmail_ShouldUseEmailAsTokenIdentifier()
        {
            var dto = LoginRequestBuilder.New().WithPhone(null).WithEmail("fulano@exemplo.com").Build();
            var user = CreateUserEntity(id: "user-2", email: "fulano@exemplo.com", phoneNumber: null);
            _identityServiceMock.Setup(x => x.FindUserByLoginAsync(dto)).ReturnsAsync(user);
            _identityServiceMock.Setup(x => x.CheckPasswordAsync(user, dto.Password)).ReturnsAsync(true);
            _identityServiceMock.Setup(x => x.GetRolesAsync(user)).ReturnsAsync((false, true));
            _identityServiceMock.Setup(x => x.GetUserClaimsAsync(user)).ReturnsAsync(new List<Claim>());
            _identityServiceMock.Setup(x => x.GetRoleClaimsAsync("User")).ReturnsAsync(new List<Claim>());

            await CreateService().LoginAsync(dto, AuthClient.Web);

            _tokenServiceMock.Verify(x => x.GenerateAccessToken(It.Is<TokenClaimsData>(d => d.Identifier == "fulano@exemplo.com" && d.IsUser && !d.IsAdmin)), Times.Once);
        }

        [Fact]
        public async Task LoginAsync_WhenAccountHasNoPassword_ShouldThrowGenericUnauthorizedWithoutCountingLockout()
        {
            var dto = LoginRequestBuilder.New().WithPhone(null).Build();
            var user = CreateUserEntity(email: dto.Email);
            _identityServiceMock.Setup(x => x.FindUserByLoginAsync(dto)).ReturnsAsync(user);
            _identityServiceMock.Setup(x => x.HasPasswordAsync(user)).ReturnsAsync(false);

            Func<Task> act = () => CreateService().LoginAsync(dto, AuthClient.App);

            (await act.Should().ThrowAsync<UnauthorizedAccessException>()).Which.Message.Should().Be("Usuário ou senha inválidos.");
            _identityServiceMock.Verify(x => x.AccessFailedAsync(It.IsAny<User>()), Times.Never);
            _identityServiceMock.Verify(x => x.CheckPasswordAsync(It.IsAny<User>(), It.IsAny<string>()), Times.Never);
        }

        // ── LoginWithGoogleAsync ─────────────────────────────────────────────

        private const string GoogleSub = "google-sub-1";
        private const string GoogleEmail = "fulana@gmail.com";

        private static GoogleIdentity Google(string? email = GoogleEmail, bool verified = true, string? given = "Fulana", string? family = "de Tal", string? name = "Fulana de Tal") =>
            new(GoogleSub, email, verified, given, family, name);

        private void SetUpGoogleToken(GoogleIdentity? identity) =>
            _googleValidatorMock.Setup(x => x.ValidateAsync("id-token")).ReturnsAsync(identity);

        private void SetUpRoles(User user, bool isAdmin, bool isUser)
        {
            _identityServiceMock.Setup(x => x.GetRolesAsync(user)).ReturnsAsync((isAdmin, isUser));
            _identityServiceMock.Setup(x => x.GetUserClaimsAsync(user)).ReturnsAsync(new List<Claim>());
            _identityServiceMock.Setup(x => x.GetRoleClaimsAsync(It.IsAny<string>())).ReturnsAsync(new List<Claim>());
        }

        private Task<AuthResponseDto> GoogleLogin(string? idToken = "id-token") =>
            CreateService().LoginWithGoogleAsync(new GoogleLoginRequestDto { IdToken = idToken });

        [Theory]
        [InlineData(null)]
        [InlineData("  ")]
        public async Task LoginWithGoogleAsync_WhenIdTokenIsMissing_ShouldThrowBadRequest(string? idToken)
        {
            Func<Task> act = () => GoogleLogin(idToken);

            await act.Should().ThrowAsync<BadRequestException>();
            _googleValidatorMock.Verify(x => x.ValidateAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task LoginWithGoogleAsync_WhenNotConfigured_ShouldThrowServiceUnavailable()
        {
            _googleValidatorMock.Setup(x => x.IsConfigured).Returns(false);

            Func<Task> act = () => GoogleLogin();

            (await act.Should().ThrowAsync<ServiceUnavailableException>()).Which.Message.Should().Be("Login com Google não configurado.");
        }

        [Fact]
        public async Task LoginWithGoogleAsync_WhenTokenIsInvalid_ShouldThrowUnauthorized()
        {
            SetUpGoogleToken(null);

            Func<Task> act = () => GoogleLogin();

            (await act.Should().ThrowAsync<UnauthorizedAccessException>()).Which.Message.Should().Be("Login com Google inválido.");
        }

        [Theory]
        [InlineData(GoogleEmail, false)]
        [InlineData(null, true)]
        [InlineData("sem-arroba", true)]
        public async Task LoginWithGoogleAsync_WhenEmailIsMissingOrNotVerified_ShouldThrowUnauthorized(string? email, bool verified)
        {
            SetUpGoogleToken(Google(email: email, verified: verified));

            Func<Task> act = () => GoogleLogin();

            await act.Should().ThrowAsync<UnauthorizedAccessException>();
            _identityServiceMock.Verify(x => x.RegisterExternalUserAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task LoginWithGoogleAsync_WhenAlreadyLinked_ShouldOpenAppSessionWithoutLinkingAgain()
        {
            SetUpGoogleToken(Google());
            var user = CreateUserEntity(email: GoogleEmail);
            _identityServiceMock.Setup(x => x.FindByExternalLoginAsync("Google", GoogleSub)).ReturnsAsync(user);
            SetUpRoles(user, isAdmin: false, isUser: true);
            _refreshTokenServiceMock.Setup(x => x.CreateSessionAsync("user-1", AuthClient.App)).ReturnsAsync("refresh-app");

            var result = await GoogleLogin();

            result.UserId.Should().Be("user-1");
            result.Token.Should().Be("fake-jwt-token");
            result.RefreshToken.Should().Be("refresh-app");
            _identityServiceMock.Verify(x => x.LinkExternalLoginAsync(It.IsAny<User>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            _identityServiceMock.Verify(x => x.UpdateLastLoginAsync(user), Times.Once);
        }

        [Fact]
        public async Task LoginWithGoogleAsync_WhenEmailHasAccount_ShouldLinkAutomatically()
        {
            SetUpGoogleToken(Google(email: "Fulana@Gmail.com"));
            var user = CreateUserEntity(phoneNumber: E164);
            _identityServiceMock.Setup(x => x.FindByEmailAsync(GoogleEmail)).ReturnsAsync(user);
            SetUpRoles(user, isAdmin: false, isUser: true);

            await GoogleLogin();

            _identityServiceMock.Verify(x => x.LinkExternalLoginAsync(user, "Google", GoogleSub), Times.Once);
            _identityServiceMock.Verify(x => x.RegisterExternalUserAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task LoginWithGoogleAsync_WhenEmailBelongsToAdministrator_ShouldThrowForbiddenWithoutLinking()
        {
            SetUpGoogleToken(Google());
            var admin = CreateUserEntity(email: GoogleEmail);
            _identityServiceMock.Setup(x => x.FindByEmailAsync(GoogleEmail)).ReturnsAsync(admin);
            SetUpRoles(admin, isAdmin: true, isUser: false);

            Func<Task> act = () => GoogleLogin();

            (await act.Should().ThrowAsync<SecurityException>()).Which.Message.Should().Be("Administrador entra com e-mail e senha.");
            _identityServiceMock.Verify(x => x.LinkExternalLoginAsync(It.IsAny<User>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            _refreshTokenServiceMock.Verify(x => x.CreateSessionAsync(It.IsAny<string>(), It.IsAny<AuthClient>()), Times.Never);
        }

        [Fact]
        public async Task LoginWithGoogleAsync_WhenLinkedAccountBecameAdministrator_ShouldThrowForbidden()
        {
            SetUpGoogleToken(Google());
            var user = CreateUserEntity(email: GoogleEmail);
            _identityServiceMock.Setup(x => x.FindByExternalLoginAsync("Google", GoogleSub)).ReturnsAsync(user);
            SetUpRoles(user, isAdmin: true, isUser: false);

            Func<Task> act = () => GoogleLogin();

            await act.Should().ThrowAsync<SecurityException>();
        }

        [Fact]
        public async Task LoginWithGoogleAsync_WhenNoAccount_ShouldCreateUserWithGoogleNames()
        {
            SetUpGoogleToken(Google());
            var created = CreateUserEntity(id: "novo", email: GoogleEmail);
            _identityServiceMock.Setup(x => x.RegisterExternalUserAsync(GoogleEmail, "Fulana", "de Tal", "Google", GoogleSub)).ReturnsAsync(created);
            SetUpRoles(created, isAdmin: false, isUser: true);

            var result = await GoogleLogin();

            result.UserId.Should().Be("novo");
            _refreshTokenServiceMock.Verify(x => x.CreateSessionAsync("novo", AuthClient.App), Times.Once);
        }

        [Theory]
        [InlineData(null, null, "Maria Clara Souza", "Maria", "Clara Souza")]
        [InlineData(null, null, null, "fulana", "")]
        [InlineData("Ana", null, "Ana", "Ana", "")]
        public async Task LoginWithGoogleAsync_WhenNamesAreMissing_ShouldFallBackToNameAndEmail(string? given, string? family, string? name, string expectedFirst, string expectedLast)
        {
            SetUpGoogleToken(Google(given: given, family: family, name: name));
            var created = CreateUserEntity(id: "novo", email: GoogleEmail);
            _identityServiceMock.Setup(x => x.RegisterExternalUserAsync(GoogleEmail, expectedFirst, expectedLast, "Google", GoogleSub)).ReturnsAsync(created);
            SetUpRoles(created, isAdmin: false, isUser: true);

            var result = await GoogleLogin();

            result.UserId.Should().Be("novo");
        }

        [Fact]
        public async Task LoginWithGoogleAsync_WhenNamesAreLong_ShouldTruncateTo30And70()
        {
            SetUpGoogleToken(Google(given: new string('a', 40), family: new string('b', 90)));
            var created = CreateUserEntity(id: "novo", email: GoogleEmail);
            _identityServiceMock.Setup(x => x.RegisterExternalUserAsync(GoogleEmail, new string('a', 30), new string('b', 70), "Google", GoogleSub)).ReturnsAsync(created);
            SetUpRoles(created, isAdmin: false, isUser: true);

            var result = await GoogleLogin();

            result.UserId.Should().Be("novo");
        }

        [Fact]
        public async Task LoginWithGoogleAsync_WhenConcurrentFirstLoginCreatedAccount_ShouldUseIt()
        {
            SetUpGoogleToken(Google());
            var other = CreateUserEntity(id: "do-outro-request", email: GoogleEmail);
            _identityServiceMock.SetupSequence(x => x.FindByExternalLoginAsync("Google", GoogleSub))
                .ReturnsAsync((User?)null)
                .ReturnsAsync(other);
            _identityServiceMock.Setup(x => x.RegisterExternalUserAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new InvalidOperationException("DuplicateUserName"));
            SetUpRoles(other, isAdmin: false, isUser: true);

            var result = await GoogleLogin();

            result.UserId.Should().Be("do-outro-request");
        }

        [Fact]
        public async Task LoginWithGoogleAsync_WhenAccountIsLockedOut_ShouldStillLogInWithoutResettingLockout()
        {
            SetUpGoogleToken(Google());
            var user = CreateUserEntity(email: GoogleEmail);
            _identityServiceMock.Setup(x => x.FindByExternalLoginAsync("Google", GoogleSub)).ReturnsAsync(user);
            _identityServiceMock.Setup(x => x.IsLockedOutAsync(user)).ReturnsAsync(true);
            SetUpRoles(user, isAdmin: false, isUser: true);

            var result = await GoogleLogin();

            result.UserId.Should().Be("user-1");
            _identityServiceMock.Verify(x => x.ResetAccessFailedCountAsync(It.IsAny<User>()), Times.Never);
        }

        // ── RefreshTokenAsync ────────────────────────────────────────────────

        private void SetUpPrincipal(string token, string? uid)
        {
            var principal = uid is null
                ? new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", "x") }, "TestAuth"))
                : ClaimsPrincipalTestHelper.CreateUser(uid);
            _tokenServiceMock.Setup(x => x.GetPrincipalFromExpiredToken(token)).Returns(principal);
        }

        [Fact]
        public async Task RefreshTokenAsync_WhenPrincipalIsNull_ShouldThrowUnauthorizedAccessException()
        {
            var request = RefreshTokenRequestBuilder.New().WithToken("isto-nao-e-um-jwt").Build();
            _tokenServiceMock.Setup(x => x.GetPrincipalFromExpiredToken(request.Token)).Returns((ClaimsPrincipal?)null);

            Func<Task> act = () => CreateService().RefreshTokenAsync(request, AuthClient.Web);

            await act.Should().ThrowAsync<UnauthorizedAccessException>();
            _identityServiceMock.Verify(x => x.FindByIdAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task RefreshTokenAsync_WhenTokenIsEmpty_ShouldThrowUnauthorizedAccessException()
        {
            var request = RefreshTokenRequestBuilder.New().WithToken(string.Empty).Build();

            Func<Task> act = () => CreateService().RefreshTokenAsync(request, AuthClient.Web);

            await act.Should().ThrowAsync<UnauthorizedAccessException>();
            _identityServiceMock.Verify(x => x.FindByIdAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task RefreshTokenAsync_WhenTokenHasNoUidClaim_ShouldThrowUnauthorizedAccessException()
        {
            var request = RefreshTokenRequestBuilder.New().WithToken("token-sem-uid").Build();
            SetUpPrincipal(request.Token, uid: null);

            Func<Task> act = () => CreateService().RefreshTokenAsync(request, AuthClient.Web);

            await act.Should().ThrowAsync<UnauthorizedAccessException>();
            _identityServiceMock.Verify(x => x.FindByIdAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task RefreshTokenAsync_WhenUserNotFound_ShouldThrowUnauthorizedAccessException()
        {
            var request = RefreshTokenRequestBuilder.New().WithToken("token-valido").Build();
            SetUpPrincipal(request.Token, "user-1");
            _identityServiceMock.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync((User?)null);

            Func<Task> act = () => CreateService().RefreshTokenAsync(request, AuthClient.Web);

            await act.Should().ThrowAsync<UnauthorizedAccessException>();
        }

        [Theory]
        [InlineData(AuthClient.Web)]
        [InlineData(AuthClient.App)]
        public async Task RefreshTokenAsync_WhenAccountIsLockedOut_ShouldThrowUnauthorizedWithoutRevokingOrIssuing(AuthClient client)
        {
            var request = RefreshTokenRequestBuilder.New().WithToken("token-valido").Build();
            SetUpPrincipal(request.Token, "user-1");
            var user = CreateUserEntity(id: "user-1", email: "user@example.com");
            _identityServiceMock.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(user);
            _identityServiceMock.Setup(x => x.IsLockedOutAsync(user)).ReturnsAsync(true);

            Func<Task> act = () => CreateService().RefreshTokenAsync(request, client);

            await act.Should().ThrowAsync<UnauthorizedAccessException>();
            _refreshTokenServiceMock.Verify(x => x.RotateAsync(It.IsAny<string>(), It.IsAny<AuthClient>(), It.IsAny<string>()), Times.Never);
        }

        [Theory]
        [InlineData(AuthClient.Web)]
        [InlineData(AuthClient.App)]
        public async Task RefreshTokenAsync_WhenRotationFails_ShouldThrowUnauthorizedWithoutIssuingAccessToken(AuthClient client)
        {
            var request = RefreshTokenRequestBuilder.New().WithToken("token-valido").Build();
            SetUpPrincipal(request.Token, "user-1");
            var user = CreateUserEntity(id: "user-1", email: "user@example.com");
            _identityServiceMock.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(user);
            _refreshTokenServiceMock.Setup(x => x.RotateAsync("user-1", client, request.RefreshToken)).ReturnsAsync((string?)null);

            Func<Task> act = () => CreateService().RefreshTokenAsync(request, client);

            await act.Should().ThrowAsync<UnauthorizedAccessException>();
            _tokenServiceMock.Verify(x => x.GenerateAccessToken(It.IsAny<TokenClaimsData>()), Times.Never);
        }

        [Theory]
        [InlineData(AuthClient.Web)]
        [InlineData(AuthClient.App)]
        public async Task RefreshTokenAsync_WhenTokenIsValid_ShouldReturnNewTokensForSameClient(AuthClient client)
        {
            var request = RefreshTokenRequestBuilder.New().WithToken("token-valido").Build();
            SetUpPrincipal(request.Token, "user-1");
            var user = CreateUserEntity(id: "user-1", email: "user@example.com");
            _identityServiceMock.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(user);
            _refreshTokenServiceMock.Setup(x => x.RotateAsync("user-1", client, request.RefreshToken)).ReturnsAsync("new-refresh-token");
            _identityServiceMock.Setup(x => x.GetRolesAsync(user)).ReturnsAsync((true, false));
            _identityServiceMock.Setup(x => x.GetUserClaimsAsync(user)).ReturnsAsync(new List<Claim>());
            _identityServiceMock.Setup(x => x.GetRoleClaimsAsync("Administrator")).ReturnsAsync(new List<Claim>());
            _tokenServiceMock.Setup(x => x.GenerateAccessToken(It.IsAny<TokenClaimsData>())).Returns("new-access-token");

            var result = await CreateService().RefreshTokenAsync(request, client);

            result.UserId.Should().Be("user-1");
            result.Token.Should().Be("new-access-token");
            result.RefreshToken.Should().Be("new-refresh-token");
            _refreshTokenServiceMock.Verify(x => x.RotateAsync("user-1", client, request.RefreshToken), Times.Once);
        }

        // ── LogoutAsync ───────────────────────────────────

        [Fact]
        public async Task LogoutAsync_WithRefreshToken_ShouldRevokeOnlyThatSession()
        {
            await CreateService().LogoutAsync("refresh-do-aparelho");

            _refreshTokenServiceMock.Verify(x => x.RevokeSessionAsync("refresh-do-aparelho"), Times.Once);
            _refreshTokenServiceMock.Verify(x => x.RevokeAllSessionsAsync(It.IsAny<string>()), Times.Never);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task LogoutAsync_WithoutRefreshToken_ShouldSucceedWithoutRevoking(string? refreshToken)
        {
            await CreateService().LogoutAsync(refreshToken);

            _refreshTokenServiceMock.Verify(x => x.RevokeSessionAsync(It.IsAny<string>()), Times.Never);
        }
    }
}
