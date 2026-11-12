using FamilyVaultApi.Models.Internal;

namespace FamilyVaultApi.Services.IService
{
    public interface IGoogleIdTokenValidator
    {
        bool IsConfigured { get; }

        // Confere assinatura, iss, aud e exp; token inválido devolve null
        Task<GoogleIdentity?> ValidateAsync(string idToken);
    }
}
