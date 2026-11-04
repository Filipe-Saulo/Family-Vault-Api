using Bogus;
using FamilyVaultApi.Models.Internal;

namespace FamilyVaultApi.UnitTests.Builders.Category
{
    public class CategoryAccessInfoBuilder
    {
        private static readonly Faker Faker = new("pt_BR");

        private string? _userId;
        private int _categoryPurposeId = Faker.Random.Int(1, 10);

        public static CategoryAccessInfoBuilder New() => new();

        // Global: sem dono (padrão)
        public CategoryAccessInfoBuilder OwnedBy(string? userId)
        {
            _userId = userId;
            return this;
        }

        public CategoryAccessInfoBuilder WithCategoryPurposeId(int categoryPurposeId)
        {
            _categoryPurposeId = categoryPurposeId;
            return this;
        }

        public CategoryAccessInfo Build() => new()
        {
            UserId = _userId,
            CategoryPurposeId = _categoryPurposeId
        };
    }
}
