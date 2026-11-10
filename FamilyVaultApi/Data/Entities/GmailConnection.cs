using FamilyVaultApi.Models.Internal.Enums;

namespace FamilyVaultApi.Data.Entities
{
    // Uma por usuário. O refresh token só existe criptografado (GmailTokenProtector).
    public class GmailConnection
    {
        public int GmailConnectionId { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string GoogleEmail { get; set; } = string.Empty;
        public string RefreshTokenEncrypted { get; set; } = string.Empty;
        public GmailConnectionStatus Status { get; set; }
        public DateTime ConnectedAt { get; set; }
        public DateTime? InitialScanCompletedAt { get; set; }
        // Início da última verificação concluída: a próxima busca o que chegou depois dele
        public DateTime? SyncedUntil { get; set; }
        // Última verificação pedida (botão ou conexão): trava de 1 por minuto feita por UPDATE condicional
        public DateTime? LastScanRequestedAt { get; set; }

        // Navigation properties
        public virtual User User { get; set; } = null!;
    }
}
