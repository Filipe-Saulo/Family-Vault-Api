using FamilyVaultApi.Models.Internal.Enums;

namespace FamilyVaultApi.Models.Dto.Requests.CategoryPurpose
{
    public class CreateCategoryPurposeDto
    {
        public CategoryPurposeCode Code { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
    }
}
