using FamilyVaultApi.Models.Internal.Enums;

namespace FamilyVaultApi.Models.Dto.Responses.Dashboard
{
    public class CategorySummaryItemDto
    {
        public int CategoryId { get; set; }
        public string CategoryDescription { get; set; }
        public int TransactionTypeId { get; set; }
        public TransactionTypeCode TransactionTypeCode { get; set; }
        public string TransactionTypeName { get; set; }
        public decimal Total { get; set; }
    }
}
