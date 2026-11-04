using FamilyVaultApi.Exceptions;
using FamilyVaultApi.Models.Dto.Requests.Category;
using FamilyVaultApi.Models.Dto.Responses.Category;
using FamilyVaultApi.Models.Internal;
using FamilyVaultApi.Models.Internal.Enums;
using FamilyVaultApi.Repositories.IRepository;
using FamilyVaultApi.Services.Service;
using FamilyVaultApi.UnitTests.Builders.Category;
using FamilyVaultApi.UnitTests.Helpers;
using FluentAssertions;
using Moq;
using System.Security;
using System.Security.Claims;

namespace FamilyVaultApi.UnitTests.Services
{
    public class CategoryServiceTests
    {
        private static readonly ClaimsPrincipal Admin = ClaimsPrincipalTestHelper.CreateAdmin("admin-1");
        private static readonly ClaimsPrincipal User = ClaimsPrincipalTestHelper.CreateUser("user-1");
        private static readonly ClaimsPrincipal Manager = ClaimsPrincipalTestHelper.CreateUserWithPermission("user-1", PermissionCode.ManageCategories);

        private readonly Mock<ICategoryRepository> _repositoryMock;
        private readonly CategoryService _service;

        public CategoryServiceTests()
        {
            _repositoryMock = new Mock<ICategoryRepository>();
            _service = new CategoryService(_repositoryMock.Object);
        }

        private void SetUpAccess(int id, string? ownerId, int purposeId = 1)
            => _repositoryMock.Setup(x => x.GetAccessInfoAsync(id))
                .ReturnsAsync(CategoryAccessInfoBuilder.New().OwnedBy(ownerId).WithCategoryPurposeId(purposeId).Build());

        // ── CreateAsync ──────────────────────────────────────────────────────

        [Fact]
        public async Task CreateAsync_WhenDtoIsNull_ShouldThrowArgumentNullException()
        {
            Func<Task> act = () => _service.CreateAsync(null!, Admin);

            await act.Should().ThrowAsync<ArgumentNullException>();
        }

        [Fact]
        public async Task CreateAsync_WhenCallerIsAdministrator_ShouldCreateGlobalCategory()
        {
            var dto = CreateCategoryDtoBuilder.New().Build();
            var expected = new CategoryResponseDto { CategoryId = 1, Description = dto.Description, IsGlobal = true };
            _repositoryMock.Setup(x => x.AddAsync(dto, null)).ReturnsAsync(expected);

            var result = await _service.CreateAsync(dto, Admin);

            result.Should().Be(expected);
            _repositoryMock.Verify(x => x.AddAsync(dto, null), Times.Once);
        }

        [Fact]
        public async Task CreateAsync_WhenCallerIsUser_ShouldCreatePersonalCategoryOwnedByCaller()
        {
            var dto = CreateCategoryDtoBuilder.New().Build();
            var expected = new CategoryResponseDto { CategoryId = 2, Description = dto.Description, IsGlobal = false };
            _repositoryMock.Setup(x => x.AddAsync(dto, "user-1")).ReturnsAsync(expected);

            var result = await _service.CreateAsync(dto, User);

            result.Should().Be(expected);
            _repositoryMock.Verify(x => x.AddAsync(dto, "user-1"), Times.Once);
            _repositoryMock.Verify(x => x.AddAsync(It.IsAny<CreateCategoryDto>(), null), Times.Never);
        }

        [Fact]
        public async Task CreateAsync_WhenCallerHasManageCategoriesButIsNotAdministrator_ShouldStillCreatePersonalCategory()
        {
            var dto = CreateCategoryDtoBuilder.New().Build();

            await _service.CreateAsync(dto, Manager);

            _repositoryMock.Verify(x => x.AddAsync(dto, "user-1"), Times.Once);
        }

        [Fact]
        public async Task CreateAsync_WhenCallerHasNoUid_ShouldThrowSecurityExceptionAndNotCreate()
        {
            var dto = CreateCategoryDtoBuilder.New().Build();

            Func<Task> act = () => _service.CreateAsync(dto, ClaimsPrincipalTestHelper.CreateUnauthenticated());

            await act.Should().ThrowAsync<SecurityException>();
            _repositoryMock.Verify(x => x.AddAsync(It.IsAny<CreateCategoryDto>(), It.IsAny<string?>()), Times.Never);
        }

        // ── GetAllAsync ──────────────────────────────────────────────────────

        [Fact]
        public async Task GetAllAsync_WhenQueryIsNull_ShouldUseDefaultQuery()
        {
            var expected = new PagedResult<CategoryResponseDto>();

            _repositoryMock
                .Setup(x => x.GetAllAsync(It.Is<CategoryQueryRequestDto>(q => q != null), It.IsAny<string?>()))
                .ReturnsAsync(expected);

            var result = await _service.GetAllAsync(null!, Admin);

            result.Should().Be(expected);
            _repositoryMock.Verify(x => x.GetAllAsync(It.IsAny<CategoryQueryRequestDto>(), It.IsAny<string?>()), Times.Once);
        }

        [Fact]
        public async Task GetAllAsync_WhenQueryIsProvided_ShouldPassItThrough()
        {
            var query = new CategoryQueryRequestDto();
            var expected = new PagedResult<CategoryResponseDto>();

            _repositoryMock
                .Setup(x => x.GetAllAsync(It.Is<CategoryQueryRequestDto>(q => ReferenceEquals(q, query)), It.IsAny<string?>()))
                .ReturnsAsync(expected);

            var result = await _service.GetAllAsync(query, Admin);

            result.Should().Be(expected);
        }

        [Fact]
        public async Task GetAllAsync_WhenCallerIsAdministrator_ShouldRequestOnlyGlobalCategories()
        {
            await _service.GetAllAsync(new CategoryQueryRequestDto(), Admin);

            _repositoryMock.Verify(x => x.GetAllAsync(It.IsAny<CategoryQueryRequestDto>(), null), Times.Once);
        }

        [Fact]
        public async Task GetAllAsync_WhenCallerIsUser_ShouldRequestGlobalAndCallerCategories()
        {
            await _service.GetAllAsync(new CategoryQueryRequestDto(), User);

            _repositoryMock.Verify(x => x.GetAllAsync(It.IsAny<CategoryQueryRequestDto>(), "user-1"), Times.Once);
        }

        [Fact]
        public async Task GetAllAsync_WhenCallerHasNoUid_ShouldThrowSecurityException()
        {
            Func<Task> act = () => _service.GetAllAsync(new CategoryQueryRequestDto(), ClaimsPrincipalTestHelper.CreateUnauthenticated());

            await act.Should().ThrowAsync<SecurityException>();
        }

        [Theory]
        [InlineData(0, 1)]
        [InlineData(-5, 1)]
        [InlineData(3, 3)]
        public async Task GetAllAsync_PageNumber_ShouldBeNormalizedBeforeRepository(int input, int expected)
        {
            CategoryQueryRequestDto? captured = null;
            CaptureQuery(q => captured = q);

            await _service.GetAllAsync(new CategoryQueryRequestDto { PageNumber = input }, Admin);

            captured!.PageNumber.Should().Be(expected);
        }

        [Theory]
        [InlineData(0, 20)]
        [InlineData(-1, 20)]
        [InlineData(101, 100)]
        [InlineData(1000, 100)]
        [InlineData(50, 50)]
        [InlineData(100, 100)]
        public async Task GetAllAsync_PageSize_ShouldBeNormalizedBeforeRepository(int input, int expected)
        {
            CategoryQueryRequestDto? captured = null;
            CaptureQuery(q => captured = q);

            await _service.GetAllAsync(new CategoryQueryRequestDto { PageSize = input }, Admin);

            captured!.PageSize.Should().Be(expected);
        }

        private void CaptureQuery(Action<CategoryQueryRequestDto> capture)
            => _repositoryMock.Setup(x => x.GetAllAsync(It.IsAny<CategoryQueryRequestDto>(), It.IsAny<string?>()))
                .Callback<CategoryQueryRequestDto, string?>((q, _) => capture(q))
                .ReturnsAsync(new PagedResult<CategoryResponseDto>());

        // ── GetLookupAsync ───────────────────────────────────────────────────

        [Fact]
        public async Task GetLookupAsync_WhenCallerIsAdministrator_ShouldRequestOnlyGlobalAndReturnList()
        {
            var expected = new List<CategoryResponseDto>
            {
                new() { CategoryId = 1, Description = "Mercado", IsGlobal = true },
                new() { CategoryId = 2, Description = "Salario", IsGlobal = true }
            };
            _repositoryMock.Setup(x => x.GetLookupAsync(null)).ReturnsAsync(expected);

            var result = await _service.GetLookupAsync(Admin);

            result.Should().BeEquivalentTo(expected);
            _repositoryMock.Verify(x => x.GetLookupAsync(null), Times.Once);
        }

        [Fact]
        public async Task GetLookupAsync_WhenCallerIsUser_ShouldRequestGlobalAndCallerCategories()
        {
            await _service.GetLookupAsync(User);

            _repositoryMock.Verify(x => x.GetLookupAsync("user-1"), Times.Once);
            _repositoryMock.Verify(x => x.GetLookupAsync(null), Times.Never);
        }

        // ── UpdateAsync ──────────────────────────────────────────────────────

        [Fact]
        public async Task UpdateAsync_WhenDtoIsNull_ShouldThrowArgumentNullException()
        {
            Func<Task> act = () => _service.UpdateAsync(1, null!, Admin);

            await act.Should().ThrowAsync<ArgumentNullException>();
        }

        [Fact]
        public async Task UpdateAsync_WhenCategoryDoesNotExist_ShouldThrowNotFoundException()
        {
            _repositoryMock.Setup(x => x.GetAccessInfoAsync(5)).ReturnsAsync((CategoryAccessInfo?)null);

            Func<Task> act = () => _service.UpdateAsync(5, UpdateCategoryDtoBuilder.New().Build(), Admin);

            await act.Should().ThrowAsync<NotFoundException>();
            _repositoryMock.Verify(x => x.UpdateAsync(It.IsAny<int>(), It.IsAny<UpdateCategoryDto>()), Times.Never);
        }

        [Fact]
        public async Task UpdateAsync_WhenAdministratorUpdatesGlobalCategory_ShouldReturnRepositoryResult()
        {
            var dto = UpdateCategoryDtoBuilder.New().WithCategoryPurposeId(3).Build();
            var expected = new CategoryResponseDto { CategoryId = 5 };
            SetUpAccess(5, ownerId: null, purposeId: 3);
            _repositoryMock.Setup(x => x.UpdateAsync(5, dto)).ReturnsAsync(expected);

            var result = await _service.UpdateAsync(5, dto, Admin);

            result.Should().Be(expected);
            _repositoryMock.Verify(x => x.UpdateAsync(5, dto), Times.Once);
        }

        [Fact]
        public async Task UpdateAsync_WhenUserWithManageCategoriesUpdatesGlobalCategory_ShouldBeAllowed()
        {
            var dto = UpdateCategoryDtoBuilder.New().WithCategoryPurposeId(3).Build();
            SetUpAccess(5, ownerId: null, purposeId: 3);

            await _service.UpdateAsync(5, dto, Manager);

            _repositoryMock.Verify(x => x.UpdateAsync(5, dto), Times.Once);
        }

        [Fact]
        public async Task UpdateAsync_WhenUserUpdatesGlobalCategory_ShouldThrowSecurityException()
        {
            SetUpAccess(5, ownerId: null);

            Func<Task> act = () => _service.UpdateAsync(5, UpdateCategoryDtoBuilder.New().Build(), User);

            await act.Should().ThrowAsync<SecurityException>();
            _repositoryMock.Verify(x => x.UpdateAsync(It.IsAny<int>(), It.IsAny<UpdateCategoryDto>()), Times.Never);
        }

        [Fact]
        public async Task UpdateAsync_WhenUserUpdatesOwnPersonalCategory_ShouldBeAllowed()
        {
            var dto = UpdateCategoryDtoBuilder.New().WithCategoryPurposeId(3).Build();
            SetUpAccess(5, ownerId: "user-1", purposeId: 3);

            await _service.UpdateAsync(5, dto, User);

            _repositoryMock.Verify(x => x.UpdateAsync(5, dto), Times.Once);
        }

        [Fact]
        public async Task UpdateAsync_WhenUserUpdatesPersonalCategoryOfAnotherUser_ShouldThrowNotFoundException()
        {
            SetUpAccess(5, ownerId: "outro-usuario");

            Func<Task> act = () => _service.UpdateAsync(5, UpdateCategoryDtoBuilder.New().Build(), User);

            await act.Should().ThrowAsync<NotFoundException>();
            _repositoryMock.Verify(x => x.UpdateAsync(It.IsAny<int>(), It.IsAny<UpdateCategoryDto>()), Times.Never);
        }

        [Fact]
        public async Task UpdateAsync_WhenAdministratorUpdatesPersonalCategoryOfAnotherUser_ShouldBeAllowed()
        {
            var dto = UpdateCategoryDtoBuilder.New().WithCategoryPurposeId(3).Build();
            SetUpAccess(5, ownerId: "outro-usuario", purposeId: 3);

            await _service.UpdateAsync(5, dto, Admin);

            _repositoryMock.Verify(x => x.UpdateAsync(5, dto), Times.Once);
        }

        [Fact]
        public async Task UpdateAsync_WhenUserWithManageCategoriesUpdatesPersonalCategoryOfAnotherUser_ShouldBeAllowed()
        {
            var dto = UpdateCategoryDtoBuilder.New().WithCategoryPurposeId(3).Build();
            SetUpAccess(5, ownerId: "outro-usuario", purposeId: 3);

            await _service.UpdateAsync(5, dto, Manager);

            _repositoryMock.Verify(x => x.UpdateAsync(5, dto), Times.Once);
        }

        [Fact]
        public async Task UpdateAsync_WhenPurposeChangesAndCategoryHasTransactions_ShouldThrowConflictException()
        {
            SetUpAccess(5, ownerId: null, purposeId: 1);
            _repositoryMock.Setup(x => x.CountTransactionsAsync(5)).ReturnsAsync(2);

            Func<Task> act = () => _service.UpdateAsync(5, UpdateCategoryDtoBuilder.New().WithCategoryPurposeId(2).Build(), Admin);

            await act.Should().ThrowAsync<ConflictException>();
            _repositoryMock.Verify(x => x.UpdateAsync(It.IsAny<int>(), It.IsAny<UpdateCategoryDto>()), Times.Never);
        }

        [Fact]
        public async Task UpdateAsync_WhenPurposeChangesAndCategoryHasNoTransactions_ShouldUpdate()
        {
            var dto = UpdateCategoryDtoBuilder.New().WithCategoryPurposeId(2).Build();
            SetUpAccess(5, ownerId: null, purposeId: 1);
            _repositoryMock.Setup(x => x.CountTransactionsAsync(5)).ReturnsAsync(0);

            await _service.UpdateAsync(5, dto, Admin);

            _repositoryMock.Verify(x => x.UpdateAsync(5, dto), Times.Once);
        }

        [Fact]
        public async Task UpdateAsync_WhenPurposeIsUnchangedAndCategoryHasTransactions_ShouldUpdateWithoutConflict()
        {
            var dto = UpdateCategoryDtoBuilder.New().WithCategoryPurposeId(1).Build();
            SetUpAccess(5, ownerId: null, purposeId: 1);
            _repositoryMock.Setup(x => x.CountTransactionsAsync(5)).ReturnsAsync(10);

            await _service.UpdateAsync(5, dto, Admin);

            _repositoryMock.Verify(x => x.UpdateAsync(5, dto), Times.Once);
        }

        // ── DeleteAsync ──────────────────────────────────────────────────────

        [Fact]
        public async Task DeleteAsync_WhenCategoryDoesNotExist_ShouldThrowNotFoundException()
        {
            _repositoryMock.Setup(x => x.GetAccessInfoAsync(7)).ReturnsAsync((CategoryAccessInfo?)null);

            Func<Task> act = () => _service.DeleteAsync(7, Admin);

            await act.Should().ThrowAsync<NotFoundException>();
            _repositoryMock.Verify(x => x.DeleteAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task DeleteAsync_WhenAdministratorDeletesGlobalCategoryWithoutTransactions_ShouldDelegateToRepository()
        {
            SetUpAccess(7, ownerId: null);

            await _service.DeleteAsync(7, Admin);

            _repositoryMock.Verify(x => x.DeleteAsync(7), Times.Once);
        }

        [Fact]
        public async Task DeleteAsync_WhenUserDeletesGlobalCategory_ShouldThrowSecurityException()
        {
            SetUpAccess(7, ownerId: null);

            Func<Task> act = () => _service.DeleteAsync(7, User);

            await act.Should().ThrowAsync<SecurityException>();
            _repositoryMock.Verify(x => x.DeleteAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task DeleteAsync_WhenUserWithManageCategoriesDeletesGlobalCategory_ShouldBeAllowed()
        {
            SetUpAccess(7, ownerId: null);

            await _service.DeleteAsync(7, Manager);

            _repositoryMock.Verify(x => x.DeleteAsync(7), Times.Once);
        }

        [Fact]
        public async Task DeleteAsync_WhenUserDeletesOwnPersonalCategory_ShouldBeAllowed()
        {
            SetUpAccess(7, ownerId: "user-1");

            await _service.DeleteAsync(7, User);

            _repositoryMock.Verify(x => x.DeleteAsync(7), Times.Once);
        }

        [Fact]
        public async Task DeleteAsync_WhenUserDeletesPersonalCategoryOfAnotherUser_ShouldThrowNotFoundException()
        {
            SetUpAccess(7, ownerId: "outro-usuario");

            Func<Task> act = () => _service.DeleteAsync(7, User);

            await act.Should().ThrowAsync<NotFoundException>();
            _repositoryMock.Verify(x => x.DeleteAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task DeleteAsync_WhenAdministratorDeletesPersonalCategoryOfAnotherUser_ShouldBeAllowed()
        {
            SetUpAccess(7, ownerId: "outro-usuario");

            await _service.DeleteAsync(7, Admin);

            _repositoryMock.Verify(x => x.DeleteAsync(7), Times.Once);
        }

        [Fact]
        public async Task DeleteAsync_WhenCategoryHasTransactions_ShouldThrowConflictWithCountInMessage()
        {
            SetUpAccess(7, ownerId: null);
            _repositoryMock.Setup(x => x.CountTransactionsAsync(7)).ReturnsAsync(4);

            Func<Task> act = () => _service.DeleteAsync(7, Admin);

            (await act.Should().ThrowAsync<ConflictException>()).WithMessage("*4*");
            _repositoryMock.Verify(x => x.DeleteAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task DeleteAsync_WhenUserLacksPermissionOnCategoryWithTransactions_ShouldFailAuthorizationBeforeConflict()
        {
            SetUpAccess(7, ownerId: null);
            _repositoryMock.Setup(x => x.CountTransactionsAsync(7)).ReturnsAsync(4);

            Func<Task> act = () => _service.DeleteAsync(7, User);

            await act.Should().ThrowAsync<SecurityException>();
        }
    }
}
