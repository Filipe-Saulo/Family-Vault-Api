using FamilyVaultApi.Models.Dto.Requests.Category;
using FamilyVaultApi.Models.Dto.Responses.Category;
using FamilyVaultApi.Models.Internal;
using FamilyVaultApi.Models.Internal.Enums;

namespace FamilyVaultApi.Repositories.IRepository
{
    public interface ICategoryRepository
    {
        Task<CategoryResponseDto> AddAsync(CreateCategoryDto dto);
        Task<PagedResult<CategoryResponseDto>> GetAllAsync(CategoryQueryRequestDto query);
        Task<CategoryResponseDto> UpdateAsync(int id, UpdateCategoryDto dto);
        Task DeleteAsync(int id);
        Task<CategoryPurposeCode?> GetPurposeCodeAsync(int categoryId);
    }
}
