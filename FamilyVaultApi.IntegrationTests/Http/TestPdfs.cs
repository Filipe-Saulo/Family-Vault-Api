using System.Text;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;
using PdfSharp.Pdf.IO;

namespace FamilyVaultApi.IntegrationTests.Http
{
    // PDFs reais (PDFsharp) para o fluxo de PDF com senha. O marcador vai num comentário do conteúdo da página, que o
    // PDFsharp comprime; o FakeChargeExtractor o acha pelo ReadPageContent depois que a API abre o PDF.
    public static class TestPdfs
    {
        public static byte[] Create(string marker, string? userPassword = null, string? ownerPassword = null)
        {
            using var document = new PdfDocument();
            var page = document.AddPage();
            page.Contents.AppendContent().CreateStream(Encoding.ASCII.GetBytes($"% {marker}\n"));

            if (userPassword != null || ownerPassword != null)
            {
                if (userPassword != null)
                    document.SecuritySettings.UserPassword = userPassword;
                document.SecuritySettings.OwnerPassword = ownerPassword ?? "senha-do-dono";
                document.SecurityHandler.SetEncryptionToV5();
            }

            using var output = new MemoryStream();
            document.Save(output);
            return output.ToArray();
        }

        // Conteúdo descomprimido das páginas; vazio se não for um PDF sem senha legível
        public static string ReadPageContent(byte[] pdf)
        {
            try
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
            catch (Exception)
            {
                return string.Empty;
            }
        }
    }
}
