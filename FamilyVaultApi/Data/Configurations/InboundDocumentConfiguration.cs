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

            // longblob no MySQL (o padrão do provedor para byte[] seria um varbinary menor)
            builder.Property(d => d.TemporaryContent).HasColumnName("temporary_content").HasColumnType("longblob");

            builder.Property(d => d.ContentType).HasColumnName("content_type").HasMaxLength(50);

            builder.Property(d => d.ProcessingStartedAt).HasColumnName("processing_started_at");

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
        }
    }
}
