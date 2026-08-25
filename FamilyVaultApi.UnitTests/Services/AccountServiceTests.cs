using FamilyVaultApi.Data.Entities;
using FamilyVaultApi.Exceptions;
using FamilyVaultApi.Models.Dto.Requests.Account;
using FamilyVaultApi.Models.Dto.Responses.Account;
using FamilyVaultApi.Models.Internal;
using FamilyVaultApi.Repositories.IRepository;
using FamilyVaultApi.Services.IService;
using FamilyVaultApi.Services.Service;
using FamilyVaultApi.UnitTests.Builders.Account;
using FamilyVaultApi.UnitTests.Helpers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Moq;
using System.Security;
using System.Security.Claims;

namespace FamilyVaultApi.UnitTests.Services
{
    public class AccountServiceTests
    {
        private readonly Mock<IAccountRepository> _repositoryMock;
        private readonly Mock<IIdentityService> _identityServiceMock;
        private readonly Mock<ITokenService> _tokenServiceMock;
        private readonly Mock<IPhoneNumberService> _phoneNumberServiceMock;
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
            _identityServiceMock.Setup(x => x.AdministratorExistsAsync()).ReturnsAsync(false);
            _tokenServiceMock.Setup(x => x.GenerateAccessToken(It.IsAny<TokenClaimsData>())).Returns("fake-jwt-token");

            string defaultFormatted = "+5511987654312";
            _phoneNumberServiceMock
                .Setup(x => x.TryValidateAndFormat(It.IsAny<string>(), It.IsAny<string>(), out defaultFormatted))
                .Returns(true);
        }

        private AccountService CreateService() => new(_repositoryMock.Object, _identityServiceMock.Object, _httpContextAccessorMock.Object, _tokenServiceMock.Object, _phoneNumberServiceMock.Object);

        private static User CreateUserEntity(string id = "user-1", string? email = null, string? phoneNumber = null) => new()
        {
            Id = id,
            Email = email,
            PhoneNumber = phoneNumber,
            SecurityStamp = "stamp-1"
        };

        // ── RegisterAsync ────────────────────────────────────────────────────

        [Fact]
        public async Task RegisterAsync_WhenNoCredentialsProvided_ShouldReturnError()
        {
            var dto = CreateAccountRequestBuilder.New().WithEmail(null).WithPhoneNumber(null).Build();

            var errors = await CreateService().RegisterAsync(dto);

            errors.Should().ContainSingle(e => e.Code == "NoCredentials");
        }

        [Fact]
        public async Task RegisterAsync_WhenEmailIsInvalid_ShouldReturnError()
        {
            var dto = CreateAccountRequestBuilder.New().WithEmail("not-an-email").WithPhoneNumber(null).Build();

            var errors = await CreateService().RegisterAsync(dto);

            errors.Should().Contain(e => e.Code == "InvalidEmail");
        }

        [Fact]
        public async Task RegisterAsync_WhenEmailAlreadyExists_ShouldReturnError()
        {
            var dto = CreateAccountRequestBuilder.New().WithPhoneNumber(null).Build();
            _repositoryMock.Setup(x => x.EmailUserExistsAsync(dto.Email)).ReturnsAsync(true);

            var errors = await CreateService().RegisterAsync(dto);

            errors.Should().Contain(e => e.Code == "DuplicateEmail");
        }

        [Fact]
        public async Task RegisterAsync_WhenBrazilianPhoneIsInvalid_ShouldReturnError()
        {
            var dto = CreateAccountRequestBuilder.New().WithEmail(null).WithPhoneNumber("5511123").Build();
            string? formatted = null;
            _phoneNumberServiceMock
                .Setup(x => x.TryValidateAndFormat("5511123", It.IsAny<string>(), out formatted))
                .Returns(false);

            var errors = await CreateService().RegisterAsync(dto);

            errors.Should().Contain(e => e.Code == "InvalidPhone");
        }

        [Fact]
        public async Task RegisterAsync_WhenPhoneAlreadyExists_ShouldReturnError()
        {
            var dto = CreateAccountRequestBuilder.New().WithEmail(null).WithPhoneNumber("5511987654312").Build();
            _repositoryMock.Setup(x => x.PhoneExistsAsync(It.IsAny<string>())).ReturnsAsync(true);

            var errors = await CreateService().RegisterAsync(dto);

            errors.Should().Contain(e => e.Code == "DuplicatePhoneNumber");
        }

        [Fact]
        public async Task RegisterAsync_WhenPasswordsDoNotMatch_ShouldReturnError()
        {
            var dto = CreateAccountRequestBuilder.New()
                .WithEmail(null)
                .WithPhoneNumber("5511987654312")
                .WithPassword("Senha123")
                .WithPasswordConfirm("Diferente123")
                .Build();

            var errors = await CreateService().RegisterAsync(dto);

            errors.Should().Contain(e => e.Code == "PasswordMismatch");
        }

        [Fact]
        public async Task RegisterAsync_WhenFirstAdministrator_ShouldBootstrapWithoutAuthentication()
        {
            var dto = CreateAccountRequestBuilder.New().WithPhoneNumber(null).Build();
            _identityServiceMock.Setup(x => x.AdministratorExistsAsync()).ReturnsAsync(false);
            _identityServiceMock.Setup(x => x.RegisterAdmin(dto)).ReturnsAsync(Enumerable.Empty<IdentityError>());

            var errors = await CreateService().RegisterAsync(dto);

            errors.Should().BeEmpty();
            _identityServiceMock.Verify(x => x.RegisterAdmin(dto), Times.Once);
        }

        [Fact]
        public async Task RegisterAsync_WhenAdministratorExists_AndCallerIsNotAdmin_ShouldReturnError()
        {
            var dto = CreateAccountRequestBuilder.New().WithPhoneNumber(null).Build();
            _identityServiceMock.Setup(x => x.AdministratorExistsAsync()).ReturnsAsync(true);
            _httpContextAccessorMock = HttpContextAccessorTestHelper.WithUser(ClaimsPrincipalTestHelper.CreateUser());

            var errors = await CreateService().RegisterAsync(dto);

            errors.Should().Contain(e => e.Code == "AdminRegistrationRestricted");
        }

        [Fact]
        public async Task RegisterAsync_WhenAdministratorExists_AndCallerIsAdmin_ShouldRegisterNewAdmin()
        {
            var dto = CreateAccountRequestBuilder.New().WithPhoneNumber(null).Build();
            _identityServiceMock.Setup(x => x.AdministratorExistsAsync()).ReturnsAsync(true);
            _identityServiceMock.Setup(x => x.RegisterAdmin(dto)).ReturnsAsync(Enumerable.Empty<IdentityError>());
            _httpContextAccessorMock = HttpContextAccessorTestHelper.WithUser(ClaimsPrincipalTestHelper.CreateAdmin());

            var errors = await CreateService().RegisterAsync(dto);

            errors.Should().BeEmpty();
            _identityServiceMock.Verify(x => x.RegisterAdmin(dto), Times.Once);
        }

        [Fact]
        public async Task RegisterAsync_WhenValidUserPhoneRegistration_ShouldRegisterUser()
        {
            var dto = CreateAccountRequestBuilder.New().WithEmail(null).WithPhoneNumber("5511987654312").Build();
            _identityServiceMock
                .Setup(x => x.RegisterUser(dto, It.IsAny<string>()))
                .ReturnsAsync(Enumerable.Empty<IdentityError>());

            var errors = await CreateService().RegisterAsync(dto);

            errors.Should().BeEmpty();
            _identityServiceMock.Verify(x => x.RegisterUser(dto, It.IsAny<string>()), Times.Once);
        }

        // ── LoginAsync ───────────────────────────────────────────────────────

        [Fact]
        public async Task LoginAsync_WhenNoCredentialsProvided_ShouldThrowBadRequestException()
        {
            var dto = LoginRequestBuilder.New().WithEmail(null).WithPhone(null).Build();

            Func<Task> act = () => CreateService().LoginAsync(dto);

            await act.Should().ThrowAsync<BadRequestException>();
        }

        [Fact]
        public async Task LoginAsync_WhenBothEmailAndPhoneProvided_ShouldThrowBadRequestException()
        {
            var dto = LoginRequestBuilder.New().WithEmail("a@b.com").WithPhone("5511987654321").Build();

            Func<Task> act = () => CreateService().LoginAsync(dto);

            await act.Should().ThrowAsync<BadRequestException>();
        }

        [Fact]
        public async Task LoginAsync_WhenUserNotFound_ShouldThrowNotFoundException()
        {
            var dto = LoginRequestBuilder.New().WithPhone(null).Build();
            _identityServiceMock.Setup(x => x.FindUserByLoginAsync(dto)).ReturnsAsync((User?)null);

            Func<Task> act = () => CreateService().LoginAsync(dto);

            await act.Should().ThrowAsync<NotFoundException>();
        }

        [Fact]
        public async Task LoginAsync_WhenPasswordIsInvalid_ShouldThrowBadRequestException()
        {
            var dto = LoginRequestBuilder.New().WithPhone(null).Build();
            var user = CreateUserEntity(email: dto.Email);
            _identityServiceMock.Setup(x => x.FindUserByLoginAsync(dto)).ReturnsAsync(user);
            _identityServiceMock.Setup(x => x.CheckPasswordAsync(user, dto.Password)).ReturnsAsync(false);

            Func<Task> act = () => CreateService().LoginAsync(dto);

            await act.Should().ThrowAsync<BadRequestException>()
                .WithMessage("*Senha inválida*");
        }

        [Fact]
        public async Task LoginAsync_WhenUserHasNoValidRole_ShouldThrowInvalidOperationException()
        {
            var dto = LoginRequestBuilder.New().WithPhone(null).Build();
            var user = CreateUserEntity(email: dto.Email);
            _identityServiceMock.Setup(x => x.FindUserByLoginAsync(dto)).ReturnsAsync(user);
            _identityServiceMock.Setup(x => x.CheckPasswordAsync(user, dto.Password)).ReturnsAsync(true);
            _identityServiceMock.Setup(x => x.GetRolesAsync(user)).ReturnsAsync((false, false));

            Func<Task> act = () => CreateService().LoginAsync(dto);

            await act.Should().ThrowAsync<InvalidOperationException>();
        }

        [Fact]
        public async Task LoginAsync_WhenCredentialsAreValid_ShouldReturnAuthResponseWithGeneratedToken()
        {
            var dto = LoginRequestBuilder.New().WithPhone(null).Build();
            var user = CreateUserEntity(id: "user-1", email: dto.Email);
            _identityServiceMock.Setup(x => x.FindUserByLoginAsync(dto)).ReturnsAsync(user);
            _identityServiceMock.Setup(x => x.CheckPasswordAsync(user, dto.Password)).ReturnsAsync(true);
            _identityServiceMock.Setup(x => x.GetRolesAsync(user)).ReturnsAsync((true, false));
            _identityServiceMock.Setup(x => x.GetUserClaimsAsync(user)).ReturnsAsync(new List<Claim>());
            _identityServiceMock.Setup(x => x.GetRoleClaimsAsync("Administrator")).ReturnsAsync(new List<Claim>());
            _identityServiceMock.Setup(x => x.CreateRefreshTokenAsync(user)).ReturnsAsync("refresh-token");
            _tokenServiceMock.Setup(x => x.GenerateAccessToken(It.Is<TokenClaimsData>(d => d.UserId == "user-1" && d.IsAdmin)))
                .Returns("access-token");

            var result = await CreateService().LoginAsync(dto);

            result.UserId.Should().Be("user-1");
            result.Token.Should().Be("access-token");
            result.RefreshToken.Should().Be("refresh-token");
            _identityServiceMock.Verify(x => x.UpdateLastLoginAsync(user), Times.Once);
        }

        // ── RefreshTokenAsync ────────────────────────────────────────────────

        [Fact]
        public async Task RefreshTokenAsync_WhenTokenHasNoUidClaim_ShouldThrowSecurityTokenException()
        {
            var request = RefreshTokenRequestBuilder.New().WithToken(JwtTokenTestHelper.CreateToken(uid: null)).Build();

            Func<Task> act = () => CreateService().RefreshTokenAsync(request);

            await act.Should().ThrowAsync<SecurityTokenException>();
        }

        [Fact]
        public async Task RefreshTokenAsync_WhenTokenHasNoUsernameClaim_ShouldThrowSecurityTokenException()
        {
            var request = RefreshTokenRequestBuilder.New()
                .WithToken(JwtTokenTestHelper.CreateToken("user-1", includeUsernameClaim: false))
                .Build();

            Func<Task> act = () => CreateService().RefreshTokenAsync(request);

            await act.Should().ThrowAsync<SecurityTokenException>();
        }

        [Fact]
        public async Task RefreshTokenAsync_WhenUserNotFound_ShouldThrowUnauthorizedAccessException()
        {
            var request = RefreshTokenRequestBuilder.New()
                .WithToken(JwtTokenTestHelper.CreateToken("user-1", usernameClaimValue: "user@example.com"))
                .Build();
            _identityServiceMock.Setup(x => x.FindByNameAsync("user@example.com")).ReturnsAsync((User?)null);

            Func<Task> act = () => CreateService().RefreshTokenAsync(request);

            await act.Should().ThrowAsync<UnauthorizedAccessException>();
        }

        [Fact]
        public async Task RefreshTokenAsync_WhenRefreshTokenIsInvalid_ShouldRevokeSecurityStampAndThrow()
        {
            var request = RefreshTokenRequestBuilder.New()
                .WithToken(JwtTokenTestHelper.CreateToken("user-1", usernameClaimValue: "user@example.com"))
                .Build();
            var user = CreateUserEntity(id: "user-1", email: "user@example.com");
            _identityServiceMock.Setup(x => x.FindByNameAsync("user@example.com")).ReturnsAsync(user);
            _identityServiceMock.Setup(x => x.VerifyRefreshTokenAsync(user, request.RefreshToken)).ReturnsAsync(false);

            Func<Task> act = () => CreateService().RefreshTokenAsync(request);

            await act.Should().ThrowAsync<SecurityTokenException>();
            _identityServiceMock.Verify(x => x.RevokeSecurityStampAsync(user), Times.Once);
        }

        [Fact]
        public async Task RefreshTokenAsync_WhenTokenIsValid_ShouldReturnNewTokens()
        {
            var request = RefreshTokenRequestBuilder.New()
                .WithToken(JwtTokenTestHelper.CreateToken("user-1", usernameClaimValue: "user@example.com"))
                .Build();
            var user = CreateUserEntity(id: "user-1", email: "user@example.com");
            _identityServiceMock.Setup(x => x.FindByNameAsync("user@example.com")).ReturnsAsync(user);
            _identityServiceMock.Setup(x => x.VerifyRefreshTokenAsync(user, request.RefreshToken)).ReturnsAsync(true);
            _identityServiceMock.Setup(x => x.GetRolesAsync(user)).ReturnsAsync((true, false));
            _identityServiceMock.Setup(x => x.GetUserClaimsAsync(user)).ReturnsAsync(new List<Claim>());
            _identityServiceMock.Setup(x => x.GetRoleClaimsAsync("Administrator")).ReturnsAsync(new List<Claim>());
            _identityServiceMock.Setup(x => x.CreateRefreshTokenAsync(user)).ReturnsAsync("new-refresh-token");
            _tokenServiceMock.Setup(x => x.GenerateAccessToken(It.IsAny<TokenClaimsData>())).Returns("new-access-token");

            var result = await CreateService().RefreshTokenAsync(request);

            result.UserId.Should().Be("user-1");
            result.Token.Should().Be("new-access-token");
            result.RefreshToken.Should().Be("new-refresh-token");
        }

        // ── LogoutAsync ──────────────────────────────────────────────────────

        [Fact]
        public async Task LogoutAsync_WhenAuthenticatedViaHttpContext_ShouldUseClaimsUid()
        {
            var httpContext = new DefaultHttpContext { User = ClaimsPrincipalTestHelper.CreateUser("user-1") };
            _httpContextAccessorMock.SetupGet(x => x.HttpContext).Returns(httpContext);

            await CreateService().LogoutAsync();

            _identityServiceMock.Verify(x => x.LogoutAsync("user-1"), Times.Once);
        }

        [Fact]
        public async Task LogoutAsync_WhenNotAuthenticated_ShouldFallBackToTokenClaim()
        {
            var token = JwtTokenTestHelper.CreateToken("user-2");

            await CreateService().LogoutAsync(token);

            _identityServiceMock.Verify(x => x.LogoutAsync("user-2"), Times.Once);
        }

        [Fact]
        public async Task LogoutAsync_WhenNoUidAvailable_ShouldThrowUnauthorizedAccessException()
        {
            Func<Task> act = () => CreateService().LogoutAsync(token: null);

            await act.Should().ThrowAsync<UnauthorizedAccessException>();
        }

        // ── ResetPasswordAsync ───────────────────────────────────────────────

        [Fact]
        public async Task ResetPasswordAsync_WhenPasswordsDoNotMatch_ShouldThrowArgumentException()
        {
            var dto = PasswordResetRequestBuilder.New().WithPassword("Senha123").WithPasswordConfirm("Outra123").Build();

            Func<Task> act = () => CreateService().ResetPasswordAsync(dto, ClaimsPrincipalTestHelper.CreateUnauthenticated());

            await act.Should().ThrowAsync<ArgumentException>();
        }

        [Fact]
        public async Task ResetPasswordAsync_WhenPhoneIsMissing_ShouldThrowArgumentException()
        {
            var dto = PasswordResetRequestBuilder.New().WithPhone(" ").Build();

            Func<Task> act = () => CreateService().ResetPasswordAsync(dto, ClaimsPrincipalTestHelper.CreateUnauthenticated());

            await act.Should().ThrowAsync<ArgumentException>();
        }

        [Fact]
        public async Task ResetPasswordAsync_WhenUserNotFound_ShouldThrowNotFoundException()
        {
            var dto = PasswordResetRequestBuilder.New().Build();
            _repositoryMock.Setup(x => x.FindByPhoneAsync(dto.Phone)).ReturnsAsync((User?)null);

            Func<Task> act = () => CreateService().ResetPasswordAsync(dto, ClaimsPrincipalTestHelper.CreateUnauthenticated());

            await act.Should().ThrowAsync<NotFoundException>();
        }

        [Fact]
        public async Task ResetPasswordAsync_WhenAnonymous_ShouldSkipAuthorizationAndUpdatePassword()
        {
            var dto = PasswordResetRequestBuilder.New().Build();
            var user = CreateUserEntity(id: "user-1", phoneNumber: dto.Phone);
            _repositoryMock.Setup(x => x.FindByPhoneAsync(dto.Phone)).ReturnsAsync(user);

            await CreateService().ResetPasswordAsync(dto, ClaimsPrincipalTestHelper.CreateUnauthenticated());

            _repositoryMock.Verify(x => x.UpdatePasswordAsync(user, dto.Password), Times.Once);
        }

        [Fact]
        public async Task ResetPasswordAsync_WhenNonAdminTargetsAnotherUser_ShouldThrowSecurityException()
        {
            var dto = PasswordResetRequestBuilder.New().Build();
            var user = CreateUserEntity(id: "other-user", phoneNumber: dto.Phone);
            _repositoryMock.Setup(x => x.FindByPhoneAsync(dto.Phone)).ReturnsAsync(user);

            Func<Task> act = () => CreateService().ResetPasswordAsync(dto, ClaimsPrincipalTestHelper.CreateUser("user-1"));

            await act.Should().ThrowAsync<SecurityException>();
        }

        [Fact]
        public async Task ResetPasswordAsync_WhenUserResetsOwnPassword_ShouldUpdatePassword()
        {
            var dto = PasswordResetRequestBuilder.New().Build();
            var user = CreateUserEntity(id: "user-1", phoneNumber: dto.Phone);
            _repositoryMock.Setup(x => x.FindByPhoneAsync(dto.Phone)).ReturnsAsync(user);

            await CreateService().ResetPasswordAsync(dto, ClaimsPrincipalTestHelper.CreateUser("user-1"));

            _repositoryMock.Verify(x => x.UpdatePasswordAsync(user, dto.Password), Times.Once);
        }

        [Fact]
        public async Task ResetPasswordAsync_WhenAdminTargetsAnotherUser_ShouldBypassOwnershipCheck()
        {
            var dto = PasswordResetRequestBuilder.New().Build();
            var user = CreateUserEntity(id: "other-user", phoneNumber: dto.Phone);
            _repositoryMock.Setup(x => x.FindByPhoneAsync(dto.Phone)).ReturnsAsync(user);

            await CreateService().ResetPasswordAsync(dto, ClaimsPrincipalTestHelper.CreateAdmin("admin-1"));

            _repositoryMock.Verify(x => x.UpdatePasswordAsync(user, dto.Password), Times.Once);
        }
    }
}
