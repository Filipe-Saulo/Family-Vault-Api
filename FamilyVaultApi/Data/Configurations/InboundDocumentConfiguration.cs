using FamilyVaultApi.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyVaultApi.Data.Configurations
{
    public class InboundDocumentConfiguration : IEntityTypeConfiguration<InboundDocument>
    {
        public void Configure(EntityTypeBuilder<InboundDocument> builder)
        {
            builder.ToTable("inbound_documents");

            builder.HasKey(d => d.InboundDocumentId);
            builder.Property(d => d.InboundDocumentId).HasColumnName("inbound_document_id").ValueGeneratedOnAdd();

            builder.Property(d => d.UserId)
                .HasColumnName("user_id")
                .IsRequired()
                .HasMaxLength(450);

            builder.Property(d => d.Source)
                .HasColumnName("source")
                .HasConversion<string>()
                .IsRequired()
                .HasMaxLength(20);

            builder.Property(d => d.ExternalId)
                .HasColumnName("external_id")
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(d => d.Sender).HasColumnName("sender").HasMaxLength(320);

            builder.Property(d => d.ReceivedAt).HasColumnName("received_at").IsRequired();

            builder.Property(d => d.Status)
                .HasColumnName("status")
                .HasConversion<string>()
                .IsRequired()
                .HasMaxLength(20);

            builder.Property(d => d.Attempts).HasColumnName("attempts").IsRequired().HasDefaultValue(0);

            builder.Property(d => d.FailureReason).HasColumnName("failure_reason").HasMaxLength(300);

            // varbinary(max), o padrão do SQL Server para byte[] sem tamanho: o arquivo inteiro (até Charges:MaxUploadBytes)
            // fica aqui até a extração. Sem HasColumnType para o SQLite dos testes também aceitar
            builder.Property(d => d.TemporaryContent).HasColumnName("temporary_content");

            builder.Property(d => d.ContentType).HasColumnName("content_type").HasMaxLength(50);

            builder.Property(d => d.ProcessingStartedAt).HasColumnName("processing_started_at");

            builder.Property(d => d.GmailScanId).HasColumnName("gmail_scan_id");

            builder.Property(d => d.PasswordAttempts).HasColumnName("password_attempts").IsRequired().HasDefaultValue(0);

            builder.Property(d => d.PasswordRequiredAt).HasColumnName("password_required_at");

            builder.Property(d => d.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Indexes
            builder.HasIndex(d => new { d.UserId, d.ExternalId })
                .IsUnique()
                .HasDatabaseName("idx_inbound_documents_user_external");
            builder.HasIndex(d => new { d.Status, d.CreatedAt }).HasDatabaseName("idx_inbound_documents_status_created");

            // Foreign keys
            builder.HasOne(d => d.User)
                .WithMany()
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("fk_inbound_documents_user")
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(d => d.GmailScan)
                .WithMany()
                .HasForeignKey(d => d.GmailScanId)
                .HasConstraintName("fk_inbound_documents_gmail_scan")
                // ClientSetNull: SET NULL no banco seria um segundo caminho a partir de tb_user (via gmail_scans), que o
                // SQL Server recusa. Varreduras só saem com o usuário, e o UserRepository zera a coluna antes.
                .OnDelete(DeleteBehavior.ClientSetNull);
        }
    }
}
