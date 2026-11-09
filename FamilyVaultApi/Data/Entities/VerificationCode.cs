using FamilyVaultApi.Models.Internal.Enums;

namespace FamilyVaultApi.Data.Entities
{
    // Código pendente de um destino: pedir outro substitui a linha
    public class VerificationCode
    {
        public string Destination { get; set; } = string.Empty;
        public VerificationPurpose Purpose { get; set; }
        public string CodeHash { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public int FailedAttempts { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
