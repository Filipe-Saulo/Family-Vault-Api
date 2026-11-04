using FamilyVaultApi.Common.Validators.DtoValidators;
using FluentAssertions;
using System.ComponentModel.DataAnnotations;

namespace FamilyVaultApi.UnitTests.Validation
{
    public class PhoneValidatorCustomAttributeTests
    {
        private const string InvalidMessage = "Número de telefone inválido.";
        private const string RequiredMessage = "Número de telefone é obrigatório.";

        private static ValidationResult? Validate(ValidationAttribute attribute, object? value)
            => attribute.GetValidationResult(value, new ValidationContext(new object()) { MemberName = "PhoneNumber" });

        [Fact]
        public void GetValidationResult_WhenValueIsValid_ShouldSucceed()
        {
            Validate(new PhoneValidatorCustomAttribute(), "(11) 98765-4312").Should().Be(ValidationResult.Success);
        }

        [Fact]
        public void GetValidationResult_WhenValueIsInvalid_ShouldReturnInvalidMessage()
        {
            Validate(new PhoneValidatorCustomAttribute(), "abc")!.ErrorMessage.Should().Be(InvalidMessage);
        }

        [Fact]
        public void GetValidationResult_WhenInvalidThenValidThenInvalid_ShouldNotShareStateBetweenCalls()
        {
            var attribute = new PhoneValidatorCustomAttribute();

            Validate(attribute, "abc")!.ErrorMessage.Should().Be(InvalidMessage);
            Validate(attribute, "(11) 98765-4312").Should().Be(ValidationResult.Success);
            Validate(attribute, "xyz")!.ErrorMessage.Should().Be(InvalidMessage);
        }

        [Fact]
        public void GetValidationResult_WhenRequiredAttributeFailsForDifferentReasons_ShouldKeepTheMessageOfEachCall()
        {
            var attribute = new PhoneValidatorCustomRequiredAttribute();

            Validate(attribute, null)!.ErrorMessage.Should().Be(RequiredMessage);
            Validate(attribute, "abc")!.ErrorMessage.Should().Be(InvalidMessage);
            Validate(attribute, "")!.ErrorMessage.Should().Be(RequiredMessage);
        }

        [Fact]
        public void GetValidationResult_WhenRequiredAttributeIsUsedAfterAFailure_ShouldSucceedForValidValue()
        {
            var attribute = new PhoneValidatorCustomRequiredAttribute();

            Validate(attribute, null).Should().NotBe(ValidationResult.Success);
            Validate(attribute, "(11) 98765-4312").Should().Be(ValidationResult.Success);
        }
    }
}
