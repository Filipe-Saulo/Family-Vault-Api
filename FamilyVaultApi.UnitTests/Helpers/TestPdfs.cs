using System.Text;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;
using PdfSharp.Pdf.IO;

namespace FamilyVaultApi.UnitTests.Helpers
{
    // PDFs reais (PDFsharp) para os testes do fluxo de PDF com senha. O marcador vai num comentário do conteúdo da
    // página, que o PDFsharp comprime: para achá-lo depois de aberto, use ReadPageContent.
    public static class TestPdfs
    {
        public enum Encryption { Rc4With128Bits, Aes128, Aes256 }

        public static byte[] Create(string marker, string? userPassword = null, string? ownerPassword = null, Encryption encryption = Encryption.Aes256)
        {
            using var document = new PdfDocument();
            var page = document.AddPage();
            page.Contents.AppendContent().CreateStream(Encoding.ASCII.GetBytes($"% {marker}\n"));

            if (userPassword != null || ownerPassword != null)
            {
                if (userPassword != null)
                    document.SecuritySettings.UserPassword = userPassword;
                document.SecuritySettings.OwnerPassword = ownerPassword ?? "senha-do-dono";

                switch (encryption)
                {
                    case Encryption.Rc4With128Bits: document.SecurityHandler.SetEncryptionToV2With128Bits(); break;
                    case Encryption.Aes128: document.SecurityHandler.SetEncryptionToV4UsingAES(); break;
                    default: document.SecurityHandler.SetEncryptionToV5(); break;
                }
            }

            using var output = new MemoryStream();
            document.Save(output);
            return output.ToArray();
        }

        // Conteúdo descomprimido das páginas de um PDF sem senha
        public static string ReadPageContent(byte[] pdf)
        {
            using var document = PdfReader.Open(new MemoryStream(pdf), PdfDocumentOpenMode.Import);
            var text = new StringBuilder();
            foreach (var page in document.Pages)
            {
                foreach (var item in page.Contents.Elements)
                {
                    var stream = item is PdfReference reference ? reference.Value as PdfDictionary : item as PdfDictionary;
                    if (stream?.Stream != null)
                        text.Append(Encoding.Latin1.GetString(stream.Stream.UnfilteredValue));
                }
            }
            return text.ToString();
        }

        public static bool IsEncrypted(byte[] pdf) => Encoding.Latin1.GetString(pdf).Contains("/Encrypt");
    }
}
