using FamilyVaultApi.Models.Internal.Enums;

namespace FamilyVaultApi.Data.Entities
{
    // Documento recebido (upload ou e-mail) à espera de interpretação. O conteúdo é apagado ao terminar.
    public class InboundDocument
    {
        public int InboundDocumentId { get; set; }
        public string UserId { get; set; } = string.Empty;
        public InboundDocumentSource Source { get; set; }
        // Upload: "upload:" + sha256 (hex) do conteúdo; garante um único documento por arquivo e usuário
        public string ExternalId { get; set; } = string.Empty;
        public string? Sender { get; set; }
        public DateTime ReceivedAt { get; set; }
        public InboundDocumentStatus Status { get; set; }
        public int Attempts { get; set; }
        public string? FailureReason { get; set; }
        public byte[]? TemporaryContent { get; set; }
        public string? ContentType { get; set; }
        public DateTime? ProcessingStartedAt { get; set; }
        public DateTime CreatedAt { get; set; }

        // Navigation properties
        public virtual User User { get; set; } = null!;
        public virtual Charge? Charge { get; set; }
    }
}
