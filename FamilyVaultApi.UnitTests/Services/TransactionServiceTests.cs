using FamilyVaultApi.Exceptions;
using FamilyVaultApi.Models.Dto.Requests.Transaction;
using FamilyVaultApi.Models.Dto.Responses.TransactionResponse;
using FamilyVaultApi.Models.Internal;
using FamilyVaultApi.Models.Internal.Enums;
using FamilyVaultApi.Repositories.IRepository;
using FamilyVaultApi.Services.Service;
using FamilyVaultApi.UnitTests.Builders.Transaction;
using FamilyVaultApi.UnitTests.Helpers;
using FluentAssertions;
using Moq;
using System.Security;

namespace FamilyVaultApi.UnitTests.Services
{
    public class TransactionServiceTests
    {
        private readonly Mock<ITransactionRepository> _repositoryMock;
        private readonly Mock<ICategoryRepository> _categoryRepositoryMock;
        private readonly Mock<ITransactionTypeRepository> _transactionTypeRepositoryMock;
        private readonly TransactionService _service;

        public TransactionServiceTests()
        {
            _repositoryMock = new Mock<ITransactionRepository>();
            _categoryRepositoryMock = new Mock<ICategoryRepository>();
            _transactionTypeRepositoryMock = new Mock<ITransactionTypeRepository>();

            _categoryRepositoryMock.Setup(x => x.GetPurposeCodeAsync(It.IsAny<int>())).ReturnsAsync(CategoryPurposeCode.Expense);
            _transactionTypeRepositoryMock.Setup(x => x.GetCodeAsync(It.IsAny<int>())).ReturnsAsync(TransactionTypeCode.Expense);
            _repositoryMock.Setup(x => x.GetOwnerUserIdAsync(It.IsAny<int>())).ReturnsAsync("user-1");

            _service = new TransactionService(_repositoryMock.Object, _categoryRepositoryMock.Object, _transactionTypeRepositoryMock.Object);
        }

        // ── CreateAsync ──────────────────────────────────────────────────────

        [Fact]
        public async Task CreateAsync_WhenDtoIsNull_ShouldThrowArgumentNullException()
        {
            Func<Task> act = () => _service.CreateAsync(null!, ClaimsPrincipalTestHelper.CreateUser());

            await act.Should().ThrowAsync<ArgumentNullException>();
        }

        [Fact]
        public async Task CreateAsync_AsUser_ShouldOverrideUserIdWithCallerId()
        {
            var dto = CreateTransactionDtoBuilder.New().WithUserId("outro-user").Build();
            var expected = new TransactionResponseDto();

            _repositoryMock
                .Setup(x => x.AddAsync(It.Is<CreateTransactionDto>(d => d.UserId == "user-1")))
                .ReturnsAsync(expected);

            var result = await _service.CreateAsync(dto, ClaimsPrincipalTestHelper.CreateUser("user-1"));

            result.Should().Be(expected);
            dto.UserId.Should().Be("user-1");
            _repositoryMock.Verify(x => x.AddAsync(It.Is<CreateTransactionDto>(d => d.UserId == "user-1")), Times.Once);
        }

        [Fact]
        public async Task CreateAsync_AsAdmin_WithoutUserId_ShouldThrowBadRequestException()
        {
            var dto = CreateTransactionDtoBuilder.New().WithUserId(null).Build();

            Func<Task> act = () => _service.CreateAsync(dto, ClaimsPrincipalTestHelper.CreateAdmin());

            await act.Should().ThrowAsync<BadRequestException>()
                .WithMessage("*UserId*");
        }

        [Fact]
        public async Task CreateAsync_AsAdmin_WithUserId_ShouldKeepProvidedUserId()
        {
            var dto = CreateTransactionDtoBuilder.New().WithUserId("target-user").Build();
            var expected = new TransactionResponseDto();

            _repositoryMock
                .Setup(x => x.AddAsync(It.Is<CreateTransactionDto>(d => d.UserId == "target-user")))
                .ReturnsAsync(expected);

            var result = await _service.CreateAsync(dto, ClaimsPrincipalTestHelper.CreateAdmin());

            result.Should().Be(expected);
        }

        [Fact]
        public async Task CreateAsync_WhenCategoryNotFound_ShouldThrowNotFoundException()
        {
            var dto = CreateTransactionDtoBuilder.New().WithCategoryId(99).Build();
            _categoryRepositoryMock.Setup(x => x.GetPurposeCodeAsync(99)).ReturnsAsync((CategoryPurposeCode?)null);

            Func<Task> act = () => _service.CreateAsync(dto, ClaimsPrincipalTestHelper.CreateAdmin());

            await act.Should().ThrowAsync<NotFoundException>();
        }

        [Fact]
        public async Task CreateAsync_WhenTransactionTypeNotFound_ShouldThrowNotFoundException()
        {
            var dto = CreateTransactionDtoBuilder.New().WithTransactionTypeId(99).Build();
            _transactionTypeRepositoryMock.Setup(x => x.GetCodeAsync(99)).ReturnsAsync((TransactionTypeCode?)null);

            Func<Task> act = () => _service.CreateAsync(dto, ClaimsPrincipalTestHelper.CreateAdmin());

            await act.Should().ThrowAsync<NotFoundException>();
        }

        [Fact]
        public async Task CreateAsync_WhenCategoryPurposeIsIncompatibleWithTransactionType_ShouldThrowBadRequestException()
        {
            var dto = CreateTransactionDtoBuilder.New().WithCategoryId(1).WithTransactionTypeId(2).Build();
            _categoryRepositoryMock.Setup(x => x.GetPurposeCodeAsync(1)).ReturnsAsync(CategoryPurposeCode.Income);
            _transactionTypeRepositoryMock.Setup(x => x.GetCodeAsync(2)).ReturnsAsync(TransactionTypeCode.Expense);

            Func<Task> act = () => _service.CreateAsync(dto, ClaimsPrincipalTestHelper.CreateAdmin());

            await act.Should().ThrowAsync<BadRequestException>();
            _repositoryMock.Verify(x => x.AddAsync(It.IsAny<CreateTransactionDto>()), Times.Never);
        }

        [Fact]
        public async Task CreateAsync_WhenCategoryPurposeMatchesTransactionType_ShouldCreate()
        {
            var dto = CreateTransactionDtoBuilder.New().WithCategoryId(1).WithTransactionTypeId(2).Build();
            var expected = new TransactionResponseDto();
            _categoryRepositoryMock.Setup(x => x.GetPurposeCodeAsync(1)).ReturnsAsync(CategoryPurposeCode.Income);
            _transactionTypeRepositoryMock.Setup(x => x.GetCodeAsync(2)).ReturnsAsync(TransactionTypeCode.Income);
            _repositoryMock.Setup(x => x.AddAsync(dto)).ReturnsAsync(expected);

            var result = await _service.CreateAsync(dto, ClaimsPrincipalTestHelper.CreateAdmin());

            result.Should().Be(expected);
        }

        // ── GetAllAsync ──────────────────────────────────────────────────────

        [Fact]
        public async Task GetAllAsync_WhenQueryIsNull_ShouldUseDefaultQuery()
        {
            var expected = new PagedResult<TransactionResponseDto>();

            _repositoryMock
                .Setup(x => x.GetAllAsync(It.IsAny<TransactionQueryRequestDto>()))
                .ReturnsAsync(expected);

            var result = await _service.GetAllAsync(null!);

            result.Should().Be(expected);
            _repositoryMock.Verify(x => x.GetAllAsync(It.IsAny<TransactionQueryRequestDto>()), Times.Once);
        }

        // ── UpdateAsync ──────────────────────────────────────────────────────

        [Fact]
        public async Task UpdateAsync_WhenDtoIsNull_ShouldThrowArgumentNullException()
        {
            Func<Task> act = () => _service.UpdateAsync(1, null!, ClaimsPrincipalTestHelper.CreateAdmin());

            await act.Should().ThrowAsync<ArgumentNullException>();
        }

        [Fact]
        public async Task UpdateAsync_AsAdmin_ShouldBypassOwnershipAndReturnRepositoryResult()
        {
            var dto = UpdateTransactionDtoBuilder.New().Build();
            var expected = new TransactionResponseDto { TransactionId = 9 };

            _repositoryMock.Setup(x => x.UpdateAsync(9, dto)).ReturnsAsync(expected);

            var result = await _service.UpdateAsync(9, dto, ClaimsPrincipalTestHelper.CreateAdmin());

            result.Should().Be(expected);
            _repositoryMock.Verify(x => x.UpdateAsync(9, dto), Times.Once);
            _repositoryMock.Verify(x => x.GetOwnerUserIdAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task UpdateAsync_WhenCategoryPurposeIsIncompatibleWithTransactionType_ShouldThrowBadRequestException()
        {
            var dto = UpdateTransactionDtoBuilder.New().WithCategoryId(1).WithTransactionTypeId(2).Build();
            _categoryRepositoryMock.Setup(x => x.GetPurposeCodeAsync(1)).ReturnsAsync(CategoryPurposeCode.Income);
            _transactionTypeRepositoryMock.Setup(x => x.GetCodeAsync(2)).ReturnsAsync(TransactionTypeCode.Expense);

            Func<Task> act = () => _service.UpdateAsync(9, dto, ClaimsPrincipalTestHelper.CreateAdmin());

            await act.Should().ThrowAsync<BadRequestException>();
            _repositoryMock.Verify(x => x.UpdateAsync(It.IsAny<int>(), It.IsAny<UpdateTransactionDto>()), Times.Never);
        }

        [Fact]
        public async Task UpdateAsync_WhenCallerIsOwner_ShouldSucceed()
        {
            var dto = UpdateTransactionDtoBuilder.New().Build();
            var expected = new TransactionResponseDto { TransactionId = 9 };
            _repositoryMock.Setup(x => x.GetOwnerUserIdAsync(9)).ReturnsAsync("user-1");
            _repositoryMock.Setup(x => x.UpdateAsync(9, dto)).ReturnsAsync(expected);

            var result = await _service.UpdateAsync(9, dto, ClaimsPrincipalTestHelper.CreateUser("user-1"));

            result.Should().Be(expected);
        }

        [Fact]
        public async Task UpdateAsync_WhenCallerIsNotOwnerAndHasNoPermission_ShouldThrowSecurityException()
        {
            var dto = UpdateTransactionDtoBuilder.New().Build();
            _repositoryMock.Setup(x => x.GetOwnerUserIdAsync(9)).ReturnsAsync("other-user");

            Func<Task> act = () => _service.UpdateAsync(9, dto, ClaimsPrincipalTestHelper.CreateUser("user-1"));

            await act.Should().ThrowAsync<SecurityException>();
            _repositoryMock.Verify(x => x.UpdateAsync(It.IsAny<int>(), It.IsAny<UpdateTransactionDto>()), Times.Never);
        }

        [Fact]
        public async Task UpdateAsync_WhenCallerHasManageTransactionsPermission_ShouldBypassOwnership()
        {
            var dto = UpdateTransactionDtoBuilder.New().Build();
            var expected = new TransactionResponseDto { TransactionId = 9 };
            _repositoryMock.Setup(x => x.GetOwnerUserIdAsync(9)).ReturnsAsync("other-user");
            _repositoryMock.Setup(x => x.UpdateAsync(9, dto)).ReturnsAsync(expected);

            var result = await _service.UpdateAsync(9, dto, ClaimsPrincipalTestHelper.CreateUserWithPermission("user-1", PermissionCode.ManageTransactions));

            result.Should().Be(expected);
            _repositoryMock.Verify(x => x.GetOwnerUserIdAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task UpdateAsync_WhenTransactionNotFoundForNonPrivilegedCaller_ShouldThrowNotFoundException()
        {
            var dto = UpdateTransactionDtoBuilder.New().Build();
            _repositoryMock.Setup(x => x.GetOwnerUserIdAsync(9)).ReturnsAsync((string?)null);

            Func<Task> act = () => _service.UpdateAsync(9, dto, ClaimsPrincipalTestHelper.CreateUser("user-1"));

            await act.Should().ThrowAsync<NotFoundException>();
        }

        // ── DeleteAsync ──────────────────────────────────────────────────────

        [Fact]
        public async Task DeleteAsync_AsAdmin_ShouldBypassOwnershipAndDelegateToRepository()
        {
            await _service.DeleteAsync(3, ClaimsPrincipalTestHelper.CreateAdmin());

            _repositoryMock.Verify(x => x.DeleteAsync(3), Times.Once);
            _repositoryMock.Verify(x => x.GetOwnerUserIdAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task DeleteAsync_WhenCallerIsOwner_ShouldDelegateToRepository()
        {
            _repositoryMock.Setup(x => x.GetOwnerUserIdAsync(3)).ReturnsAsync("user-1");

            await _service.DeleteAsync(3, ClaimsPrincipalTestHelper.CreateUser("user-1"));

            _repositoryMock.Verify(x => x.DeleteAsync(3), Times.Once);
        }

        [Fact]
        public async Task DeleteAsync_WhenCallerIsNotOwnerAndHasNoPermission_ShouldThrowSecurityException()
        {
            _repositoryMock.Setup(x => x.GetOwnerUserIdAsync(3)).ReturnsAsync("other-user");

            Func<Task> act = () => _service.DeleteAsync(3, ClaimsPrincipalTestHelper.CreateUser("user-1"));

            await act.Should().ThrowAsync<SecurityException>();
            _repositoryMock.Verify(x => x.DeleteAsync(It.IsAny<int>()), Times.Never);
        }
    }
}
