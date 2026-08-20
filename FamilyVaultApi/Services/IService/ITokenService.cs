using FamilyVaultApi.Models.Internal;

namespace FamilyVaultApi.Services.IService
{
    public interface ITokenService
    {
        string GenerateAccessToken(TokenClaimsData data);
    }
}
