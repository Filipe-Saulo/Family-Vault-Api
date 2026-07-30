using FamilyVaultApi.Models.Internal.Enums;

namespace FamilyVaultApi.Models.Dto.Responses.CategoryPurpose
{
    public class CategoryPurposeResponseDto
    {
        public int CategoryPurposeId { get; set; }
        public CategoryPurposeCode Code { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
