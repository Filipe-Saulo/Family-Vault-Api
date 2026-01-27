using FamilyVaultApi.Models.Dto.Requests.Category;
using FamilyVaultApi.Models.Dto.Responses.Category;
using FamilyVaultApi.Models.Internal;

namespace FamilyVaultApi.Repositories.IRepository
{
    public interface ICategoryRepository
    {
        Task<CategoryResponseDto> AddAsync(CreateCategoryDto dto);
        Task<PagedResult<CategoryResponseDto>> GetAllAsync(CategoryQueryRequestDto query);
        Task DeleteAsync(int id);
    }
}
