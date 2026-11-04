using FamilyVaultApi.Models.Internal;
using System.Security.Claims;

namespace FamilyVaultApi.Services.IService
{
    public interface ITokenService
    {
        string GenerateAccessToken(TokenClaimsData data);
        ClaimsPrincipal? GetPrincipalFromExpiredToken(string token);
    }
}
