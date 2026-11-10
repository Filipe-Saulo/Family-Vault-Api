namespace FamilyVaultApi.Data.Entities
{
    // Remetente que a pessoa pediu para não ler mais (descartar com ignoreSender); sai da busca no Gmail
    public class GmailIgnoredSender
    {
        // Tamanho máximo de um endereço (RFC 5321); maior que isso não é ignorado
        public const int MaxSenderLength = 254;

        public int GmailIgnoredSenderId { get; set; }
        public string UserId { get; set; } = string.Empty;
        // Endereço em minúsculas
        public string Sender { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }

        // Navigation properties
        public virtual User User { get; set; } = null!;
    }
}
