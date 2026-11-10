using FamilyVaultApi.Models.Internal.Enums;

namespace FamilyVaultApi.Data.Entities
{
    // Uma leitura do Gmail (etapa 1 do worker). Fica Completed quando a listagem termina; a leitura dos documentos
    // continua depois e o andamento sai de Processed sobre Found.
    public class GmailScan
    {
        public int GmailScanId { get; set; }
        public string UserId { get; set; } = string.Empty;
        public GmailScanKind Kind { get; set; }
        public DateTime WindowStart { get; set; }
        public DateTime WindowEnd { get; set; }
        public GmailScanStatus Status { get; set; }
        public int Found { get; set; }
        public int Processed { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }

        // Navigation properties
        public virtual User User { get; set; } = null!;
    }
}
