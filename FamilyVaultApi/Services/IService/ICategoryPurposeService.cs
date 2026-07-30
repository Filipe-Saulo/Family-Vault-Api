using FamilyVaultApi.Models.Dto.Requests.CategoryPurpose;
using FamilyVaultApi.Models.Dto.Responses.CategoryPurpose;

namespace FamilyVaultApi.Services.IService
{
    public interface ICategoryPurposeService
    {
        Task<CategoryPurposeResponseDto> CreateAsync(CreateCategoryPurposeDto dto);
        Task<List<CategoryPurposeResponseDto>> GetAllAsync(bool? isActive);
        Task<CategoryPurposeResponseDto> UpdateAsync(int id, UpdateCategoryPurposeDto dto);
    }
}
