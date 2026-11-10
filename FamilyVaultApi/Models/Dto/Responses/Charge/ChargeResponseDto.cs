namespace FamilyVaultApi.Models.Dto.Responses.Charge
{
    public class ChargeResponseDto
    {
        public int ChargeId { get; set; }
        public int DocumentId { get; set; }
        public string Source { get; set; }
        public string? Issuer { get; set; }
        public string? Description { get; set; }
        public decimal? Amount { get; set; }
        public DateTime? DueDate { get; set; }
        public string? Barcode { get; set; }
        public int? SuggestedCategoryId { get; set; }
        public int? PossibleDuplicateTransactionId { get; set; }
        public string Status { get; set; }
        public int? TransactionId { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
