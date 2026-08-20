using Bogus;
using FamilyVaultApi.Models.Dto.Requests.Category;

namespace FamilyVaultApi.UnitTests.Builders.Category
{
    public class UpdateCategoryDtoBuilder
    {
        private static readonly Faker Faker = new("pt_BR");

        private string _description = Faker.Commerce.Department();
        private int _categoryPurposeId = Faker.Random.Int(1, 10);

        public static UpdateCategoryDtoBuilder New() => new();

        public UpdateCategoryDtoBuilder WithDescription(string description)
        {
            _description = description;
            return this;
        }

        public UpdateCategoryDtoBuilder WithCategoryPurposeId(int categoryPurposeId)
        {
            _categoryPurposeId = categoryPurposeId;
            return this;
        }

        public UpdateCategoryDto Build() => new()
        {
            Description = _description,
            CategoryPurposeId = _categoryPurposeId
        };
    }
}
