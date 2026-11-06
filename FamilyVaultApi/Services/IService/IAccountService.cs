using FamilyVaultApi.Models.Dto.Requests.Account;
using FamilyVaultApi.Models.Dto.Responses.Account;
using FamilyVaultApi.Models.Internal.Enums;
using Microsoft.AspNetCore.Identity;

namespace FamilyVaultApi.Services.IService
{
    public interface IAccountService
    {
        Task<IEnumerable<IdentityError>> RegisterAsync(CreateAccountRequestDto dto);
        Task<AuthResponseDto> LoginAsync(LoginRequestDto loginDto, AuthClient client);
        Task<AuthResponseDto> RefreshTokenAsync(RefreshTokenRequestDto request, AuthClient client);
        Task LogoutAsync(string? refreshToken);
    }
}
