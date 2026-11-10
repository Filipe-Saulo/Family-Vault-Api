namespace FamilyVaultApi.Models.Internal
{
    public record ChargeCategoryOption(int Id, string Name);

    public class ChargeExtractionInput
    {
        public byte[] Content { get; init; } = [];
        public string ContentType { get; init; } = "";
        public IReadOnlyList<ChargeCategoryOption> ExpenseCategories { get; init; } = [];
    }

    public class ChargeExtractionResult
    {
        public bool IsCharge { get; init; }
        public string? Issuer { get; init; }
        public string? Description { get; init; }
        public decimal? Amount { get; init; }
        public DateTime? DueDate { get; init; }
        public string? Barcode { get; init; }
        public int? SuggestedCategoryId { get; init; }
    }
}
