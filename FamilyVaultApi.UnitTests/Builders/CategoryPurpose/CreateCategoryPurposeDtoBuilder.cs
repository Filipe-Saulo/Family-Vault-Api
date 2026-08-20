using Bogus;
using FamilyVaultApi.Models.Dto.Requests.CategoryPurpose;
using FamilyVaultApi.Models.Internal.Enums;

namespace FamilyVaultApi.UnitTests.Builders.CategoryPurpose
{
    public class CreateCategoryPurposeDtoBuilder
    {
        private static readonly Faker Faker = new("pt_BR");

        private CategoryPurposeCode _code = CategoryPurposeCode.Expense;
        private string _name = Faker.Commerce.Department();
        private string _description = Faker.Lorem.Sentence();

        public static CreateCategoryPurposeDtoBuilder New() => new();

        public CreateCategoryPurposeDtoBuilder WithCode(CategoryPurposeCode code)
        {
            _code = code;
            return this;
        }

        public CreateCategoryPurposeDtoBuilder WithName(string name)
        {
            _name = name;
            return this;
        }

        public CreateCategoryPurposeDtoBuilder WithDescription(string description)
        {
            _description = description;
            return this;
        }

        public CreateCategoryPurposeDto Build() => new()
        {
            Code = _code,
            Name = _name,
            Description = _description
        };
    }
}
