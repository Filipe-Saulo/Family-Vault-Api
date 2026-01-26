namespace FamilyVaultApi.Data.Entities
{
    public class TransactionType
    {
        public int TransactionTypeId { get; set; }
        public string Code { get; set; } // 'expense', 'income'
        public string Name { get; set; } // 'Despesa', 'Receita'
        public string Description { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        public virtual ICollection<Transaction> Transactions { get; set; }
    }
}
