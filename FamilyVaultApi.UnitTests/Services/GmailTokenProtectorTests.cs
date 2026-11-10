using FamilyVaultApi.Models.Internal;
using FamilyVaultApi.Services.Service;
using FluentAssertions;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;

namespace FamilyVaultApi.UnitTests.Services
{
    public class GmailTokenProtectorTests
    {
        private static GmailTokenProtector Create(byte[]? key = null) =>
            new(Options.Create(new GmailOptions { TokenEncryptionKey = Convert.ToBase64String(key ?? RandomNumberGenerator.GetBytes(32)) }));

        [Fact]
        public void ProtectThenUnprotect_ShouldReturnTheOriginalWithoutExposingIt()
        {
            var protector = Create();

            var protectedText = protector.Protect("1//refresh-token");

            protectedText.Should().NotContain("refresh-token");
            protector.Unprotect(protectedText).Should().Be("1//refresh-token");
        }

        [Fact]
        public void Protect_SameValueTwice_ShouldUseDifferentNonces()
        {
            var protector = Create();

            protector.Protect("token").Should().NotBe(protector.Protect("token"));
        }

        [Fact]
        public void Unprotect_TamperedText_ShouldThrow()
        {
            var protector = Create();
            var bytes = Convert.FromBase64String(protector.Protect("token"));
            bytes[^1] ^= 0x01;

            Action act = () => protector.Unprotect(Convert.ToBase64String(bytes));

            act.Should().Throw<CryptographicException>();
        }

        [Fact]
        public void Unprotect_WithAnotherKey_ShouldThrow()
        {
            var protectedText = Create().Protect("token");

            Action act = () => Create().Unprotect(protectedText);

            act.Should().Throw<CryptographicException>();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("nao-e-base64!")]
        [InlineData("c2hvcnQ=")]
        public void ParseKey_InvalidKey_ShouldThrowWithConfigurationHint(string? key)
        {
            Action act = () => GmailTokenProtector.ParseKey(key);

            act.Should().Throw<InvalidOperationException>().WithMessage("*Gmail:TokenEncryptionKey*");
        }
    }
}
