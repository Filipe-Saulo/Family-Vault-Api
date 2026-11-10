using FamilyVaultApi.Models.Internal.Enums;

namespace FamilyVaultApi.Data.Entities
{
    // Conta proposta pela interpretação do documento; só vira transação quando o usuário confirma
    public class Charge
    {
        public int ChargeId { get; set; }
        public string UserId { get; set; } = string.Empty;
        public int InboundDocumentId { get; set; }
        public string? Issuer { get; set; }
        public string? Description { get; set; }
        public decimal? Amount { get; set; }
        public DateTime? DueDate { get; set; }
        public string? Barcode { get; set; }
        public int? SuggestedCategoryId { get; set; }
        public int? PossibleDuplicateTransactionId { get; set; }
        public ChargeStatus Status { get; set; }
        public int? TransactionId { get; set; }
        public DateTime CreatedAt { get; set; }

        // Navigation properties
        public virtual User User { get; set; } = null!;
        public virtual InboundDocument InboundDocument { get; set; } = null!;
        public virtual Category? SuggestedCategory { get; set; }
        public virtual Transaction? PossibleDuplicateTransaction { get; set; }
        public virtual Transaction? Transaction { get; set; }
    }
}
