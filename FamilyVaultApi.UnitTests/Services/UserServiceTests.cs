using FamilyVaultApi.Models.Dto.Requests.User;
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
        private readonly UserService _service;

        public UserServiceTests()
        {
            _repositoryMock = new Mock<IUserRepository>();
            _service = new UserService(_repositoryMock.Object);
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
        [InlineData(101)]
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
    }
}
