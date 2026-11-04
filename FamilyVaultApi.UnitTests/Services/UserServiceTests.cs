using FamilyVaultApi.Exceptions;
using FamilyVaultApi.Data.Entities;
using FamilyVaultApi.Models.Dto.Requests.Account;
using FamilyVaultApi.Models.Dto.Requests.User;
using FamilyVaultApi.Services.IService;
using Microsoft.AspNetCore.Identity;
using FamilyVaultApi.Models.Dto.Responses.User;
using FamilyVaultApi.Models.Internal;
using FamilyVaultApi.Models.Internal.Enums;
using FamilyVaultApi.Repositories.IRepository;
using FamilyVaultApi.Services.Service;
using FamilyVaultApi.UnitTests.Builders.User;
using FamilyVaultApi.UnitTests.Helpers;
using FluentAssertions;
using Moq;
using System.Security;

namespace FamilyVaultApi.UnitTests.Services
{
    public class UserServiceTests
    {
        private readonly Mock<IUserRepository> _repositoryMock;
        private readonly Mock<IIdentityService> _identityMock = new();
        private readonly Mock<IAccountRepository> _accountRepositoryMock = new();
        private readonly Mock<IPhoneNumberService> _phoneMock = new();
        private readonly UserService _service;

        public UserServiceTests()
        {
            _repositoryMock = new Mock<IUserRepository>();
            _service = new UserService(_repositoryMock.Object, _identityMock.Object, _accountRepositoryMock.Object, _phoneMock.Object);
        }

        // ── GetUsersAsync ────────────────────────────────────────────────────

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task GetUsersAsync_WhenPageNumberIsInvalid_ShouldNormalizeToOne(int pageNumber)
        {
            var query = new UserQueryRequestDto { PageNumber = pageNumber, PageSize = 20 };
            var expected = new PagedResult<UserResponseDto>();

            _repositoryMock
                .Setup(x => x.GetAllUsersAsync(It.Is<UserQueryRequestDto>(q => q.PageNumber == 1)))
                .ReturnsAsync(expected);

            var result = await _service.GetUsersAsync(query);

            result.Should().Be(expected);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        public async Task GetUsersAsync_WhenPageSizeIsOutOfRange_ShouldNormalizeToTwenty(int pageSize)
        {
            var query = new UserQueryRequestDto { PageNumber = 1, PageSize = pageSize };
            var expected = new PagedResult<UserResponseDto>();

            _repositoryMock
                .Setup(x => x.GetAllUsersAsync(It.Is<UserQueryRequestDto>(q => q.PageSize == 20)))
                .ReturnsAsync(expected);

            var result = await _service.GetUsersAsync(query);

            result.Should().Be(expected);
        }

        [Fact]
        public async Task GetUsersAsync_WhenPageSizeIsAboveMaximum_ShouldNormalizeToOneHundred()
        {
            var query = new UserQueryRequestDto { PageNumber = 1, PageSize = 101 };
            var expected = new PagedResult<UserResponseDto>();

            _repositoryMock
                .Setup(x => x.GetAllUsersAsync(It.Is<UserQueryRequestDto>(q => q.PageSize == 100)))
                .ReturnsAsync(expected);

            var result = await _service.GetUsersAsync(query);

            result.Should().Be(expected);
        }

        [Fact]
        public async Task GetUsersAsync_WhenPagingIsValid_ShouldKeepItUnchanged()
        {
            var query = new UserQueryRequestDto { PageNumber = 2, PageSize = 50 };
            var expected = new PagedResult<UserResponseDto>();

            _repositoryMock
                .Setup(x => x.GetAllUsersAsync(It.Is<UserQueryRequestDto>(q => q.PageNumber == 2 && q.PageSize == 50)))
                .ReturnsAsync(expected);

            var result = await _service.GetUsersAsync(query);

            result.Should().Be(expected);
        }

        // ── UpdateUserAsync ──────────────────────────────────────────────────

        [Fact]
        public async Task UpdateUserAsync_WhenDtoIsNull_ShouldThrowArgumentNullException()
        {
            Func<Task> act = () => _service.UpdateUserAsync("user-1", null!, ClaimsPrincipalTestHelper.CreateUser("user-1"));

            await act.Should().ThrowAsync<ArgumentNullException>();
        }

        [Fact]
        public async Task UpdateUserAsync_WhenNotAuthenticated_ShouldThrowUnauthorizedAccessException()
        {
            var dto = UpdateUserDtoBuilder.New().Build();

            Func<Task> act = () => _service.UpdateUserAsync("user-1", dto, ClaimsPrincipalTestHelper.CreateUnauthenticated());

            await act.Should().ThrowAsync<UnauthorizedAccessException>();
        }

        [Fact]
        public async Task UpdateUserAsync_WhenUserTriesToUpdateAnotherUser_ShouldThrowSecurityException()
        {
            var dto = UpdateUserDtoBuilder.New().Build();

            Func<Task> act = () => _service.UpdateUserAsync("other-user", dto, ClaimsPrincipalTestHelper.CreateUser("user-1"));

            await act.Should().ThrowAsync<SecurityException>();
        }

        [Fact]
        public async Task UpdateUserAsync_WhenUserUpdatesOwnAccount_ShouldSucceed()
        {
            var dto = UpdateUserDtoBuilder.New().Build();
            var expected = new UserResponseDto { UserId = "user-1" };

            _repositoryMock.Setup(x => x.UpdateAsync("user-1", dto)).ReturnsAsync(expected);

            var result = await _service.UpdateUserAsync("user-1", dto, ClaimsPrincipalTestHelper.CreateUser("user-1"));

            result.Should().Be(expected);
        }

        [Fact]
        public async Task UpdateUserAsync_WhenAdminUpdatesAnotherUser_ShouldSucceed()
        {
            var dto = UpdateUserDtoBuilder.New().Build();
            var expected = new UserResponseDto { UserId = "target-user" };

            _repositoryMock.Setup(x => x.UpdateAsync("target-user", dto)).ReturnsAsync(expected);

            var result = await _service.UpdateUserAsync("target-user", dto, ClaimsPrincipalTestHelper.CreateAdmin("admin-1"));

            result.Should().Be(expected);
        }

        // ── DeleteUserAsync ──────────────────────────────────────────────────

        [Fact]
        public async Task DeleteUserAsync_WhenNotAuthenticated_ShouldThrowUnauthorizedAccessException()
        {
            Func<Task> act = () => _service.DeleteUserAsync("user-1", ClaimsPrincipalTestHelper.CreateUnauthenticated());

            await act.Should().ThrowAsync<UnauthorizedAccessException>();
        }

        [Fact]
        public async Task DeleteUserAsync_WhenUserTriesToDeleteAnotherUser_ShouldThrowSecurityException()
        {
            Func<Task> act = () => _service.DeleteUserAsync("other-user", ClaimsPrincipalTestHelper.CreateUser("user-1"));

            await act.Should().ThrowAsync<SecurityException>();
        }

        [Fact]
        public async Task DeleteUserAsync_WhenUserDeletesOwnAccount_ShouldSucceed()
        {
            await _service.DeleteUserAsync("user-1", ClaimsPrincipalTestHelper.CreateUser("user-1"));

            _repositoryMock.Verify(x => x.DeleteAsync("user-1"), Times.Once);
        }

        [Fact]
        public async Task DeleteUserAsync_WhenAdminDeletesAnotherUser_ShouldSucceed()
        {
            await _service.DeleteUserAsync("target-user", ClaimsPrincipalTestHelper.CreateAdmin());

            _repositoryMock.Verify(x => x.DeleteAsync("target-user"), Times.Once);
        }

        [Fact]
        public async Task DeleteUserAsync_WhenTargetIsLastAdministrator_ShouldThrowConflictException()
        {
            _repositoryMock.Setup(x => x.IsLastAdministratorAsync("admin-1")).ReturnsAsync(true);

            Func<Task> act = () => _service.DeleteUserAsync("admin-1", ClaimsPrincipalTestHelper.CreateAdmin("admin-1"));

            await act.Should().ThrowAsync<ConflictException>();
            _repositoryMock.Verify(x => x.DeleteAsync(It.IsAny<string>()), Times.Never);
        }

        // ── ManageUsers ──────────────────────────────────────────────────────

        [Fact]
        public async Task UpdateUserAsync_WhenManageUsersUpdatesRegularUser_ShouldSucceed()
        {
            var dto = UpdateUserDtoBuilder.New().Build();
            var expected = new UserResponseDto { UserId = "target-user" };

            _repositoryMock.Setup(x => x.IsAdministratorAsync("target-user")).ReturnsAsync(false);
            _repositoryMock.Setup(x => x.UpdateAsync("target-user", dto)).ReturnsAsync(expected);

            var result = await _service.UpdateUserAsync("target-user", dto,
                ClaimsPrincipalTestHelper.CreateUserWithPermission("user-1", PermissionCode.ManageUsers));

            result.Should().Be(expected);
        }

        [Fact]
        public async Task UpdateUserAsync_WhenManageUsersUpdatesAdministrator_ShouldThrowSecurityException()
        {
            var dto = UpdateUserDtoBuilder.New().Build();

            _repositoryMock.Setup(x => x.IsAdministratorAsync("admin-1")).ReturnsAsync(true);

            Func<Task> act = () => _service.UpdateUserAsync("admin-1", dto,
                ClaimsPrincipalTestHelper.CreateUserWithPermission("user-1", PermissionCode.ManageUsers));

            await act.Should().ThrowAsync<SecurityException>();
            _repositoryMock.Verify(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<UpdateUserDto>()), Times.Never);
        }

        [Fact]
        public async Task UpdateUserAsync_WhenOtherPermissionUpdatesAnotherUser_ShouldThrowSecurityException()
        {
            var dto = UpdateUserDtoBuilder.New().Build();

            Func<Task> act = () => _service.UpdateUserAsync("other-user", dto,
                ClaimsPrincipalTestHelper.CreateUserWithPermission("user-1", PermissionCode.ManageTransactions));

            await act.Should().ThrowAsync<SecurityException>();
            _repositoryMock.Verify(x => x.IsAdministratorAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task UpdateUserAsync_WhenManageUsersTargetDoesNotExist_ShouldThrowNotFoundException()
        {
            var dto = UpdateUserDtoBuilder.New().Build();

            _repositoryMock.Setup(x => x.IsAdministratorAsync("missing"))
                .ThrowsAsync(new NotFoundException("Usuário", "missing"));

            Func<Task> act = () => _service.UpdateUserAsync("missing", dto,
                ClaimsPrincipalTestHelper.CreateUserWithPermission("user-1", PermissionCode.ManageUsers));

            await act.Should().ThrowAsync<NotFoundException>();
        }

        [Fact]
        public async Task DeleteUserAsync_WhenManageUsersDeletesRegularUser_ShouldSucceed()
        {
            _repositoryMock.Setup(x => x.IsAdministratorAsync("target-user")).ReturnsAsync(false);

            await _service.DeleteUserAsync("target-user",
                ClaimsPrincipalTestHelper.CreateUserWithPermission("user-1", PermissionCode.ManageUsers));

            _repositoryMock.Verify(x => x.DeleteAsync("target-user"), Times.Once);
        }

        [Fact]
        public async Task DeleteUserAsync_WhenManageUsersDeletesAdministrator_ShouldThrowSecurityException()
        {
            _repositoryMock.Setup(x => x.IsAdministratorAsync("admin-1")).ReturnsAsync(true);

            Func<Task> act = () => _service.DeleteUserAsync("admin-1",
                ClaimsPrincipalTestHelper.CreateUserWithPermission("user-1", PermissionCode.ManageUsers));

            await act.Should().ThrowAsync<SecurityException>();
            _repositoryMock.Verify(x => x.DeleteAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task DeleteUserAsync_WhenAdminDeletesAnotherUser_ShouldNotCheckTargetRole()
        {
            await _service.DeleteUserAsync("target-user", ClaimsPrincipalTestHelper.CreateAdmin());

            _repositoryMock.Verify(x => x.IsAdministratorAsync(It.IsAny<string>()), Times.Never);
        }

        // ── CreateUserAsync ──────────────────────────────────────────────────

        private static CreateUserRequestDto NewCreateDto(string role, string? email, string? phone) => new()
        {
            FirstName = "Ana", LastName = "Silva", Age = 30,
            Password = "Senha@123", PasswordConfirm = "Senha@123",
            Role = role, Email = email, PhoneNumber = phone
        };

        [Fact]
        public async Task CreateUserAsync_WhenDtoIsNull_ShouldThrowArgumentNullException()
        {
            Func<Task> act = () => _service.CreateUserAsync(null!);

            await act.Should().ThrowAsync<ArgumentNullException>();
        }

        [Fact]
        public async Task CreateUserAsync_WhenAdministratorWithoutEmail_ShouldThrowBadRequestException()
        {
            Func<Task> act = () => _service.CreateUserAsync(NewCreateDto("Administrator", null, null));

            await act.Should().ThrowAsync<BadRequestException>();
            _identityMock.Verify(x => x.RegisterAdmin(It.IsAny<CreateAccountRequestDto>()), Times.Never);
        }

        [Fact]
        public async Task CreateUserAsync_WhenUserWithoutPhone_ShouldThrowBadRequestException()
        {
            Func<Task> act = () => _service.CreateUserAsync(NewCreateDto("User", "a@b.com", null));

            await act.Should().ThrowAsync<BadRequestException>();
            _identityMock.Verify(x => x.RegisterUser(It.IsAny<CreateAccountRequestDto>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task CreateUserAsync_WhenRoleIsUnknown_ShouldThrowBadRequestException()
        {
            Func<Task> act = () => _service.CreateUserAsync(NewCreateDto("Root", "a@b.com", "+5511987654312"));

            await act.Should().ThrowAsync<BadRequestException>();
        }

        [Fact]
        public async Task CreateUserAsync_WhenPasswordsDoNotMatch_ShouldThrowBadRequestException()
        {
            var dto = NewCreateDto("Administrator", "admin@teste.com", null);
            dto.PasswordConfirm = "outra";
            _accountRepositoryMock.Setup(x => x.EmailUserExistsAsync(It.IsAny<string>())).ReturnsAsync(false);

            Func<Task> act = () => _service.CreateUserAsync(dto);

            await act.Should().ThrowAsync<BadRequestException>();
            _identityMock.Verify(x => x.RegisterAdmin(It.IsAny<CreateAccountRequestDto>()), Times.Never);
        }

        [Fact]
        public async Task CreateUserAsync_WhenAdministratorEmailAlreadyExists_ShouldThrowBadRequestException()
        {
            var dto = NewCreateDto("Administrator", "admin@teste.com", null);
            _accountRepositoryMock.Setup(x => x.EmailUserExistsAsync("admin@teste.com")).ReturnsAsync(true);

            Func<Task> act = () => _service.CreateUserAsync(dto);

            await act.Should().ThrowAsync<BadRequestException>();
            _identityMock.Verify(x => x.RegisterAdmin(It.IsAny<CreateAccountRequestDto>()), Times.Never);
        }

        [Fact]
        public async Task CreateUserAsync_WhenAdministratorHasPhoneNumber_ShouldThrowBadRequestException()
        {
            var dto = NewCreateDto("Administrator", "admin@teste.com", "+5511987654312");

            Func<Task> act = () => _service.CreateUserAsync(dto);

            (await act.Should().ThrowAsync<BadRequestException>()).WithMessage("Administrador é cadastrado só com e-mail.");
            _identityMock.Verify(x => x.RegisterAdmin(It.IsAny<CreateAccountRequestDto>()), Times.Never);
        }

        [Fact]
        public async Task CreateUserAsync_WhenUserEmailAlreadyExists_ShouldThrowBadRequestException()
        {
            var dto = NewCreateDto("User", "dup@teste.com", "(11) 98765-4312");
            string? formatted = "+5511987654312";
            _phoneMock.Setup(x => x.TryValidateAndFormat(It.IsAny<string?>(), "BR", out formatted)).Returns(true);
            _accountRepositoryMock.Setup(x => x.PhoneExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
            _accountRepositoryMock.Setup(x => x.EmailUserExistsAsync("dup@teste.com")).ReturnsAsync(true);

            Func<Task> act = () => _service.CreateUserAsync(dto);

            (await act.Should().ThrowAsync<BadRequestException>()).WithMessage("*E-mail já cadastrado*");
            _identityMock.Verify(x => x.RegisterUser(It.IsAny<CreateAccountRequestDto>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task CreateUserAsync_WhenUserEmailIsFree_ShouldCallRegisterUser()
        {
            var dto = NewCreateDto("User", "livre@teste.com", "(11) 98765-4312");
            string? formatted = "+5511987654312";
            var created = new User { Id = "new-user" };
            var expected = new UserResponseDto { UserId = "new-user" };

            _phoneMock.Setup(x => x.TryValidateAndFormat(It.IsAny<string?>(), "BR", out formatted)).Returns(true);
            _accountRepositoryMock.Setup(x => x.PhoneExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
            _accountRepositoryMock.Setup(x => x.EmailUserExistsAsync("livre@teste.com")).ReturnsAsync(false);
            _identityMock.Setup(x => x.RegisterUser(It.IsAny<CreateAccountRequestDto>(), "+5511987654312")).ReturnsAsync(new List<IdentityError>());
            _identityMock.Setup(x => x.FindByNameAsync("+5511987654312")).ReturnsAsync(created);
            _repositoryMock.Setup(x => x.GetByIdAsync("new-user")).ReturnsAsync(expected);

            var result = await _service.CreateUserAsync(dto);

            result.Should().Be(expected);
            _identityMock.Verify(x => x.RegisterUser(It.IsAny<CreateAccountRequestDto>(), "+5511987654312"), Times.Once);
        }

        [Fact]
        public async Task CreateUserAsync_WhenValidAdministrator_ShouldCallRegisterAdminAndReturnCreatedUser()
        {
            var dto = NewCreateDto("Administrator", "admin@teste.com", null);
            var created = new User { Id = "new-admin" };
            var expected = new UserResponseDto { UserId = "new-admin" };

            _accountRepositoryMock.Setup(x => x.EmailUserExistsAsync("admin@teste.com")).ReturnsAsync(false);
            _identityMock.Setup(x => x.RegisterAdmin(It.IsAny<CreateAccountRequestDto>())).ReturnsAsync(new List<IdentityError>());
            _identityMock.Setup(x => x.FindByNameAsync("admin@teste.com")).ReturnsAsync(created);
            _repositoryMock.Setup(x => x.GetByIdAsync("new-admin")).ReturnsAsync(expected);

            var result = await _service.CreateUserAsync(dto);

            result.Should().Be(expected);
            _identityMock.Verify(x => x.RegisterAdmin(It.Is<CreateAccountRequestDto>(a => a.Email == "admin@teste.com")), Times.Once);
            _identityMock.Verify(x => x.RegisterUser(It.IsAny<CreateAccountRequestDto>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task CreateUserAsync_WhenValidUser_ShouldCallRegisterUserWithFormattedPhone()
        {
            var dto = NewCreateDto("User", null, "(11) 98765-4312");
            string? formatted = "+5511987654312";
            var created = new User { Id = "new-user" };
            var expected = new UserResponseDto { UserId = "new-user" };

            _phoneMock.Setup(x => x.TryValidateAndFormat("(11) 98765-4312", "BR", out formatted)).Returns(true);
            _accountRepositoryMock.Setup(x => x.PhoneExistsAsync("+5511987654312")).ReturnsAsync(false);
            _identityMock.Setup(x => x.RegisterUser(It.IsAny<CreateAccountRequestDto>(), "+5511987654312")).ReturnsAsync(new List<IdentityError>());
            _identityMock.Setup(x => x.FindByNameAsync("+5511987654312")).ReturnsAsync(created);
            _repositoryMock.Setup(x => x.GetByIdAsync("new-user")).ReturnsAsync(expected);

            var result = await _service.CreateUserAsync(dto);

            result.Should().Be(expected);
            _identityMock.Verify(x => x.RegisterUser(It.IsAny<CreateAccountRequestDto>(), "+5511987654312"), Times.Once);
            _identityMock.Verify(x => x.RegisterAdmin(It.IsAny<CreateAccountRequestDto>()), Times.Never);
        }

        [Fact]
        public async Task CreateUserAsync_WhenUserPhoneAlreadyExists_ShouldThrowBadRequestException()
        {
            var dto = NewCreateDto("User", null, "(11) 98765-4312");
            string? formatted = "+5511987654312";
            _phoneMock.Setup(x => x.TryValidateAndFormat(It.IsAny<string?>(), "BR", out formatted)).Returns(true);
            _accountRepositoryMock.Setup(x => x.PhoneExistsAsync("+5511987654312")).ReturnsAsync(true);

            Func<Task> act = () => _service.CreateUserAsync(dto);

            await act.Should().ThrowAsync<BadRequestException>();
            _identityMock.Verify(x => x.RegisterUser(It.IsAny<CreateAccountRequestDto>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task CreateUserAsync_WhenIdentityReturnsErrors_ShouldThrowBadRequestException()
        {
            var dto = NewCreateDto("Administrator", "admin@teste.com", null);
            _accountRepositoryMock.Setup(x => x.EmailUserExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
            _identityMock.Setup(x => x.RegisterAdmin(It.IsAny<CreateAccountRequestDto>()))
                .ReturnsAsync(new List<IdentityError> { new() { Code = "PasswordTooShort", Description = "curta" } });

            Func<Task> act = () => _service.CreateUserAsync(dto);

            (await act.Should().ThrowAsync<BadRequestException>()).WithMessage("*PasswordTooShort*");
        }

        // ── Permissions ──────────────────────────────────────────────────────

        [Fact]
        public async Task GetPermissionsAsync_ShouldDelegateToRepository()
        {
            var expected = new List<PermissionCode> { PermissionCode.ManageUsers };
            _repositoryMock.Setup(x => x.GetPermissionsAsync("user-1")).ReturnsAsync(expected);

            var result = await _service.GetPermissionsAsync("user-1");

            result.Should().BeSameAs(expected);
        }

        [Fact]
        public async Task GrantPermissionAsync_ShouldDelegateToRepository()
        {
            await _service.GrantPermissionAsync("user-1", PermissionCode.ManageCategories);

            _repositoryMock.Verify(x => x.GrantPermissionAsync("user-1", PermissionCode.ManageCategories), Times.Once);
        }

        [Fact]
        public async Task RevokePermissionAsync_ShouldDelegateToRepository()
        {
            await _service.RevokePermissionAsync("user-1", PermissionCode.ManageCategories);

            _repositoryMock.Verify(x => x.RevokePermissionAsync("user-1", PermissionCode.ManageCategories), Times.Once);
        }

        // -- GetUsersAsync: Search ---------------------------------------------

        [Fact]
        public async Task GetUsersAsync_WhenSearchProvided_ShouldPassItUnchangedToRepository()
        {
            var query = new UserQueryRequestDto { PageNumber = 1, PageSize = 20, Search = "maria" };
            UserQueryRequestDto? received = null;

            _repositoryMock
                .Setup(x => x.GetAllUsersAsync(It.IsAny<UserQueryRequestDto>()))
                .Callback<UserQueryRequestDto>(q => received = q)
                .ReturnsAsync(new PagedResult<UserResponseDto>());

            await _service.GetUsersAsync(query);

            received.Should().NotBeNull();
            received!.Search.Should().Be("maria");
        }

        // -- GetMeAsync --------------------------------------------------------

        [Fact]
        public async Task GetMeAsync_WhenPrincipalHasNoUid_ShouldThrowUnauthorizedAccessException()
        {
            Func<Task> act = () => _service.GetMeAsync(ClaimsPrincipalTestHelper.CreateUnauthenticated());

            await act.Should().ThrowAsync<UnauthorizedAccessException>();
            _repositoryMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task GetMeAsync_WhenRepositoryReportsUserMissing_ShouldPropagateNotFoundException()
        {
            // O NotFoundException nasce no repository (GetByIdAsync); o service so propaga.
            _repositoryMock
                .Setup(x => x.GetByIdAsync("user-1"))
                .ThrowsAsync(new NotFoundException("Usuario", "user-1"));

            Func<Task> act = () => _service.GetMeAsync(ClaimsPrincipalTestHelper.CreateUser("user-1"));

            await act.Should().ThrowAsync<NotFoundException>();
        }

        [Fact]
        public async Task GetMeAsync_WhenUserExists_ShouldReturnOnlyTheTokenUser()
        {
            var me = new UserResponseDto { UserId = "user-1", FirstName = "Ana" };
            var other = new UserResponseDto { UserId = "user-2", FirstName = "Beto" };

            _repositoryMock.Setup(x => x.GetByIdAsync("user-1")).ReturnsAsync(me);
            _repositoryMock.Setup(x => x.GetByIdAsync("user-2")).ReturnsAsync(other);

            var result = await _service.GetMeAsync(ClaimsPrincipalTestHelper.CreateUser("user-1"));

            result.Should().Be(me);
            _repositoryMock.Verify(x => x.GetByIdAsync("user-2"), Times.Never);
        }
    }
}
