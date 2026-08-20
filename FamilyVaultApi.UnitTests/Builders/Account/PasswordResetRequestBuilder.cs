using Bogus;
using FamilyVaultApi.Models.Dto.Requests.Account;

namespace FamilyVaultApi.UnitTests.Builders.Account
{
    public class PasswordResetRequestBuilder
    {
        private static readonly Faker Faker = new("pt_BR");

        private string _phone = "5511987654321";
        private string _password = Faker.Internet.Password();
        private string _passwordConfirm;

        public PasswordResetRequestBuilder()
        {
            _passwordConfirm = _password;
        }

        public static PasswordResetRequestBuilder New() => new();

        public PasswordResetRequestBuilder WithPhone(string phone)
        {
            _phone = phone;
            return this;
        }

        public PasswordResetRequestBuilder WithPassword(string password)
        {
            _password = password;
            return this;
        }

        public PasswordResetRequestBuilder WithPasswordConfirm(string passwordConfirm)
        {
            _passwordConfirm = passwordConfirm;
            return this;
        }

        public PasswordResetRequestDto Build() => new()
        {
            Phone = _phone,
            Password = _password,
            PasswordConfirm = _passwordConfirm
        };
    }
}
