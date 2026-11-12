using FamilyVaultApi.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyVaultApi.Data.Configurations
{
    public class PersonalAccessTokenConfiguration : IEntityTypeConfiguration<PersonalAccessToken>
    {
        public void Configure(EntityTypeBuilder<PersonalAccessToken> builder)
        {
            builder.ToTable("personal_access_tokens");

            builder.HasKey(t => t.PersonalAccessTokenId);

            builder.Property(t => t.PersonalAccessTokenId)
                .HasColumnName("personal_access_token_id");

            builder.Property(t => t.UserId)
                .HasColumnName("user_id")
                .IsRequired()
                .HasMaxLength(255);

            builder.Property(t => t.Name)
                .HasColumnName("name")
                .IsRequired()
                .HasMaxLength(PersonalAccessToken.MaxNameLength);

            // SHA-256 em hexadecimal
            builder.Property(t => t.TokenHash)
                .HasColumnName("token_hash")
                .IsRequired()
                .HasMaxLength(64);

            builder.Property(t => t.Scope)
                .HasColumnName("scope")
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(t => t.CreatedAt)
                .HasColumnName("created_at")
                .IsRequired();

            builder.Property(t => t.ExpiresAt)
                .HasColumnName("expires_at")
                .IsRequired();

            builder.Property(t => t.LastUsedAt)
                .HasColumnName("last_used_at");

            builder.Property(t => t.RevokedAt)
                .HasColumnName("revoked_at");

            // Indexes
            builder.HasIndex(t => t.TokenHash).IsUnique().HasDatabaseName("uq_personal_access_tokens_hash");
            builder.HasIndex(t => t.UserId).HasDatabaseName("idx_personal_access_tokens_user");

            // Foreign keys
            builder.HasOne(t => t.User)
                .WithMany()
                .HasForeignKey(t => t.UserId)
                .HasConstraintName("fk_personal_access_tokens_user")
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
