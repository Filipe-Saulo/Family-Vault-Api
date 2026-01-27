using FamilyVaultApi.Models.Dto.Responses.Category;
using FamilyVaultApi.Models.Dto.Responses.TransactionType;

namespace FamilyVaultApi.Models.Dto.Responses.TransactionResponse
{
    public class TransactionResponseDto
    {
        public int TransactionId { get; set; }
        public string Description { get; set; }
        public decimal Amount { get; set; }
        public DateTime TransactionDate { get; set; }


        public string UserId { get; set; }


        public CategorySimpleDto Category { get; set; }
        public TransactionTypeSimpleDto TransactionType { get; set; }


        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
