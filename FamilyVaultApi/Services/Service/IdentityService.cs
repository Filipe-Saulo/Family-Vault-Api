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

        private readonly string _loginProvider = TokenOptions.DefaultProvider;
        private readonly string _refreshTokenPurpose = "RefreshToken";

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

        public async Task<string> CreateRefreshTokenAsync(User user)
        {
            await _userManager.RemoveAuthenticationTokenAsync(user, _loginProvider, _refreshTokenPurpose);

            var newRefreshToken = await _userManager.GenerateUserTokenAsync(user, _loginProvider, _refreshTokenPurpose);

            await _userManager.SetAuthenticationTokenAsync(user, _loginProvider, _refreshTokenPurpose, newRefreshToken);

            return newRefreshToken;
        }

        public async Task<bool> VerifyRefreshTokenAsync(User user, string refreshToken)
        {
            return await _userManager.VerifyUserTokenAsync(user, _loginProvider, _refreshTokenPurpose, refreshToken);
        }

        public async Task<User?> FindByNameAsync(string username)
        {
            return await _userManager.FindByNameAsync(username);
        }

        public async Task RevokeSecurityStampAsync(User user)
        {
            await _userManager.UpdateSecurityStampAsync(user);
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

            await _userManager.RemoveAuthenticationTokenAsync(user, _loginProvider, _refreshTokenPurpose);
            await _userManager.UpdateSecurityStampAsync(user);
        }

        public async Task<bool> AdministratorExistsAsync()
        {
            return (await _userManager.GetUsersInRoleAsync("Administrator")).Any();
        }
    }
}
