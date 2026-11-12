using FamilyVaultApi.Data.Entities;
using FamilyVaultApi.Exceptions;
using FamilyVaultApi.Models.Dto.Requests.Account;
using FamilyVaultApi.Models.Internal.Enums;
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
                // FindByNameAsync compara pelo campo normalizado: maiúsculas digitadas ou salvas não impedem o login
                return await _userManager.FindByNameAsync(loginDto.Email.Trim());

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

        public async Task<string> GeneratePasswordResetCodeAsync(User user, VerificationChannel channel)
        {
            return await _userManager.GenerateUserTokenAsync(user, TokenProviderFor(channel), _passwordResetCodePurpose);
        }

        public async Task<bool> VerifyPasswordResetCodeAsync(User user, string code, VerificationChannel channel)
        {
            return await _userManager.VerifyUserTokenAsync(user, TokenProviderFor(channel), _passwordResetCodePurpose, code);
        }

        private static string TokenProviderFor(VerificationChannel channel)
        {
            return channel == VerificationChannel.Email ? TokenOptions.DefaultEmailProvider : TokenOptions.DefaultPhoneProvider;
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

        public async Task<bool> HasPasswordAsync(User user)
        {
            return await _userManager.HasPasswordAsync(user);
        }

        public async Task<User?> FindByExternalLoginAsync(string provider, string providerKey)
        {
            return await _userManager.FindByLoginAsync(provider, providerKey);
        }

        public async Task<User?> FindByEmailAsync(string email)
        {
            // Quem se cadastrou por e-mail tem o e-mail no UserName e no Email; o Email cobre os dois casos.
            // O e-mail não é único no Identity (RequireUniqueEmail desligado): com mais de uma conta, não há qual vincular
            var normalized = _userManager.NormalizeEmail(email);
            var users = await _userManager.Users.Where(u => u.NormalizedEmail == normalized).Take(2).ToListAsync();
            if (users.Count > 1)
                throw new ConflictException("Este e-mail está em mais de uma conta. Entre com telefone ou e-mail e senha.");

            return users.SingleOrDefault();
        }

        public async Task LinkExternalLoginAsync(User user, string provider, string providerKey)
        {
            var result = await _userManager.AddLoginAsync(user, new UserLoginInfo(provider, providerKey, provider));
            if (!result.Succeeded)
                throw new InvalidOperationException($"Falha ao vincular o login {provider}: {string.Join(", ", result.Errors.Select(e => e.Code))}");

            if (!user.EmailConfirmed)
            {
                user.EmailConfirmed = true;
                await _userManager.UpdateAsync(user);
            }
        }

        public async Task<User> RegisterExternalUserAsync(string email, string firstName, string lastName, string provider, string providerKey)
        {
            var user = new User
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FirstName = firstName,
                LastName = lastName,
                FullName = $"{firstName} {lastName}".Trim(),
                RegisteredAt = DateTime.UtcNow,
                LastLogin = DateTime.UtcNow,
                Age = 0
            };

            // Sem senha: a primeira é definida pelo "esqueci a senha" por e-mail
            var created = await _userManager.CreateAsync(user);
            if (!created.Succeeded)
                throw new InvalidOperationException($"Falha ao criar a conta {provider}: {string.Join(", ", created.Errors.Select(e => e.Code))}");

            try
            {
                var linked = await _userManager.AddLoginAsync(user, new UserLoginInfo(provider, providerKey, provider));
                if (!linked.Succeeded)
                    throw new InvalidOperationException($"Falha ao vincular o login {provider}: {string.Join(", ", linked.Errors.Select(e => e.Code))}");

                var role = await _userManager.AddToRoleAsync(user, "User");
                if (!role.Succeeded)
                    throw new InvalidOperationException($"Falha ao atribuir o papel User: {string.Join(", ", role.Errors.Select(e => e.Code))}");
            }
            catch (Exception)
            {
                await _userManager.DeleteAsync(user);
                throw;
            }

            return user;
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

        public async Task<IEnumerable<IdentityError>> RegisterVerifiedUser(CreateAccountRequestDto createAccountDto, string destination, VerificationChannel channel)
        {
            var isEmail = channel == VerificationChannel.Email;
            var user = new User
            {
                UserName = destination,
                Email = isEmail ? destination : null,
                EmailConfirmed = isEmail,
                PhoneNumber = isEmail ? null : destination,
                PhoneNumberConfirmed = !isEmail,
                FirstName = createAccountDto.FirstName,
                LastName = createAccountDto.LastName,
                FullName = $"{createAccountDto.FirstName} {createAccountDto.LastName}",
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
