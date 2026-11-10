using FamilyVaultApi.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyVaultApi.Data.Configurations
{
    public class GmailConnectionConfiguration : IEntityTypeConfiguration<GmailConnection>
    {
        public void Configure(EntityTypeBuilder<GmailConnection> builder)
        {
            builder.ToTable("gmail_connections");

            builder.HasKey(c => c.GmailConnectionId);
            builder.Property(c => c.GmailConnectionId).HasColumnName("gmail_connection_id").ValueGeneratedOnAdd();

            builder.Property(c => c.UserId)
                .HasColumnName("user_id")
                .IsRequired()
                .HasMaxLength(450);

            builder.Property(c => c.GoogleEmail).HasColumnName("google_email").IsRequired().HasMaxLength(320);

            // base64 de nonce + texto cifrado + tag (AES-GCM)
            builder.Property(c => c.RefreshTokenEncrypted).HasColumnName("refresh_token_encrypted").IsRequired().HasMaxLength(1024);

            builder.Property(c => c.Status)
                .HasColumnName("status")
                .HasConversion<string>()
                .IsRequired()
                .HasMaxLength(20);

            builder.Property(c => c.ConnectedAt).HasColumnName("connected_at").IsRequired();
            builder.Property(c => c.InitialScanCompletedAt).HasColumnName("initial_scan_completed_at");
            builder.Property(c => c.SyncedUntil).HasColumnName("synced_until");
            builder.Property(c => c.LastScanRequestedAt).HasColumnName("last_scan_requested_at");

            // Indexes
            builder.HasIndex(c => c.UserId)
                .IsUnique()
                .HasDatabaseName("idx_gmail_connections_user");

            // Foreign keys
            builder.HasOne(c => c.User)
                .WithMany()
                .HasForeignKey(c => c.UserId)
                .HasConstraintName("fk_gmail_connections_user")
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
