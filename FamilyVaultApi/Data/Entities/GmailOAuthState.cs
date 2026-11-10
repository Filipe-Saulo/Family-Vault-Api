namespace FamilyVaultApi.Data.Entities
{
    // State do fluxo OAuth: uso único, ligado ao usuário, com prazo. Guarda só o sha256 do valor enviado ao Google.
    public class GmailOAuthState
    {
        public int GmailOAuthStateId { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string StateHash { get; set; } = string.Empty;
        public string ReturnUrl { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }

        // Navigation properties
        public virtual User User { get; set; } = null!;
    }
}
