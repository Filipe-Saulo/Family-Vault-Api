using FamilyVaultApi.Data.Entities;
using FamilyVaultApi.Exceptions;
using FamilyVaultApi.Models.Dto.Requests.Account;
using FamilyVaultApi.Models.Internal.Enums;
using FamilyVaultApi.Services.IService;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace FamilyVaultApi.Services.Service
{
    public class IdentityService : IIdentityService
    {
        private readonly UserManager<User> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        private readonly string _loginProvider = TokenOptions.DefaultProvider;
        private const string _legacyRefreshTokenPurpose = "RefreshToken";
        private const string _webRefreshTokenPurpose = "RefreshToken-web";
        private const string _appRefreshTokenPurpose = "RefreshToken-app";

        public IdentityService(UserManager<User> userManager, RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
        }

        public async Task<User?> FindUserByLoginAsync(LoginRequestDto loginDto)
        {
            if (!string.IsNullOrEmpty(loginDto.Email))
                return await _userManager.Users.FirstOrDefaultAsync(u => u.UserName == loginDto.Email);

            if (!string.IsNullOrEmpty(loginDto.Phone))
                return await _userManager.Users.FirstOrDefaultAsync(u => u.UserName == loginDto.Phone);

            return null;
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
            return await _roleManager.GetClaimsAsync(role);
        }

        private static string GetRefreshTokenPurpose(AuthClient client)
        {
            return client == AuthClient.Web ? _webRefreshTokenPurpose : _appRefreshTokenPurpose;
        }

        public async Task<string> CreateRefreshTokenAsync(User user, AuthClient client)
        {
            var purpose = GetRefreshTokenPurpose(client);

            await _userManager.RemoveAuthenticationTokenAsync(user, _loginProvider, purpose);

            var newRefreshToken = await _userManager.GenerateUserTokenAsync(user, _loginProvider, purpose);

            await _userManager.SetAuthenticationTokenAsync(user, _loginProvider, purpose, newRefreshToken);

            return newRefreshToken;
        }

        public async Task<bool> VerifyRefreshTokenAsync(User user, AuthClient client, string refreshToken)
        {
            var purpose = GetRefreshTokenPurpose(client);

            var storedToken = await _userManager.GetAuthenticationTokenAsync(user, _loginProvider, purpose);
            if (storedToken == null || string.IsNullOrEmpty(refreshToken))
                return false;

            var storedBytes = Encoding.UTF8.GetBytes(storedToken);
            var receivedBytes = Encoding.UTF8.GetBytes(refreshToken);
            if (storedBytes.Length != receivedBytes.Length
                || !CryptographicOperations.FixedTimeEquals(storedBytes, receivedBytes))
                return false;

            return await _userManager.VerifyUserTokenAsync(user, _loginProvider, purpose, refreshToken);
        }

        public async Task RevokeRefreshTokensAsync(User user)
        {
            await _userManager.RemoveAuthenticationTokenAsync(user, _loginProvider, _webRefreshTokenPurpose);
            await _userManager.RemoveAuthenticationTokenAsync(user, _loginProvider, _appRefreshTokenPurpose);
            await _userManager.RemoveAuthenticationTokenAsync(user, _loginProvider, _legacyRefreshTokenPurpose);
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

        public async Task LogoutAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                throw new NotFoundException("Usuário não encontrado.", userId);

            await RevokeRefreshTokensAsync(user);
        }

        public async Task<bool> AdministratorExistsAsync()
        {
            return (await _userManager.GetUsersInRoleAsync("Administrator")).Any();
        }
    }
}
