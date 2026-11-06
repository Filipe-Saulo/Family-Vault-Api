using FamilyVaultApi.Models.Internal.Enums;

namespace FamilyVaultApi.Data.Entities
{
    // Uma linha por token emitido. O FamilyId é a sessão: nasce no login e passa de token em token na rotação.
    public class RefreshToken
    {
        public long RefreshTokenId { get; set; }
        public string TokenHash { get; set; } = string.Empty;
        public string FamilyId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public AuthClient Client { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public DateTime? ConsumedAt { get; set; }
        public DateTime? RevokedAt { get; set; }

        public virtual User User { get; set; } = null!;
    }
}
