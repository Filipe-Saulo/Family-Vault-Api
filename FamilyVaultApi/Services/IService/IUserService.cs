using FamilyVaultApi.Models.Dto.Requests.User;
using FamilyVaultApi.Models.Dto.Responses.User;
using FamilyVaultApi.Models.Internal;
using System.Security.Claims;

namespace FamilyVaultApi.Services.IService
{
    public interface IUserService
    {
        Task<PagedResult<UserResponseDto>> GetUsersAsync(UserQueryRequestDto query);
        Task DeleteUserAsync(string userId, ClaimsPrincipal userClaims);
    }
}
