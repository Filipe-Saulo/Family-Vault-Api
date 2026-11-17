using System.Text;
using FamilyVaultApi.Services.Service;
using FamilyVaultApi.UnitTests.Helpers;
using FluentAssertions;

namespace FamilyVaultApi.UnitTests.Services
{
    // PDFs reais criptografados pelo PDFsharp: o mesmo caminho das faturas protegidas com parte do CPF
    public class PdfUnlockerTests
    {
        [Theory]
        [InlineData(TestPdfs.Encryption.Rc4With128Bits)]
        [InlineData(TestPdfs.Encryption.Aes128)]
        [InlineData(TestPdfs.Encryption.Aes256)]
        public void TryUnlock_RightUserPassword_ShouldReturnPdfWithoutEncryptionKeepingTheContent(TestPdfs.Encryption encryption)
        {
            var locked = TestPdfs.Create("fatura-123", userPassword: "12345", encryption: encryption);

            var unlocked = PdfUnlocker.TryUnlock(locked, "12345");

            unlocked.Should().NotBeNull();
            TestPdfs.IsEncrypted(unlocked!).Should().BeFalse();
            TestPdfs.ReadPageContent(unlocked!).Should().Contain("fatura-123");
        }

        [Theory]
        [InlineData("")]
        [InlineData("00000")]
        public void TryUnlock_WrongPassword_ShouldReturnNull(string password)
        {
            var locked = TestPdfs.Create("fatura", userPassword: "12345");

            PdfUnlocker.TryUnlock(locked, password).Should().BeNull();
        }

        [Fact]
        public void TryUnlock_OwnerPasswordOnlyWithEmptyPassword_ShouldUnlock()
        {
            var locked = TestPdfs.Create("so-dono", ownerPassword: "dono");

            var unlocked = PdfUnlocker.TryUnlock(locked, string.Empty);

            unlocked.Should().NotBeNull();
            TestPdfs.IsEncrypted(unlocked!).Should().BeFalse();
            TestPdfs.ReadPageContent(unlocked!).Should().Contain("so-dono");
        }

        [Fact]
        public void TryUnlock_UnreadableBytes_ShouldReturnNullWithoutThrowing()
        {
            var garbage = Encoding.ASCII.GetBytes("%PDF-1.7 << /Encrypt 5 0 R >>");

            PdfUnlocker.TryUnlock(garbage, "12345").Should().BeNull();
        }
    }
}
