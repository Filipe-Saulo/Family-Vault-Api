using FamilyVaultApi.Models.Dto.Responses.TransactionType;
using FamilyVaultApi.Repositories.IRepository;
using FamilyVaultApi.Services.Service;
using FamilyVaultApi.UnitTests.Builders.TransactionType;
using FluentAssertions;
using Moq;

namespace FamilyVaultApi.UnitTests.Services
{
    public class TransactionTypeServiceTests
    {
        private readonly Mock<ITransactionTypeRepository> _repositoryMock;
        private readonly TransactionTypeService _service;

        public TransactionTypeServiceTests()
        {
            _repositoryMock = new Mock<ITransactionTypeRepository>();
            _service = new TransactionTypeService(_repositoryMock.Object);
        }

        // ── CreateAsync ──────────────────────────────────────────────────────

        [Fact]
        public async Task CreateAsync_WhenDtoIsNull_ShouldThrowArgumentNullException()
        {
            Func<Task> act = () => _service.CreateAsync(null!);

            await act.Should().ThrowAsync<ArgumentNullException>();
        }

        [Fact]
        public async Task CreateAsync_WhenDtoIsValid_ShouldReturnRepositoryResult()
        {
            var dto = CreateTransactionTypeDtoBuilder.New().Build();
            var expected = new TransactionTypeResponseDto { TransactionTypeId = 1, Name = dto.Name };

            _repositoryMock.Setup(x => x.AddAsync(dto)).ReturnsAsync(expected);

            var result = await _service.CreateAsync(dto);

            result.Should().Be(expected);
            _repositoryMock.Verify(x => x.AddAsync(dto), Times.Once);
        }

        // ── GetAllAsync ──────────────────────────────────────────────────────

        [Theory]
        [InlineData(null)]
        [InlineData(true)]
        [InlineData(false)]
        public async Task GetAllAsync_ShouldPassIsActiveThrough(bool? isActive)
        {
            var expected = new List<TransactionTypeResponseDto>();

            _repositoryMock.Setup(x => x.GetAllAsync(isActive)).ReturnsAsync(expected);

            var result = await _service.GetAllAsync(isActive);

            result.Should().BeSameAs(expected);
            _repositoryMock.Verify(x => x.GetAllAsync(isActive), Times.Once);
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
            var dto = UpdateTransactionTypeDtoBuilder.New().Build();
            var expected = new TransactionTypeResponseDto { TransactionTypeId = 4 };

            _repositoryMock.Setup(x => x.UpdateAsync(4, dto)).ReturnsAsync(expected);

            var result = await _service.UpdateAsync(4, dto);

            result.Should().Be(expected);
            _repositoryMock.Verify(x => x.UpdateAsync(4, dto), Times.Once);
        }
    }
}
