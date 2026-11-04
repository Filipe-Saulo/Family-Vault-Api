using FamilyVaultApi.Models.Dto.Requests.Category;
using FamilyVaultApi.Models.Dto.Requests.Transaction;
using FamilyVaultApi.Models.Dto.Requests.User;
using FamilyVaultApi.UnitTests.Builders.Category;
using FamilyVaultApi.UnitTests.Builders.Transaction;
using FamilyVaultApi.UnitTests.Builders.User;
using FluentAssertions;
using System.ComponentModel.DataAnnotations;

namespace FamilyVaultApi.UnitTests.Validation
{
    public class DtoValidationTests
    {
        private static List<ValidationResult> Validate(object instance)
        {
            var results = new List<ValidationResult>();
            Validator.TryValidateObject(instance, new ValidationContext(instance), results, validateAllProperties: true);
            return results;
        }

        private static bool Fails(object instance, string member)
            => Validate(instance).Any(r => r.MemberNames.Contains(member));

        // ── CreateTransactionDto / UpdateTransactionDto ──────────────────────

        [Theory]
        [InlineData("0")]
        [InlineData("-5")]
        [InlineData("0.001")]
        public void CreateTransactionDto_WhenAmountIsZeroOrNegative_ShouldFail(string amount)
        {
            var dto = CreateTransactionDtoBuilder.New().Build();
            dto.Amount = decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture);

            Fails(dto, nameof(CreateTransactionDto.Amount)).Should().BeTrue();
        }

        [Fact]
        public void CreateTransactionDto_WhenAmountIsPositive_ShouldNotFailOnAmount()
        {
            var dto = CreateTransactionDtoBuilder.New().Build();
            dto.Amount = 10.50m;

            Fails(dto, nameof(CreateTransactionDto.Amount)).Should().BeFalse();
        }

        [Fact]
        public void CreateTransactionDto_WhenDescriptionIsTooLong_ShouldFail()
        {
            var dto = CreateTransactionDtoBuilder.New().Build();
            dto.Description = new string('x', 501);

            Fails(dto, nameof(CreateTransactionDto.Description)).Should().BeTrue();
        }

        [Fact]
        public void UpdateTransactionDto_WhenAmountIsNegative_ShouldFail()
        {
            var dto = UpdateTransactionDtoBuilder.New().Build();
            dto.Amount = -1m;

            Fails(dto, nameof(UpdateTransactionDto.Amount)).Should().BeTrue();
        }

        // ── Category DTOs ────────────────────────────────────────────────────

        [Fact]
        public void CreateCategoryDto_WhenDescriptionIsTooLong_ShouldFail()
        {
            var dto = CreateCategoryDtoBuilder.New().WithDescription(new string('x', 256)).Build();

            Fails(dto, nameof(CreateCategoryDto.Description)).Should().BeTrue();
        }

        [Fact]
        public void CreateCategoryDto_WhenValid_ShouldPass()
        {
            Validate(CreateCategoryDtoBuilder.New().WithDescription("Mercado").WithCategoryPurposeId(1).Build())
                .Should().BeEmpty();
        }

        [Fact]
        public void UpdateCategoryDto_WhenDescriptionIsTooLong_ShouldFail()
        {
            var dto = UpdateCategoryDtoBuilder.New().Build();
            dto.Description = new string('x', 256);

            Fails(dto, nameof(UpdateCategoryDto.Description)).Should().BeTrue();
        }

        // ── UpdateUserDto / CreateUserRequestDto ─────────────────────────────

        [Theory]
        [InlineData(-1)]
        [InlineData(151)]
        public void UpdateUserDto_WhenAgeIsOutOfRange_ShouldFail(int age)
        {
            var dto = UpdateUserDtoBuilder.New().Build();
            dto.Age = age;

            Fails(dto, nameof(UpdateUserDto.Age)).Should().BeTrue();
        }

        [Theory]
        [InlineData(0)]
        [InlineData(150)]
        public void UpdateUserDto_WhenAgeIsAtTheLimits_ShouldNotFailOnAge(int age)
        {
            var dto = UpdateUserDtoBuilder.New().Build();
            dto.Age = age;

            Fails(dto, nameof(UpdateUserDto.Age)).Should().BeFalse();
        }

        [Fact]
        public void UpdateUserDto_WhenFirstNameIsTooLong_ShouldFail()
        {
            var dto = UpdateUserDtoBuilder.New().Build();
            dto.FirstName = new string('x', 101);

            Fails(dto, nameof(UpdateUserDto.FirstName)).Should().BeTrue();
        }

        private static CreateUserRequestDto NewCreateUser() => new()
        {
            FirstName = "Ana", LastName = "Silva", Age = 30,
            Password = "Senha@123", PasswordConfirm = "Senha@123",
            Role = "User", PhoneNumber = "(11) 98765-4312"
        };

        [Fact]
        public void CreateUserRequestDto_WhenValid_ShouldPass()
        {
            Validate(NewCreateUser()).Should().BeEmpty();
        }

        [Theory]
        [InlineData("Root")]
        [InlineData("administrator")]
        [InlineData("")]
        public void CreateUserRequestDto_WhenRoleIsNotAdministratorOrUser_ShouldFail(string role)
        {
            var dto = NewCreateUser();
            dto.Role = role;

            Fails(dto, nameof(CreateUserRequestDto.Role)).Should().BeTrue();
        }

        [Fact]
        public void CreateUserRequestDto_WhenAgeIsOutOfRange_ShouldFail()
        {
            var dto = NewCreateUser();
            dto.Age = 200;

            Fails(dto, nameof(CreateUserRequestDto.Age)).Should().BeTrue();
        }

        [Fact]
        public void CreateUserRequestDto_WhenPhoneHasInvalidShape_ShouldFail()
        {
            var dto = NewCreateUser();
            dto.PhoneNumber = "abc";

            Fails(dto, nameof(CreateUserRequestDto.PhoneNumber)).Should().BeTrue();
        }

        [Fact]
        public void CreateUserRequestDto_WhenEmailIsTooLong_ShouldFail()
        {
            var dto = NewCreateUser();
            dto.Email = new string('a', 257);

            Fails(dto, nameof(CreateUserRequestDto.Email)).Should().BeTrue();
        }
    }
}
