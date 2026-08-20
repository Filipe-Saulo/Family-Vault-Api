using Bogus;
using FamilyVaultApi.Models.Dto.Requests.Transaction;

namespace FamilyVaultApi.UnitTests.Builders.Transaction
{
    public class UpdateTransactionDtoBuilder
    {
        private static readonly Faker Faker = new("pt_BR");

        private int _categoryId = Faker.Random.Int(1, 10);
        private int _transactionTypeId = Faker.Random.Int(1, 5);
        private string _description = Faker.Commerce.ProductName();
        private decimal _amount = Faker.Finance.Amount();
        private DateTime _transactionDate = Faker.Date.Recent();

        public static UpdateTransactionDtoBuilder New() => new();

        public UpdateTransactionDtoBuilder WithCategoryId(int categoryId)
        {
            _categoryId = categoryId;
            return this;
        }

        public UpdateTransactionDtoBuilder WithTransactionTypeId(int transactionTypeId)
        {
            _transactionTypeId = transactionTypeId;
            return this;
        }

        public UpdateTransactionDtoBuilder WithDescription(string description)
        {
            _description = description;
            return this;
        }

        public UpdateTransactionDtoBuilder WithAmount(decimal amount)
        {
            _amount = amount;
            return this;
        }

        public UpdateTransactionDtoBuilder WithTransactionDate(DateTime transactionDate)
        {
            _transactionDate = transactionDate;
            return this;
        }

        public UpdateTransactionDto Build() => new()
        {
            CategoryId = _categoryId,
            TransactionTypeId = _transactionTypeId,
            Description = _description,
            Amount = _amount,
            TransactionDate = _transactionDate
        };
    }
}
