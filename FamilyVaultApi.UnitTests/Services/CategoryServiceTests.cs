using FamilyVaultApi.Models.Dto.Requests.Category;
using FamilyVaultApi.Models.Dto.Responses.Category;
using FamilyVaultApi.Models.Internal;
using FamilyVaultApi.Repositories.IRepository;
using FamilyVaultApi.Services.Service;
using FamilyVaultApi.UnitTests.Builders.Category;
using FluentAssertions;
using Moq;

namespace FamilyVaultApi.UnitTests.Services
{
    public class CategoryServiceTests
    {
        private readonly Mock<ICategoryRepository> _repositoryMock;
        private readonly CategoryService _service;

        public CategoryServiceTests()
        {
            _repositoryMock = new Mock<ICategoryRepository>();
            _service = new CategoryService(_repositoryMock.Object);
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
            var dto = CreateCategoryDtoBuilder.New().Build();
            var expected = new CategoryResponseDto { CategoryId = 1, Description = dto.Description };

            _repositoryMock.Setup(x => x.AddAsync(dto)).ReturnsAsync(expected);

            var result = await _service.CreateAsync(dto);

            result.Should().Be(expected);
            _repositoryMock.Verify(x => x.AddAsync(dto), Times.Once);
        }

        // ── GetAllAsync ──────────────────────────────────────────────────────

        [Fact]
        public async Task GetAllAsync_WhenQueryIsNull_ShouldUseDefaultQuery()
        {
            var expected = new PagedResult<CategoryResponseDto>();

            _repositoryMock
                .Setup(x => x.GetAllAsync(It.Is<CategoryQueryRequestDto>(q => q != null)))
                .ReturnsAsync(expected);

            var result = await _service.GetAllAsync(null!);

            result.Should().Be(expected);
            _repositoryMock.Verify(x => x.GetAllAsync(It.IsAny<CategoryQueryRequestDto>()), Times.Once);
        }

        [Fact]
        public async Task GetAllAsync_WhenQueryIsProvided_ShouldPassItThrough()
        {
            var query = new CategoryQueryRequestDto();
            var expected = new PagedResult<CategoryResponseDto>();

            _repositoryMock
                .Setup(x => x.GetAllAsync(It.Is<CategoryQueryRequestDto>(q => ReferenceEquals(q, query))))
                .ReturnsAsync(expected);

            var result = await _service.GetAllAsync(query);

            result.Should().Be(expected);
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
            var dto = UpdateCategoryDtoBuilder.New().Build();
            var expected = new CategoryResponseDto { CategoryId = 5 };

            _repositoryMock.Setup(x => x.UpdateAsync(5, dto)).ReturnsAsync(expected);

            var result = await _service.UpdateAsync(5, dto);

            result.Should().Be(expected);
            _repositoryMock.Verify(x => x.UpdateAsync(5, dto), Times.Once);
        }

        // ── DeleteAsync ──────────────────────────────────────────────────────

        [Fact]
        public async Task DeleteAsync_ShouldDelegateToRepository()
        {
            await _service.DeleteAsync(7);

            _repositoryMock.Verify(x => x.DeleteAsync(7), Times.Once);
        }
    }
}
