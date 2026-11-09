using Bogus;
using FamilyVaultApi.Models.Dto.Requests.Account;

namespace FamilyVaultApi.UnitTests.Builders.Account
{
    public class RegisterAccountRequestBuilder
    {
        private static readonly Faker Faker = new("pt_BR");

        private string? _phoneNumber;
        private string? _email;
        private string _code = "123456";
        private string _password = "Senha123";
        private string _passwordConfirm = "Senha123";
        private string _firstName = Faker.Name.FirstName();
        private string _lastName = Faker.Name.LastName();
        private int _age = Faker.Random.Int(18, 80);

        public static RegisterAccountRequestBuilder New() => new();

        public RegisterAccountRequestBuilder WithPhoneNumber(string? phoneNumber)
        {
            _phoneNumber = phoneNumber;
            return this;
        }

        public RegisterAccountRequestBuilder WithEmail(string? email)
        {
            _email = email;
            return this;
        }

        public RegisterAccountRequestBuilder WithCode(string code)
        {
            _code = code;
            return this;
        }

        public RegisterAccountRequestBuilder WithPassword(string password)
        {
            _password = password;
            return this;
        }

        public RegisterAccountRequestBuilder WithPasswordConfirm(string passwordConfirm)
        {
            _passwordConfirm = passwordConfirm;
            return this;
        }

        public RegisterAccountRequestDto Build() => new()
        {
            PhoneNumber = _phoneNumber,
            Email = _email,
            Code = _code,
            Password = _password,
            PasswordConfirm = _passwordConfirm,
            FirstName = _firstName,
            LastName = _lastName,
            Age = _age
        };
    }
}
