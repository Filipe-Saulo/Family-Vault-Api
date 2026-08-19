using FamilyVaultApi.Models.Dto.Requests.User;
using FamilyVaultApi.Models.Dto.Responses.User;
using FamilyVaultApi.Models.Internal;
using FamilyVaultApi.Models.Internal.Enums;
using System.Security.Claims;

namespace FamilyVaultApi.Services.IService
{
    public interface IUserService
    {
        Task<PagedResult<UserResponseDto>> GetUsersAsync(UserQueryRequestDto query);
        Task<UserResponseDto> UpdateUserAsync(string userId, UpdateUserDto dto, ClaimsPrincipal userClaims);
        Task DeleteUserAsync(string userId, ClaimsPrincipal userClaims);
        Task GrantPermissionAsync(string userId, PermissionCode permission);
        Task RevokePermissionAsync(string userId, PermissionCode permission);
    }
}
