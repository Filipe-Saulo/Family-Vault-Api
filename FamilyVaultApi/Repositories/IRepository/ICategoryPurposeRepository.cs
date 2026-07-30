using FamilyVaultApi.Models.Dto.Requests.CategoryPurpose;
using FamilyVaultApi.Models.Dto.Responses.CategoryPurpose;

namespace FamilyVaultApi.Repositories.IRepository
{
    public interface ICategoryPurposeRepository
    {
        Task<CategoryPurposeResponseDto> AddAsync(CreateCategoryPurposeDto dto);
        Task<List<CategoryPurposeResponseDto>> GetAllAsync(bool? isActive);
        Task<CategoryPurposeResponseDto> UpdateAsync(int id, UpdateCategoryPurposeDto dto);
    }
}
