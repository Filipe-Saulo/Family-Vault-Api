using FamilyVaultApi.Models.Internal.Enums;

namespace FamilyVaultApi.Models.Dto.Responses.CategoryPurpose
{
    public class CategoryPurposeSimpleDto
    {
        public int CategoryPurposeId { get; set; }
        public string Name { get; set; }
        public CategoryPurposeCode Code { get; set; }
    }
}
