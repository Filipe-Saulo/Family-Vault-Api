using FamilyVaultApi.Models.Dto.Requests.User;
using FamilyVaultApi.Models.Dto.Responses.User;
using FamilyVaultApi.Models.Internal;
using FamilyVaultApi.Models.Internal.Enums;

namespace FamilyVaultApi.Repositories.IRepository
{
    public interface IUserRepository
    {
        Task<PagedResult<UserResponseDto>> GetAllUsersAsync(UserQueryRequestDto query);
        Task<UserResponseDto> UpdateAsync(string userId, UpdateUserDto dto);
        Task DeleteAsync(string userId);
        Task GrantPermissionAsync(string userId, PermissionCode permission);
        Task RevokePermissionAsync(string userId, PermissionCode permission);
    }
}
