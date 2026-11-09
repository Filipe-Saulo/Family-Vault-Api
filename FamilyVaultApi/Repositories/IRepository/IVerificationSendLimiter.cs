using FamilyVaultApi.Models.Internal.Enums;

namespace FamilyVaultApi.Repositories.IRepository
{
    public interface IVerificationSendLimiter
    {
        // Registra o envio e devolve true; devolve false se o destino atingiu o limite, se o SMS não é para +55
        // ou se o teto global diário de SMS foi atingido
        Task<bool> TryAcquireAsync(VerificationChannel channel, string destination);
    }
}
