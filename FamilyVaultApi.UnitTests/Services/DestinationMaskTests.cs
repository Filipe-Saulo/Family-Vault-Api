using FamilyVaultApi.Common;
using FluentAssertions;

namespace FamilyVaultApi.UnitTests.Services
{
    public class DestinationMaskTests
    {
        [Fact]
        public void Mask_WhenPhone_ShouldKeepOnlyLastFourDigits()
        {
            DestinationMask.Mask("+5511987654312").Should().Be("**********4312");
        }

        [Fact]
        public void Mask_WhenEmail_ShouldKeepFirstLetterAndDomain()
        {
            DestinationMask.Mask("fulano@dominio.com").Should().Be("f***@dominio.com");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("1234")]
        [InlineData("12")]
        public void Mask_WhenEmptyOrTooShort_ShouldHideEverything(string? value)
        {
            DestinationMask.Mask(value).Should().Be("****");
        }

        [Fact]
        public void Mask_WhenPhone_ShouldNotLeakTheFullNumber()
        {
            DestinationMask.Mask("+5511987654312").Should().NotContain("98765");
        }
    }
}
