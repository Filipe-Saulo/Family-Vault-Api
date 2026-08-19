using FamilyVaultApi.Exceptions;
using FamilyVaultApi.Models.Dto.Requests.User;
using FamilyVaultApi.Models.Dto.Responses.User;
using FamilyVaultApi.Models.Internal;
using FamilyVaultApi.Models.Internal.Enums;
using FamilyVaultApi.Repositories.IRepository;
using FamilyVaultApi.Services.IService;
using System.Security;
using System.Security.Claims;

namespace FamilyVaultApi.Services.Service
{
    public class UserService : IUserService
    {

        private readonly IUserRepository _userRepository;

        public UserService(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }


        public async Task<PagedResult<UserResponseDto>> GetUsersAsync(UserQueryRequestDto query)
        {

            if (query.PageNumber <= 0) query.PageNumber = 1;
            if (query.PageSize <= 0 || query.PageSize > 100) query.PageSize = 20;

            return await _userRepository.GetAllUsersAsync(query);
        }

        public async Task<UserResponseDto> UpdateUserAsync(string userId, UpdateUserDto dto, ClaimsPrincipal userClaims)
        {
            if (dto == null)
                throw new ArgumentNullException(nameof(dto));

            if (userClaims == null || !userClaims.Identity.IsAuthenticated)
                throw new UnauthorizedAccessException("Usuário não autenticado.");

            var currentUserId = userClaims.FindFirst("uid")?.Value;
            var isAdmin = userClaims.IsInRole("Administrator");

            if (!isAdmin && currentUserId != userId)
                throw new SecurityException("Você não tem permissão para acessar este recurso.");

            return await _userRepository.UpdateAsync(userId, dto);
        }

        public async Task DeleteUserAsync(string userId, ClaimsPrincipal userClaims)
        {
            if (userClaims == null || !userClaims.Identity.IsAuthenticated)
                throw new UnauthorizedAccessException("Usuário não autenticado.");

            var currentUserId = userClaims.FindFirst("uid")?.Value;
            var isAdmin = userClaims.IsInRole("Administrator");

            if (!isAdmin && currentUserId != userId)
                throw new SecurityException("Você não tem permissão para acessar este recurso.");

            await _userRepository.DeleteAsync(userId);
        }

        public async Task<List<PermissionCode>> GetPermissionsAsync(string userId)
        {
            return await _userRepository.GetPermissionsAsync(userId);
        }

        public async Task GrantPermissionAsync(string userId, PermissionCode permission)
        {
            await _userRepository.GrantPermissionAsync(userId, permission);
        }

        public async Task RevokePermissionAsync(string userId, PermissionCode permission)
        {
            await _userRepository.RevokePermissionAsync(userId, permission);
        }

    }
}
