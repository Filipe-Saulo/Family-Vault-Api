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
        Task<bool> HasPasswordAsync(User user);
        Task<User?> FindByExternalLoginAsync(string provider, string providerKey);
        Task<User?> FindByEmailAsync(string email);
        // Vincula o login externo e marca o e-mail como confirmado (o provedor já o verificou)
        Task LinkExternalLoginAsync(User user, string provider, string providerKey);
        // Cria User sem senha, com UserName e Email = e-mail confirmado, já vinculado ao login externo
        Task<User> RegisterExternalUserAsync(string email, string firstName, string lastName, string provider, string providerKey);
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
