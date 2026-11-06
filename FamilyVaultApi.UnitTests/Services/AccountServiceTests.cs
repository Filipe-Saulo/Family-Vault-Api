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

            string defaultFormatted = "+5511987654312";
            _phoneNumberServiceMock
                .Setup(x => x.TryValidateAndFormat(It.IsAny<string>(), It.IsAny<string>(), out defaultFormatted))
                .Returns(true);
        }

        private AccountService CreateService() => new(_repositoryMock.Object, _identityServiceMock.Object, _tokenServiceMock.Object, _phoneNumberServiceMock.Object, _refreshTokenServiceMock.Object);

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
        public async Task RegisterAsync_WhenEmailIsProvided_ShouldThrowBadRequestException()
        {
            var dto = CreateAccountRequestBuilder.New().WithPhoneNumber("5511987654312").Build();

            Func<Task> act = () => CreateService().RegisterAsync(dto);

            await act.Should().ThrowAsync<BadRequestException>();
            _identityServiceMock.Verify(x => x.RegisterAdmin(It.IsAny<CreateAccountRequestDto>()), Times.Never);
            _identityServiceMock.Verify(x => x.RegisterUser(It.IsAny<CreateAccountRequestDto>(), It.IsAny<string>()), Times.Never);
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
