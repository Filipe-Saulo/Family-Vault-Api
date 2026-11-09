using FamilyVaultApi.Data.Entities;
using FamilyVaultApi.Models.Dto.Requests.Account;
using FamilyVaultApi.Models.Internal.Enums;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;

namespace FamilyVaultApi.Services.IService
{
    public interface IIdentityService
    {
        Task<User?> FindUserByLoginAsync(LoginRequestDto loginDto);
        Task<User?> FindByPhoneAsync(string phoneE164);
        Task<string> GeneratePasswordResetCodeAsync(User user, VerificationChannel channel);
        Task<bool> VerifyPasswordResetCodeAsync(User user, string code, VerificationChannel channel);
        Task<IEnumerable<IdentityError>> ResetPasswordAsync(User user, string newPassword);
        Task<IEnumerable<IdentityError>> ChangePasswordAsync(User user, string currentPassword, string newPassword);
        Task UnlockAsync(User user);
        Task<bool> CheckPasswordAsync(User user, string password);
        Task UpdateLastLoginAsync(User user);
        Task<bool> IsLockedOutAsync(User user);
        Task AccessFailedAsync(User user);
        Task ResetAccessFailedCountAsync(User user);
        Task<(bool IsAdmin, bool IsUser)> GetRolesAsync(User user);
        Task<IList<Claim>> GetUserClaimsAsync(User user);
        Task<IList<Claim>> GetRoleClaimsAsync(string roleName);
        Task RevokeRefreshTokensAsync(User user);
        Task<User?> FindByNameAsync(string username);
        Task<User?> FindByIdAsync(string userId);
        Task<IEnumerable<IdentityError>> RegisterUser(CreateAccountRequestDto createAccountDto, string phone);
        // Cadastro público: o destino (telefone E.164 ou e-mail normalizado) vira o UserName e fica confirmado
        Task<IEnumerable<IdentityError>> RegisterVerifiedUser(CreateAccountRequestDto createAccountDto, string destination, VerificationChannel channel);
        Task<IEnumerable<IdentityError>> RegisterAdmin(CreateAccountRequestDto createAccountDto);
    }
}
