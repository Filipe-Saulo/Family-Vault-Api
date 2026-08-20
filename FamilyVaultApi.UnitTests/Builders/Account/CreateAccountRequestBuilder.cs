using Bogus;
using FamilyVaultApi.Models.Dto.Requests.Account;

namespace FamilyVaultApi.UnitTests.Builders.Account
{
    public class CreateAccountRequestBuilder
    {
        private static readonly Faker Faker = new("pt_BR");

        private string? _phoneNumber;
        private string? _email = Faker.Internet.Email();
        private string _password = "Senha123";
        private string _passwordConfirm = "Senha123";
        private string _firstName = Faker.Name.FirstName();
        private string _lastName = Faker.Name.LastName();
        private int _age = Faker.Random.Int(18, 80);

        public static CreateAccountRequestBuilder New() => new();

        public CreateAccountRequestBuilder WithPhoneNumber(string? phoneNumber)
        {
            _phoneNumber = phoneNumber;
            return this;
        }

        public CreateAccountRequestBuilder WithEmail(string? email)
        {
            _email = email;
            return this;
        }

        public CreateAccountRequestBuilder WithPassword(string password)
        {
            _password = password;
            return this;
        }

        public CreateAccountRequestBuilder WithPasswordConfirm(string passwordConfirm)
        {
            _passwordConfirm = passwordConfirm;
            return this;
        }

        public CreateAccountRequestBuilder WithAge(int age)
        {
            _age = age;
            return this;
        }

        public CreateAccountRequestDto Build() => new()
        {
            PhoneNumber = _phoneNumber!,
            Email = _email!,
            Password = _password,
            PasswordConfirm = _passwordConfirm,
            FirstName = _firstName,
            LastName = _lastName,
            Age = _age
        };
    }
}
