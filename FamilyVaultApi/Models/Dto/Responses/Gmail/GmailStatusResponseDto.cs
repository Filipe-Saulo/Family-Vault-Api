namespace FamilyVaultApi.Models.Dto.Responses.Gmail
{
    public class GmailStatusResponseDto
    {
        // true só com a conexão Active
        public bool Connected { get; set; }
        public string? GoogleEmail { get; set; }
        // Active, Revoked ou nulo sem conexão
        public string? Status { get; set; }
        public DateTime? InitialScanCompletedAt { get; set; }
        public DateTime? SyncedUntil { get; set; }
    }
}
