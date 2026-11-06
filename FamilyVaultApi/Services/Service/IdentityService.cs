using FamilyVaultApi.Data.Entities;
using FamilyVaultApi.Exceptions;
using FamilyVaultApi.Models.Dto.Requests.Account;
using FamilyVaultApi.Services.IService;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FamilyVaultApi.Services.Service
{
    public class IdentityService : IIdentityService
    {
        private readonly UserManager<User> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IPhoneNumberService _phoneNumberService;

        private readonly IRefreshTokenService _refreshTokenService;
        private const string _passwordResetCodePurpose = "ResetPasswordCode";

        public IdentityService(UserManager<User> userManager, RoleManager<IdentityRole> roleManager, IPhoneNumberService phoneNumberService, IRefreshTokenService refreshTokenService)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _phoneNumberService = phoneNumberService;
            _refreshTokenService = refreshTokenService;
        }

        public async Task<User?> FindUserByLoginAsync(LoginRequestDto loginDto)
        {
            if (!string.IsNullOrEmpty(loginDto.Email))
                return await _userManager.Users.FirstOrDefaultAsync(u => u.UserName == loginDto.Email);

            if (!string.IsNullOrEmpty(loginDto.Phone))
            {
                // O UserName guarda o telefone em E.164; telefone inválido cai no mesmo 401 genérico do login
                if (!_phoneNumberService.TryValidateAndFormat(loginDto.Phone, "BR", out var phone))
                    return null;

                return await _userManager.Users.FirstOrDefaultAsync(u => u.UserName == phone);
            }

            return null;
        }

        public async Task<User?> FindByPhoneAsync(string phoneE164)
        {
            return await _userManager.Users.FirstOrDefaultAsync(u => u.PhoneNumber == phoneE164);
        }

        public async Task<string> GeneratePasswordResetCodeAsync(User user)
        {
            return await _userManager.GenerateUserTokenAsync(user, TokenOptions.DefaultPhoneProvider, _passwordResetCodePurpose);
        }

        public async Task<bool> VerifyPasswordResetCodeAsync(User user, string code)
        {
            return await _userManager.VerifyUserTokenAsync(user, TokenOptions.DefaultPhoneProvider, _passwordResetCodePurpose, code);
        }

        public async Task<IEnumerable<IdentityError>> ResetPasswordAsync(User user, string newPassword)
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, newPassword);
            return result.Succeeded ? Enumerable.Empty<IdentityError>() : result.Errors;
        }

        public async Task<IEnumerable<IdentityError>> ChangePasswordAsync(User user, string currentPassword, string newPassword)
        {
            var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
            return result.Succeeded ? Enumerable.Empty<IdentityError>() : result.Errors;
        }

        public async Task UnlockAsync(User user)
        {
            await _userManager.ResetAccessFailedCountAsync(user);
            await _userManager.SetLockoutEndDateAsync(user, null);
        }

        public async Task<bool> CheckPasswordAsync(User user, string password)
        {
            return await _userManager.CheckPasswordAsync(user, password);
        }

        public async Task UpdateLastLoginAsync(User user)
        {
            user.LastLogin = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);
        }

        public async Task<bool> IsLockedOutAsync(User user)
        {
            return await _userManager.IsLockedOutAsync(user);
        }

        public async Task AccessFailedAsync(User user)
        {
            await _userManager.AccessFailedAsync(user);
        }

        public async Task ResetAccessFailedCountAsync(User user)
        {
            await _userManager.ResetAccessFailedCountAsync(user);
        }

        public async Task<(bool IsAdmin, bool IsUser)> GetRolesAsync(User user)
        {
            var isAdmin = await _userManager.IsInRoleAsync(user, "Administrator");
            var isUser = await _userManager.IsInRoleAsync(user, "User");
            return (isAdmin, isUser);
        }

        public async Task<IList<Claim>> GetUserClaimsAsync(User user)
        {
            return await _userManager.GetClaimsAsync(user);
        }

        public async Task<IList<Claim>> GetRoleClaimsAsync(string roleName)
        {
            var role = await _roleManager.FindByNameAsync(roleName);
            if (role == null)
                return new List<Claim>();

            return await _roleManager.GetClaimsAsync(role);
        }

        public async Task RevokeRefreshTokensAsync(User user)
        {
            await _refreshTokenService.RevokeAllSessionsAsync(user.Id);
            await _userManager.UpdateSecurityStampAsync(user);
        }

        public async Task<User?> FindByNameAsync(string username)
        {
            return await _userManager.FindByNameAsync(username);
        }

        public async Task<User?> FindByIdAsync(string userId)
        {
            return await _userManager.FindByIdAsync(userId);
        }

        public async Task<IEnumerable<IdentityError>> RegisterAdmin(CreateAccountRequestDto createAccountDto)
        {
            var user = new User
            {
                UserName = createAccountDto.Email,
                Email = createAccountDto.Email,
                FirstName = createAccountDto.FirstName,
                LastName = createAccountDto.LastName,
                FullName = $"{createAccountDto.FirstName} {createAccountDto.LastName}",
                PhoneNumber = createAccountDto.PhoneNumber,
                RegisteredAt = DateTime.UtcNow,
                LastLogin = DateTime.UtcNow,
                Age = createAccountDto.Age
            };

            var resultAccount = await _userManager.CreateAsync(user, createAccountDto.Password);

            if (!resultAccount.Succeeded)
                return resultAccount.Errors;

            try
            {
                await _userManager.AddToRoleAsync(user, "Administrator");
            }
            catch (Exception)
            {
                await _userManager.DeleteAsync(user);

                throw;
            }

            return Enumerable.Empty<IdentityError>();
        }

        public async Task<IEnumerable<IdentityError>> RegisterUser(CreateAccountRequestDto createAccountDto, string phone)
        {
            var user = new User
            {
                UserName = phone,
                Email = createAccountDto.Email,
                FirstName = createAccountDto.FirstName,
                LastName = createAccountDto.LastName,
                FullName = $"{createAccountDto.FirstName} {createAccountDto.LastName}",
                PhoneNumber = phone,
                RegisteredAt = DateTime.UtcNow,
                LastLogin = DateTime.UtcNow,
                Age = createAccountDto.Age
            };

            var resultAccount = await _userManager.CreateAsync(user, createAccountDto.Password);

            if (!resultAccount.Succeeded)
                return resultAccount.Errors;

            try
            {
                await _userManager.AddToRoleAsync(user, "User");
            }
            catch (Exception)
            {
                await _userManager.DeleteAsync(user);

                throw;
            }
            return Enumerable.Empty<IdentityError>();
        }

    }
}
