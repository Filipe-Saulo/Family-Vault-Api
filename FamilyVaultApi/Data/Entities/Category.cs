namespace FamilyVaultApi.Data.Entities
{
    public class Category
    {
        public int CategoryId { get; set; }
        public string Description { get; set; }
        public int CategoryPurposeId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        public virtual CategoryPurpose Purpose { get; set; }
        public virtual ICollection<Transaction> Transactions { get; set; }
    }
}