using FamilyVaultApi.Exceptions;
using FamilyVaultApi.Models.Dto.Requests.Account;
using FamilyVaultApi.Models.Dto.Responses.Account;
using FamilyVaultApi.Models.Internal;
using FamilyVaultApi.Repositories.IRepository;
using FamilyVaultApi.Services.Service;
using FamilyVaultApi.UnitTests.Builders.Account;
using FamilyVaultApi.UnitTests.Helpers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Moq;

namespace FamilyVaultApi.UnitTests.Services
{
    public class AccountServiceTests
    {
        private readonly Mock<IAccountRepository> _repositoryMock;
        private Mock<IHttpContextAccessor> _httpContextAccessorMock;

        public AccountServiceTests()
        {
            _repositoryMock = new Mock<IAccountRepository>();
            _httpContextAccessorMock = HttpContextAccessorTestHelper.WithNoHttpContext();

            SetUpHappyPathRepositoryDefaults();
        }

        private void SetUpHappyPathRepositoryDefaults()
        {
            _repositoryMock.Setup(x => x.EmailUserExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
            _repositoryMock.Setup(x => x.PhoneExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
            _repositoryMock.Setup(x => x.AdministratorExistsAsync()).ReturnsAsync(false);
        }

        private AccountService CreateService() => new(_repositoryMock.Object, _httpContextAccessorMock.Object);

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
            _repositoryMock.Setup(x => x.AdministratorExistsAsync()).ReturnsAsync(false);
            _repositoryMock.Setup(x => x.RegisterAdmin(dto)).ReturnsAsync(Enumerable.Empty<IdentityError>());

            var errors = await CreateService().RegisterAsync(dto);

            errors.Should().BeEmpty();
            _repositoryMock.Verify(x => x.RegisterAdmin(dto), Times.Once);
        }

        [Fact]
        public async Task RegisterAsync_WhenAdministratorExists_AndCallerIsNotAdmin_ShouldReturnError()
        {
            var dto = CreateAccountRequestBuilder.New().WithPhoneNumber(null).Build();
            _repositoryMock.Setup(x => x.AdministratorExistsAsync()).ReturnsAsync(true);
            _httpContextAccessorMock = HttpContextAccessorTestHelper.WithUser(ClaimsPrincipalTestHelper.CreateUser());

            var errors = await CreateService().RegisterAsync(dto);

            errors.Should().Contain(e => e.Code == "AdminRegistrationRestricted");
        }

        [Fact]
        public async Task RegisterAsync_WhenAdministratorExists_AndCallerIsAdmin_ShouldRegisterNewAdmin()
        {
            var dto = CreateAccountRequestBuilder.New().WithPhoneNumber(null).Build();
            _repositoryMock.Setup(x => x.AdministratorExistsAsync()).ReturnsAsync(true);
            _repositoryMock.Setup(x => x.RegisterAdmin(dto)).ReturnsAsync(Enumerable.Empty<IdentityError>());
            _httpContextAccessorMock = HttpContextAccessorTestHelper.WithUser(ClaimsPrincipalTestHelper.CreateAdmin());

            var errors = await CreateService().RegisterAsync(dto);

            errors.Should().BeEmpty();
            _repositoryMock.Verify(x => x.RegisterAdmin(dto), Times.Once);
        }

        [Fact]
        public async Task RegisterAsync_WhenValidUserPhoneRegistration_ShouldRegisterUser()
        {
            var dto = CreateAccountRequestBuilder.New().WithEmail(null).WithPhoneNumber("5511987654312").Build();
            _repositoryMock
                .Setup(x => x.RegisterUser(dto, It.IsAny<string>()))
                .ReturnsAsync(Enumerable.Empty<IdentityError>());

            var errors = await CreateService().RegisterAsync(dto);

            errors.Should().BeEmpty();
            _repositoryMock.Verify(x => x.RegisterUser(dto, It.IsAny<string>()), Times.Once);
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
        public async Task LoginAsync_WhenRepositoryReturnsNull_ShouldThrowUnauthorizedAccessException()
        {
            var dto = LoginRequestBuilder.New().WithPhone(null).Build();
            _repositoryMock.Setup(x => x.Login(dto)).ReturnsAsync((AuthResult?)null!);

            Func<Task> act = () => CreateService().LoginAsync(dto);

            await act.Should().ThrowAsync<UnauthorizedAccessException>();
        }

        [Fact]
        public async Task LoginAsync_WhenCredentialsAreValid_ShouldReturnMappedAuthResponse()
        {
            var dto = LoginRequestBuilder.New().WithPhone(null).Build();
            var authResult = new AuthResult { UserId = "user-1", Token = "token", RefreshToken = "refresh" };
            _repositoryMock.Setup(x => x.Login(dto)).ReturnsAsync(authResult);

            var result = await CreateService().LoginAsync(dto);

            result.UserId.Should().Be("user-1");
            result.Token.Should().Be("token");
            result.RefreshToken.Should().Be("refresh");
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
        public async Task RefreshTokenAsync_WhenTokenIsValid_ShouldDelegateToRepository()
        {
            var request = RefreshTokenRequestBuilder.New().WithToken(JwtTokenTestHelper.CreateToken("user-1")).Build();
            var expected = new AuthResponseDto { UserId = "user-1" };
            _repositoryMock.Setup(x => x.RefreshTokenAsync(request)).ReturnsAsync(expected);

            var result = await CreateService().RefreshTokenAsync(request);

            result.Should().Be(expected);
        }

        // ── LogoutAsync ──────────────────────────────────────────────────────

        [Fact]
        public async Task LogoutAsync_WhenAuthenticatedViaHttpContext_ShouldUseClaimsUid()
        {
            var httpContext = new DefaultHttpContext { User = ClaimsPrincipalTestHelper.CreateUser("user-1") };
            _httpContextAccessorMock.SetupGet(x => x.HttpContext).Returns(httpContext);

            await CreateService().LogoutAsync();

            _repositoryMock.Verify(x => x.LogoutAsync("user-1"), Times.Once);
        }

        [Fact]
        public async Task LogoutAsync_WhenNotAuthenticated_ShouldFallBackToTokenClaim()
        {
            var token = JwtTokenTestHelper.CreateToken("user-2");

            await CreateService().LogoutAsync(token);

            _repositoryMock.Verify(x => x.LogoutAsync("user-2"), Times.Once);
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
        public async Task ResetPasswordAsync_WhenCallerIsAnonymous_ShouldPassNullUidAndIsLoggedFalse()
        {
            var dto = PasswordResetRequestBuilder.New().Build();

            await CreateService().ResetPasswordAsync(dto, ClaimsPrincipalTestHelper.CreateUnauthenticated());

            _repositoryMock.Verify(x => x.ResetPasswordAsync(dto, null!, false), Times.Once);
        }

        [Fact]
        public async Task ResetPasswordAsync_WhenCallerIsAuthenticated_ShouldPassUidAndIsLoggedTrue()
        {
            var dto = PasswordResetRequestBuilder.New().Build();

            await CreateService().ResetPasswordAsync(dto, ClaimsPrincipalTestHelper.CreateUser("user-1"));

            _repositoryMock.Verify(x => x.ResetPasswordAsync(dto, "user-1", true), Times.Once);
        }
    }
}
