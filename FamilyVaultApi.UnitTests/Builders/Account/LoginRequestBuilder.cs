using Bogus;
using FamilyVaultApi.Models.Dto.Requests.Account;

namespace FamilyVaultApi.UnitTests.Builders.Account
{
    public class LoginRequestBuilder
    {
        private static readonly Faker Faker = new("pt_BR");

        private string? _phone;
        private string? _email = Faker.Internet.Email();
        private string _password = "Senha123";

        public static LoginRequestBuilder New() => new();

        public LoginRequestBuilder WithPhone(string? phone)
        {
            _phone = phone;
            return this;
        }

        public LoginRequestBuilder WithEmail(string? email)
        {
            _email = email;
            return this;
        }

        public LoginRequestBuilder WithPassword(string password)
        {
            _password = password;
            return this;
        }

        public LoginRequestDto Build() => new()
        {
            Phone = _phone,
            Email = _email,
            Password = _password
        };
    }
}
