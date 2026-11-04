using FamilyVaultApi.Models.Dto.Requests.Category;
using FamilyVaultApi.Models.Dto.Responses.Category;
using FamilyVaultApi.Models.Internal;
using System.Security.Claims;

namespace FamilyVaultApi.Services.IService
{
    public interface ICategoryService
    {
        Task<CategoryResponseDto> CreateAsync(CreateCategoryDto dto, ClaimsPrincipal userClaims);
        Task<PagedResult<CategoryResponseDto>> GetAllAsync(CategoryQueryRequestDto query, ClaimsPrincipal userClaims);
        Task<List<CategoryResponseDto>> GetLookupAsync(ClaimsPrincipal userClaims);
        Task<CategoryResponseDto> UpdateAsync(int id, UpdateCategoryDto dto, ClaimsPrincipal userClaims);
        Task DeleteAsync(int id, ClaimsPrincipal userClaims);
    }
}
