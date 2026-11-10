namespace FamilyVaultApi.Models.Dto.Responses.Gmail
{
    public class GmailIgnoredSenderResponseDto
    {
        public int Id { get; set; }
        public string Sender { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
