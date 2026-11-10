namespace FamilyVaultApi.Models.Dto.Responses.Gmail
{
    public class GmailScanResponseDto
    {
        public string Kind { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int Found { get; set; }
        public int Processed { get; set; }
    }
}
