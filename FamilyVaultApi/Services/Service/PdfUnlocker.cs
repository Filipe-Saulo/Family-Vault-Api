using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace FamilyVaultApi.Services.Service
{
    // Abre um PDF com senha e devolve uma cópia sem criptografia para a extração. PDFsharp 6.2.x (MIT; a 6.5 mudou
    // para AGPL). O modo Import aceita a senha do usuário (a do dono não é conhecida); as páginas são copiadas para
    // um documento novo, salvo sem criptografia. A senha vazia abre o PDF que só tem senha de dono.
    public static class PdfUnlocker
    {
        // null = a senha não abre o PDF ou ele não pôde ser lido. A senha nunca vai para log.
        public static byte[]? TryUnlock(byte[] content, string? password)
        {
            try
            {
                using var source = PdfReader.Open(new MemoryStream(content, writable: false), password ?? string.Empty, PdfDocumentOpenMode.Import);
                using var unlocked = new PdfDocument();
                foreach (var page in source.Pages)
                    unlocked.AddPage(page);

                using var output = new MemoryStream();
                unlocked.Save(output);
                return output.ToArray();
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                return null;
            }
        }
    }
}
