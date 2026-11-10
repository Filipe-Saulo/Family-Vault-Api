using FamilyVaultApi.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyVaultApi.Data.Configurations
{
    public class GmailIgnoredSenderConfiguration : IEntityTypeConfiguration<GmailIgnoredSender>
    {
        public void Configure(EntityTypeBuilder<GmailIgnoredSender> builder)
        {
            builder.ToTable("gmail_ignored_senders");

            builder.HasKey(s => s.GmailIgnoredSenderId);
            builder.Property(s => s.GmailIgnoredSenderId).HasColumnName("gmail_ignored_sender_id").ValueGeneratedOnAdd();

            builder.Property(s => s.UserId)
                .HasColumnName("user_id")
                .IsRequired()
                .HasMaxLength(450);

            // 254: tamanho máximo de um endereço (RFC 5321); com user_id (450) o índice único cabe nos 3072 bytes do InnoDB em utf8mb4
            builder.Property(s => s.Sender).HasColumnName("sender").IsRequired().HasMaxLength(GmailIgnoredSender.MaxSenderLength);
            builder.Property(s => s.CreatedAt).HasColumnName("created_at").IsRequired();

            // Indexes
            builder.HasIndex(s => new { s.UserId, s.Sender })
                .IsUnique()
                .HasDatabaseName("idx_gmail_ignored_senders_user_sender");

            // Foreign keys
            builder.HasOne(s => s.User)
                .WithMany()
                .HasForeignKey(s => s.UserId)
                .HasConstraintName("fk_gmail_ignored_senders_user")
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
