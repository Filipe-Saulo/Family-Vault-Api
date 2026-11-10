using FamilyVaultApi.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyVaultApi.Data.Configurations
{
    public class GmailOAuthStateConfiguration : IEntityTypeConfiguration<GmailOAuthState>
    {
        public void Configure(EntityTypeBuilder<GmailOAuthState> builder)
        {
            builder.ToTable("gmail_oauth_states");

            builder.HasKey(s => s.GmailOAuthStateId);
            builder.Property(s => s.GmailOAuthStateId).HasColumnName("gmail_oauth_state_id").ValueGeneratedOnAdd();

            builder.Property(s => s.UserId)
                .HasColumnName("user_id")
                .IsRequired()
                .HasMaxLength(450);

            // sha256 em hex
            builder.Property(s => s.StateHash).HasColumnName("state_hash").IsRequired().HasMaxLength(64);
            builder.Property(s => s.ReturnUrl).HasColumnName("return_url").IsRequired().HasMaxLength(500);
            builder.Property(s => s.ExpiresAt).HasColumnName("expires_at").IsRequired();

            // Indexes
            builder.HasIndex(s => s.StateHash)
                .IsUnique()
                .HasDatabaseName("idx_gmail_oauth_states_hash");
            builder.HasIndex(s => s.ExpiresAt).HasDatabaseName("idx_gmail_oauth_states_expires");

            // Foreign keys
            builder.HasOne(s => s.User)
                .WithMany()
                .HasForeignKey(s => s.UserId)
                .HasConstraintName("fk_gmail_oauth_states_user")
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
