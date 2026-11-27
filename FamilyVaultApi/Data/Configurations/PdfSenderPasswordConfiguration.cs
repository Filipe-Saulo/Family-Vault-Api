using FamilyVaultApi.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyVaultApi.Data.Configurations
{
    public class PdfSenderPasswordConfiguration : IEntityTypeConfiguration<PdfSenderPassword>
    {
        public void Configure(EntityTypeBuilder<PdfSenderPassword> builder)
        {
            builder.ToTable("pdf_sender_passwords");

            builder.HasKey(p => p.PdfSenderPasswordId);
            builder.Property(p => p.PdfSenderPasswordId).HasColumnName("pdf_sender_password_id").ValueGeneratedOnAdd();

            builder.Property(p => p.UserId)
                .HasColumnName("user_id")
                .IsRequired()
                .HasMaxLength(450);

            // 254, como em gmail_ignored_senders: com user_id (450) o índice único em nvarchar ocupa 1.408 bytes, dentro dos 1.700 do SQL Server
            builder.Property(p => p.Sender).HasColumnName("sender").IsRequired().HasMaxLength(GmailIgnoredSender.MaxSenderLength);
            builder.Property(p => p.PasswordEncrypted).HasColumnName("password_encrypted").IsRequired().HasMaxLength(500);
            builder.Property(p => p.CreatedAt).HasColumnName("created_at").IsRequired();
            builder.Property(p => p.UpdatedAt).HasColumnName("updated_at").IsRequired();

            // Indexes
            builder.HasIndex(p => new { p.UserId, p.Sender })
                .IsUnique()
                .HasDatabaseName("idx_pdf_sender_passwords_user_sender");

            // Foreign keys
            builder.HasOne(p => p.User)
                .WithMany()
                .HasForeignKey(p => p.UserId)
                .HasConstraintName("fk_pdf_sender_passwords_user")
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
