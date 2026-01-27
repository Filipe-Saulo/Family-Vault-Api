using FamilyVaultApi.Models.Dto.Responses.CategoryPurpose;

namespace FamilyVaultApi.Models.Dto.Responses.Category
{
    public class CategorySimpleDto
    {
        public int CategoryId { get; set; }
        public string Description { get; set; }


        public CategoryPurposeSimpleDto Purpose { get; set; }
    }
}
