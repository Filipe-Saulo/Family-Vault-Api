using FamilyVaultApi.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyVaultApi.Data.Configurations
{
    public class VerificationSendLimitConfiguration : IEntityTypeConfiguration<VerificationSendLimit>
    {
        public void Configure(EntityTypeBuilder<VerificationSendLimit> builder)
        {
            builder.ToTable("tb_verification_send_limits");

            builder.HasKey(l => l.Destination);

            builder.Property(l => l.Destination)
                .HasColumnName("destination")
                .HasMaxLength(254);

            builder.Property(l => l.LastSentAt)
                .HasColumnName("last_sent_at")
                .IsRequired();

            builder.Property(l => l.DayWindowStartedAt)
                .HasColumnName("day_window_started_at")
                .IsRequired();

            builder.Property(l => l.DayCount)
                .HasColumnName("day_count")
                .IsRequired();

            // Indexes
            builder.HasIndex(l => l.DayWindowStartedAt).HasDatabaseName("idx_verification_send_limits_window");
        }
    }
}
