using Bogus;
using FamilyVaultApi.Models.Dto.Requests.TransactionType;

namespace FamilyVaultApi.UnitTests.Builders.TransactionType
{
    public class UpdateTransactionTypeDtoBuilder
    {
        private static readonly Faker Faker = new("pt_BR");

        private string _name = Faker.Commerce.Department();
        private string _description = Faker.Lorem.Sentence();
        private bool _isActive = true;

        public static UpdateTransactionTypeDtoBuilder New() => new();

        public UpdateTransactionTypeDtoBuilder WithName(string name)
        {
            _name = name;
            return this;
        }

        public UpdateTransactionTypeDtoBuilder WithDescription(string description)
        {
            _description = description;
            return this;
        }

        public UpdateTransactionTypeDtoBuilder WithIsActive(bool isActive)
        {
            _isActive = isActive;
            return this;
        }

        public UpdateTransactionTypeDto Build() => new()
        {
            Name = _name,
            Description = _description,
            IsActive = _isActive
        };
    }
}
