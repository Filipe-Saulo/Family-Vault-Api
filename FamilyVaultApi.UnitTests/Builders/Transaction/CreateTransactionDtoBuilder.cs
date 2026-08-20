using Bogus;
using FamilyVaultApi.Models.Dto.Requests.Transaction;

namespace FamilyVaultApi.UnitTests.Builders.Transaction
{
    public class CreateTransactionDtoBuilder
    {
        private static readonly Faker Faker = new("pt_BR");

        private string? _userId = Faker.Random.Guid().ToString();
        private int _categoryId = Faker.Random.Int(1, 10);
        private int _transactionTypeId = Faker.Random.Int(1, 5);
        private string _description = Faker.Commerce.ProductName();
        private decimal _amount = Faker.Finance.Amount();
        private DateTime _transactionDate = Faker.Date.Recent();

        public static CreateTransactionDtoBuilder New() => new();

        public CreateTransactionDtoBuilder WithUserId(string? userId)
        {
            _userId = userId;
            return this;
        }

        public CreateTransactionDtoBuilder WithCategoryId(int categoryId)
        {
            _categoryId = categoryId;
            return this;
        }

        public CreateTransactionDtoBuilder WithTransactionTypeId(int transactionTypeId)
        {
            _transactionTypeId = transactionTypeId;
            return this;
        }

        public CreateTransactionDtoBuilder WithDescription(string description)
        {
            _description = description;
            return this;
        }

        public CreateTransactionDtoBuilder WithAmount(decimal amount)
        {
            _amount = amount;
            return this;
        }

        public CreateTransactionDtoBuilder WithTransactionDate(DateTime transactionDate)
        {
            _transactionDate = transactionDate;
            return this;
        }

        public CreateTransactionDto Build() => new()
        {
            UserId = _userId,
            CategoryId = _categoryId,
            TransactionTypeId = _transactionTypeId,
            Description = _description,
            Amount = _amount,
            TransactionDate = _transactionDate
        };
    }
}
