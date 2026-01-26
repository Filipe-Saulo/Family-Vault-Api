namespace FamilyVaultApi.Data.Entities
{
    public class CategoryPurpose
    {
        public int CategoryPurposeId { get; set; }
        public string Code { get; set; } 
        public string Name { get; set; } 
        public string Description { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
      
        public virtual ICollection<Category> Categories { get; set; }
    }
}
