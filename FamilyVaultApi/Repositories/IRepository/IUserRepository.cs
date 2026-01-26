using FamilyVaultApi.Models.Dto.Requests.User;
using FamilyVaultApi.Models.Dto.Responses.User;
using FamilyVaultApi.Models.Internal;

namespace FamilyVaultApi.Repositories.IRepository
{
    public interface IUserRepository
    {
        Task<PagedResult<UserResponseDto>> GetAllUsersAsync(UserQueryRequestDto query);
        Task DeleteAsync(string userId);
    }
}
