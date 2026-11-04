using FamilyVaultApi.Models.Dto.Requests.Account;
using FamilyVaultApi.Models.Dto.Requests.User;
using System.Security.Claims;

namespace FamilyVaultApi.Services.IService
{
    public interface IPasswordService
    {
        Task ForgotPasswordAsync(ForgotPasswordRequestDto dto);
        Task ResetPasswordAsync(ResetPasswordWithCodeRequestDto dto);
        Task ChangePasswordAsync(ChangePasswordRequestDto dto, ClaimsPrincipal userClaims);
        Task SetUserPasswordAsync(string userId, SetUserPasswordRequestDto dto);
    }
}
