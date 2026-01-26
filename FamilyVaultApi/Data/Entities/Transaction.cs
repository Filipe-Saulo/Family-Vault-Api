namespace FamilyVaultApi.Data.Entities
{
    public class Transaction
    {
        public int TransactionId { get; set; }
        public string UserId { get; set; }
        public int CategoryId { get; set; }
        public int TransactionTypeId { get; set; }
        public string Description { get; set; }
        public decimal Amount { get; set; }
        public DateTime TransactionDate { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        public virtual User User { get; set; }
        public virtual Category Category { get; set; }
        public virtual TransactionType TransactionType { get; set; }
    }
}
