using FamilyVaultApi.Services.Service;
using FluentAssertions;

namespace FamilyVaultApi.UnitTests.Services
{
    public class PhoneNumberServiceTests
    {
        private readonly PhoneNumberService _service = new();

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void TryValidateAndFormat_WhenPhoneIsNullOrWhitespace_ShouldReturnFalse(string? phone)
        {
            var result = _service.TryValidateAndFormat(phone, "BR", out var e164);

            result.Should().BeFalse();
            e164.Should().BeNull();
        }

        [Fact]
        public void TryValidateAndFormat_WhenBrazilianPhoneIsValid_ShouldReturnTrueAndFormatAsE164()
        {
            var result = _service.TryValidateAndFormat("11987654321", "BR", out var e164);

            result.Should().BeTrue();
            e164.Should().Be("+5511987654321");
        }

        [Theory]
        [InlineData("5511123")]
        [InlineData("1100000000")]
        public void TryValidateAndFormat_WhenBrazilianPhoneIsInvalid_ShouldReturnFalse(string phone)
        {
            var result = _service.TryValidateAndFormat(phone, "BR", out var e164);

            result.Should().BeFalse();
            e164.Should().BeNull();
        }

        [Fact]
        public void TryValidateAndFormat_WhenUsPhoneIsValid_ShouldReturnTrueAndFormatAsE164()
        {
            var result = _service.TryValidateAndFormat("+1 202-555-0123", "BR", out var e164);

            result.Should().BeTrue();
            e164.Should().Be("+12025550123");
        }

        [Fact]
        public void TryValidateAndFormat_WhenPortuguesePhoneIsValid_ShouldReturnTrueAndFormatAsE164()
        {
            var result = _service.TryValidateAndFormat("+351 912345678", "BR", out var e164);

            result.Should().BeTrue();
            e164.Should().Be("+351912345678");
        }

        [Fact]
        public void TryValidateAndFormat_WhenPhoneIsUnparsable_ShouldReturnFalse()
        {
            var result = _service.TryValidateAndFormat("not-a-phone", "BR", out var e164);

            result.Should().BeFalse();
            e164.Should().BeNull();
        }
    }
}
