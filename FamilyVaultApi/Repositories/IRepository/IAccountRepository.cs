using FamilyVaultApi.Models.Dto.Requests.Account;
using FamilyVaultApi.Models.Dto.Responses.Account;
using FamilyVaultApi.Models.Internal;
using Microsoft.AspNetCore.Identity;

namespace FamilyVaultApi.Repositories.IRepository
{
    public interface IAccountRepository
    {
        Task<AuthResult> Login(LoginRequestDto loginDto);
        Task<AuthResponseDto> RefreshTokenAsync(RefreshTokenRequestDto request);
        Task<IEnumerable<IdentityError>> RegisterUser(CreateAccountRequestDto createAccountDto, string phone);
        Task<IEnumerable<IdentityError>> RegisterAdmin(CreateAccountRequestDto createAccountDto);
        Task LogoutAsync(string userId);
        Task<bool> PhoneExistsAsync(string phoneNumber);
        Task<bool> EmailUserExistsAsync(string email);
        Task ResetPasswordAsync(PasswordResetRequestDto dto, string uid, bool isLogged);
    }
}
