using Bogus;
using FamilyVaultApi.Models.Dto.Requests.TransactionType;
using FamilyVaultApi.Models.Internal.Enums;

namespace FamilyVaultApi.UnitTests.Builders.TransactionType
{
    public class CreateTransactionTypeDtoBuilder
    {
        private static readonly Faker Faker = new("pt_BR");

        private TransactionTypeCode _code = TransactionTypeCode.Expense;
        private string _name = Faker.Commerce.Department();
        private string _description = Faker.Lorem.Sentence();

        public static CreateTransactionTypeDtoBuilder New() => new();

        public CreateTransactionTypeDtoBuilder WithCode(TransactionTypeCode code)
        {
            _code = code;
            return this;
        }

        public CreateTransactionTypeDtoBuilder WithName(string name)
        {
            _name = name;
            return this;
        }

        public CreateTransactionTypeDtoBuilder WithDescription(string description)
        {
            _description = description;
            return this;
        }

        public CreateTransactionTypeDto Build() => new()
        {
            Code = _code,
            Name = _name,
            Description = _description
        };
    }
}
