using FamilyVaultApi.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyVaultApi.Data.Configurations
{
    public class GmailScanConfiguration : IEntityTypeConfiguration<GmailScan>
    {
        public void Configure(EntityTypeBuilder<GmailScan> builder)
        {
            builder.ToTable("gmail_scans");

            builder.HasKey(s => s.GmailScanId);
            builder.Property(s => s.GmailScanId).HasColumnName("gmail_scan_id").ValueGeneratedOnAdd();

            builder.Property(s => s.UserId)
                .HasColumnName("user_id")
                .IsRequired()
                .HasMaxLength(450);

            builder.Property(s => s.Kind)
                .HasColumnName("kind")
                .HasConversion<string>()
                .IsRequired()
                .HasMaxLength(20);

            builder.Property(s => s.WindowStart).HasColumnName("window_start").IsRequired();
            builder.Property(s => s.WindowEnd).HasColumnName("window_end").IsRequired();

            builder.Property(s => s.Status)
                .HasColumnName("status")
                .HasConversion<string>()
                .IsRequired()
                .HasMaxLength(20);

            builder.Property(s => s.Found).HasColumnName("found").IsRequired().HasDefaultValue(0);
            builder.Property(s => s.Processed).HasColumnName("processed").IsRequired().HasDefaultValue(0);
            builder.Property(s => s.CreatedAt).HasColumnName("created_at").IsRequired();
            builder.Property(s => s.CompletedAt).HasColumnName("completed_at");

            // Indexes
            builder.HasIndex(s => new { s.Status, s.CreatedAt }).HasDatabaseName("idx_gmail_scans_status_created");
            builder.HasIndex(s => new { s.UserId, s.Status }).HasDatabaseName("idx_gmail_scans_user_status");

            // Foreign keys
            builder.HasOne(s => s.User)
                .WithMany()
                .HasForeignKey(s => s.UserId)
                .HasConstraintName("fk_gmail_scans_user")
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
