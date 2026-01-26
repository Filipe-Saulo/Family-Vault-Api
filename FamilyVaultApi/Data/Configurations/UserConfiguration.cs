using FamilyVaultApi.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyVaultApi.Data.Configurations
{
    public class UserConfiguration : IEntityTypeConfiguration<User>
    {
        public void Configure(EntityTypeBuilder<User> builder)
        {                        
            builder.Property(u => u.FirstName)
                .HasColumnName("first_name")
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(u => u.LastName)
                .HasColumnName("last_name")
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(u => u.FullName)
                .HasColumnName("full_name")
                .HasMaxLength(255);                

            builder.Property(u => u.Age)
                .HasColumnName("age")
                .IsRequired();

            builder.Property(u => u.RegisteredAt)
                .HasColumnName("registered_at")
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            builder.Property(u => u.UpdatedAt)
                .HasColumnName("updated_at")
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .ValueGeneratedOnAddOrUpdate();

            builder.Property(u => u.LastLogin)
                .HasColumnName("last_login")
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Configurar índice para idade
            builder.HasIndex(u => u.Age).HasDatabaseName("idx_users_age");

            // Configurar relacionamento com Transactions
            builder.HasMany(u => u.Transactions)
                .WithOne(t => t.User)
                .HasForeignKey(t => t.UserId)
                .HasConstraintName("fk_transactions_user")
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}