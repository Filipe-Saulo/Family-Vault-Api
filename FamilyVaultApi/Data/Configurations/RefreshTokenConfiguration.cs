using FamilyVaultApi.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyVaultApi.Data.Configurations
{
    public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
    {
        public void Configure(EntityTypeBuilder<RefreshToken> builder)
        {
            builder.ToTable("tb_refresh_tokens");

            builder.HasKey(t => t.RefreshTokenId);

            builder.Property(t => t.RefreshTokenId)
                .HasColumnName("refresh_token_id");

            // SHA-256 em hexadecimal
            builder.Property(t => t.TokenHash)
                .HasColumnName("token_hash")
                .IsRequired()
                .HasMaxLength(64);

            builder.Property(t => t.FamilyId)
                .HasColumnName("family_id")
                .IsRequired()
                .HasMaxLength(36);

            builder.Property(t => t.UserId)
                .HasColumnName("user_id")
                .IsRequired()
                .HasMaxLength(255);

            builder.Property(t => t.Client)
                .HasColumnName("client")
                .IsRequired();

            builder.Property(t => t.CreatedAt)
                .HasColumnName("created_at")
                .IsRequired();

            builder.Property(t => t.ExpiresAt)
                .HasColumnName("expires_at")
                .IsRequired();

            builder.Property(t => t.ConsumedAt)
                .HasColumnName("consumed_at");

            builder.Property(t => t.RevokedAt)
                .HasColumnName("revoked_at");

            // Indexes
            builder.HasIndex(t => t.TokenHash).IsUnique().HasDatabaseName("uq_refresh_tokens_hash");
            builder.HasIndex(t => t.FamilyId).HasDatabaseName("idx_refresh_tokens_family");
            builder.HasIndex(t => t.UserId).HasDatabaseName("idx_refresh_tokens_user");

            // Foreign keys
            builder.HasOne(t => t.User)
                .WithMany()
                .HasForeignKey(t => t.UserId)
                .HasConstraintName("fk_refresh_tokens_user")
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
