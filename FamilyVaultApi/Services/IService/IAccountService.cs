using FamilyVaultApi.Models.Dto.Requests.Account;
using FamilyVaultApi.Models.Dto.Responses.Account;
using FamilyVaultApi.Models.Internal.Enums;
using Microsoft.AspNetCore.Identity;

namespace FamilyVaultApi.Services.IService
{
    public interface IAccountService
    {
        Task SendRegistrationCodeAsync(RegistrationCodeRequestDto dto);
        Task<IEnumerable<IdentityError>> RegisterAsync(RegisterAccountRequestDto dto);
        Task<AuthResponseDto> LoginAsync(LoginRequestDto loginDto, AuthClient client);
        // Login com Google, só no app: valida o ID token e cria ou vincula a conta User
        Task<AuthResponseDto> LoginWithGoogleAsync(GoogleLoginRequestDto dto);
        Task<AuthResponseDto> RefreshTokenAsync(RefreshTokenRequestDto request, AuthClient client);
        Task LogoutAsync(string? refreshToken);
    }
}
