using Bogus;
using FamilyVaultApi.Models.Dto.Requests.CategoryPurpose;

namespace FamilyVaultApi.UnitTests.Builders.CategoryPurpose
{
    public class UpdateCategoryPurposeDtoBuilder
    {
        private static readonly Faker Faker = new("pt_BR");

        private string _name = Faker.Commerce.Department();
        private string _description = Faker.Lorem.Sentence();
        private bool _isActive = true;

        public static UpdateCategoryPurposeDtoBuilder New() => new();

        public UpdateCategoryPurposeDtoBuilder WithName(string name)
        {
            _name = name;
            return this;
        }

        public UpdateCategoryPurposeDtoBuilder WithDescription(string description)
        {
            _description = description;
            return this;
        }

        public UpdateCategoryPurposeDtoBuilder WithIsActive(bool isActive)
        {
            _isActive = isActive;
            return this;
        }

        public UpdateCategoryPurposeDto Build() => new()
        {
            Name = _name,
            Description = _description,
            IsActive = _isActive
        };
    }
}
