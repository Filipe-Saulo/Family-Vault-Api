using FamilyVaultApi.Models.Dto.Requests.Category;
using FamilyVaultApi.Models.Dto.Responses.Category;
using FamilyVaultApi.Models.Internal;
using FamilyVaultApi.Repositories.IRepository;
using FamilyVaultApi.Services.IService;

namespace FamilyVaultApi.Services.Service
{
    public class CategoryService : ICategoryService
    {
        private readonly ICategoryRepository _categoryRepository;

        public CategoryService(ICategoryRepository repository)
        {
            _categoryRepository = repository;
        }

        public async Task<CategoryResponseDto> CreateAsync(CreateCategoryDto dto)
        {
            if (dto == null)
                throw new ArgumentNullException(nameof(dto));

            return await _categoryRepository.AddAsync(dto);
        }

        public async Task<PagedResult<CategoryResponseDto>> GetAllAsync(CategoryQueryRequestDto query)
        {
            query ??= new CategoryQueryRequestDto();
            return await _categoryRepository.GetAllAsync(query);
        }

        public async Task<CategoryResponseDto> UpdateAsync(int id, UpdateCategoryDto dto)
        {
            if (dto == null)
                throw new ArgumentNullException(nameof(dto));

            return await _categoryRepository.UpdateAsync(id, dto);
        }

        public async Task DeleteAsync(int id)
        {
            await _categoryRepository.DeleteAsync(id);
        }
    }
}
