using FamilyVaultApi.Models.Dto.Responses.CategoryPurpose;
using FamilyVaultApi.Repositories.IRepository;
using FamilyVaultApi.Services.Service;
using FamilyVaultApi.UnitTests.Builders.CategoryPurpose;
using FluentAssertions;
using Moq;

namespace FamilyVaultApi.UnitTests.Services
{
    public class CategoryPurposeServiceTests
    {
        private readonly Mock<ICategoryPurposeRepository> _repositoryMock;
        private readonly CategoryPurposeService _service;

        public CategoryPurposeServiceTests()
        {
            _repositoryMock = new Mock<ICategoryPurposeRepository>();
            _service = new CategoryPurposeService(_repositoryMock.Object);
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
            var dto = CreateCategoryPurposeDtoBuilder.New().Build();
            var expected = new CategoryPurposeResponseDto { CategoryPurposeId = 1, Name = dto.Name };

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
            var expected = new List<CategoryPurposeResponseDto>();

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
            var dto = UpdateCategoryPurposeDtoBuilder.New().Build();
            var expected = new CategoryPurposeResponseDto { CategoryPurposeId = 3 };

            _repositoryMock.Setup(x => x.UpdateAsync(3, dto)).ReturnsAsync(expected);

            var result = await _service.UpdateAsync(3, dto);

            result.Should().Be(expected);
            _repositoryMock.Verify(x => x.UpdateAsync(3, dto), Times.Once);
        }
    }
}
