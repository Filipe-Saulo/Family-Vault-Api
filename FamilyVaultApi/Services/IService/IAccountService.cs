using FamilyVaultApi.Models.Dto.Requests.Account;
using FamilyVaultApi.Models.Dto.Responses.Account;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;

namespace FamilyVaultApi.Services.IService
{
    public interface IAccountService
    {
        Task<IEnumerable<IdentityError>> RegisterAsync(CreateAccountRequestDto dto);
        Task<AuthResponseDto> LoginAsync(LoginRequestDto loginDto);
        Task<AuthResponseDto> RefreshTokenAsync(RefreshTokenRequestDto request);
        Task LogoutAsync(string? token = null);
        Task ResetPasswordAsync(PasswordResetRequestDto dto, ClaimsPrincipal userClaims);      

    }
}
