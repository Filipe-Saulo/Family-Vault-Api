using Bogus;
using FamilyVaultApi.Models.Dto.Requests.User;

namespace FamilyVaultApi.UnitTests.Builders.User
{
    public class UpdateUserDtoBuilder
    {
        private static readonly Faker Faker = new("pt_BR");

        private string _firstName = Faker.Name.FirstName();
        private string _lastName = Faker.Name.LastName();
        private int _age = Faker.Random.Int(18, 80);

        public static UpdateUserDtoBuilder New() => new();

        public UpdateUserDtoBuilder WithFirstName(string firstName)
        {
            _firstName = firstName;
            return this;
        }

        public UpdateUserDtoBuilder WithLastName(string lastName)
        {
            _lastName = lastName;
            return this;
        }

        public UpdateUserDtoBuilder WithAge(int age)
        {
            _age = age;
            return this;
        }

        public UpdateUserDto Build() => new()
        {
            FirstName = _firstName,
            LastName = _lastName,
            Age = _age
        };
    }
}
