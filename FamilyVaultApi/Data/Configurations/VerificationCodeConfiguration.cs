using FamilyVaultApi.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyVaultApi.Data.Configurations
{
    public class VerificationCodeConfiguration : IEntityTypeConfiguration<VerificationCode>
    {
        public void Configure(EntityTypeBuilder<VerificationCode> builder)
        {
            builder.ToTable("tb_verification_codes");

            builder.HasKey(c => new { c.Destination, c.Purpose });

            builder.Property(c => c.Destination)
                .HasColumnName("destination")
                .HasMaxLength(254);

            builder.Property(c => c.Purpose)
                .HasColumnName("purpose");

            // HMAC-SHA256 em hexadecimal
            builder.Property(c => c.CodeHash)
                .HasColumnName("code_hash")
                .IsRequired()
                .HasMaxLength(64);

            builder.Property(c => c.ExpiresAt)
                .HasColumnName("expires_at")
                .IsRequired();

            builder.Property(c => c.FailedAttempts)
                .HasColumnName("failed_attempts")
                .IsRequired();

            builder.Property(c => c.CreatedAt)
                .HasColumnName("created_at")
                .IsRequired();

            // Indexes
            builder.HasIndex(c => c.ExpiresAt).HasDatabaseName("idx_verification_codes_expires");
        }
    }
}
