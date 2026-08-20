using FamilyVaultApi.Exceptions;
using FamilyVaultApi.Models.Dto.Requests.Transaction;
using FamilyVaultApi.Models.Dto.Responses.TransactionResponse;
using FamilyVaultApi.Models.Internal;
using FamilyVaultApi.Repositories.IRepository;
using FamilyVaultApi.Services.Service;
using FamilyVaultApi.UnitTests.Builders.Transaction;
using FamilyVaultApi.UnitTests.Helpers;
using FluentAssertions;
using Moq;

namespace FamilyVaultApi.UnitTests.Services
{
    public class TransactionServiceTests
    {
        private readonly Mock<ITransactionRepository> _repositoryMock;
        private readonly TransactionService _service;

        public TransactionServiceTests()
        {
            _repositoryMock = new Mock<ITransactionRepository>();
            _service = new TransactionService(_repositoryMock.Object);
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
            Func<Task> act = () => _service.UpdateAsync(1, null!);

            await act.Should().ThrowAsync<ArgumentNullException>();
        }

        [Fact]
        public async Task UpdateAsync_WhenDtoIsValid_ShouldReturnRepositoryResult()
        {
            var dto = UpdateTransactionDtoBuilder.New().Build();
            var expected = new TransactionResponseDto { TransactionId = 9 };

            _repositoryMock.Setup(x => x.UpdateAsync(9, dto)).ReturnsAsync(expected);

            var result = await _service.UpdateAsync(9, dto);

            result.Should().Be(expected);
            _repositoryMock.Verify(x => x.UpdateAsync(9, dto), Times.Once);
        }

        // ── DeleteAsync ──────────────────────────────────────────────────────

        [Fact]
        public async Task DeleteAsync_ShouldDelegateToRepository()
        {
            await _service.DeleteAsync(3);

            _repositoryMock.Verify(x => x.DeleteAsync(3), Times.Once);
        }
    }
}
