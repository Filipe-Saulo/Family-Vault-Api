using FamilyVaultApi.Data.Entities;
using FamilyVaultApi.Models.Dto.Requests.Account;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;

namespace FamilyVaultApi.Services.IService
{
    public interface IIdentityService
    {
        Task<User?> FindUserByLoginAsync(LoginRequestDto loginDto);
        Task<bool> CheckPasswordAsync(User user, string password);
        Task UpdateLastLoginAsync(User user);
        Task<(bool IsAdmin, bool IsUser)> GetRolesAsync(User user);
        Task<IList<Claim>> GetUserClaimsAsync(User user);
        Task<IList<Claim>> GetRoleClaimsAsync(string roleName);
        Task<string> CreateRefreshTokenAsync(User user);
        Task<bool> VerifyRefreshTokenAsync(User user, string refreshToken);
        Task<User?> FindByNameAsync(string username);
        Task RevokeSecurityStampAsync(User user);
        Task<IEnumerable<IdentityError>> RegisterUser(CreateAccountRequestDto createAccountDto, string phone);
        Task<IEnumerable<IdentityError>> RegisterAdmin(CreateAccountRequestDto createAccountDto);
        Task LogoutAsync(string userId);
        Task<bool> AdministratorExistsAsync();
    }
}
