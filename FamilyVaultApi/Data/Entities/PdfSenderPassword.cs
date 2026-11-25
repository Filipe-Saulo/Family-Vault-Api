namespace FamilyVaultApi.Data.Entities
{
    // Senha de PDF que a pessoa pediu para lembrar ("lembrar para este remetente"); fatura nova do remetente tenta
    // esta senha antes de pedir. Cifrada como o refresh token do Gmail.
    public class PdfSenderPassword
    {
        public int PdfSenderPasswordId { get; set; }
        public string UserId { get; set; } = string.Empty;
        // Endereço em minúsculas, como em gmail_ignored_senders
        public string Sender { get; set; } = string.Empty;
        public string PasswordEncrypted { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // Navigation properties
        public virtual User User { get; set; } = null!;
    }
}
