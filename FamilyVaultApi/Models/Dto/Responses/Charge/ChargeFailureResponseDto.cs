namespace FamilyVaultApi.Models.Dto.Responses.Charge
{
    public class ChargeFailureResponseDto
    {
        public int DocumentId { get; set; }
        public string Source { get; set; }
        public string? Sender { get; set; }
        public DateTime ReceivedAt { get; set; }
        public string? FailureReason { get; set; }
    }
}
