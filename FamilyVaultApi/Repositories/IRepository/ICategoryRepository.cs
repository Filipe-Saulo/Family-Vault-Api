using FamilyVaultApi.Models.Dto.Requests.Category;
using FamilyVaultApi.Models.Dto.Responses.Category;
using FamilyVaultApi.Models.Internal;
using FamilyVaultApi.Models.Internal.Enums;

namespace FamilyVaultApi.Repositories.IRepository
{
    public interface ICategoryRepository
    {
        Task<CategoryResponseDto> AddAsync(CreateCategoryDto dto, string? userId);
        Task<PagedResult<CategoryResponseDto>> GetAllAsync(CategoryQueryRequestDto query, string? ownerUserId);
        Task<List<CategoryResponseDto>> GetLookupAsync(string? ownerUserId);
        Task<CategoryResponseDto> UpdateAsync(int id, UpdateCategoryDto dto);
        Task DeleteAsync(int id);
        Task<CategoryPurposeCode?> GetPurposeCodeAsync(int categoryId);
        Task<CategoryAccessInfo?> GetAccessInfoAsync(int categoryId);
        Task<int> CountTransactionsAsync(int categoryId);
        Task<bool> IsAvailableToUserAsync(int categoryId, string userId);
    }
}
