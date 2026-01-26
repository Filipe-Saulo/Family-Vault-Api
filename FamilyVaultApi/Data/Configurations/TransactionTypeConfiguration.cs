using FamilyVaultApi.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyVaultApi.Data.Configurations
{
    public class TransactionTypeConfiguration : IEntityTypeConfiguration<TransactionType>
    {
        public void Configure(EntityTypeBuilder<TransactionType> builder)
        {
            builder.ToTable("transaction_types");

            builder.HasKey(tt => tt.TransactionTypeId);
            builder.Property(tt => tt.TransactionTypeId).HasColumnName("transaction_type_id").ValueGeneratedOnAdd();

            builder.Property(tt => tt.Code)
                .HasColumnName("code")
                .IsRequired()
                .HasMaxLength(20);

            builder.Property(tt => tt.Name)
                .HasColumnName("name")
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(tt => tt.Description)
                .HasColumnName("description")
                .HasMaxLength(200);

            builder.Property(tt => tt.IsActive)
                .HasColumnName("is_active")
                .HasDefaultValue(true);

            builder.Property(tt => tt.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            builder.Property(tt => tt.UpdatedAt)
                .HasColumnName("updated_at")
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .ValueGeneratedOnAddOrUpdate();

            // Indexes
            builder.HasIndex(tt => tt.Code).IsUnique().HasDatabaseName("uk_transaction_types_code");
            builder.HasIndex(tt => tt.IsActive).HasDatabaseName("idx_transaction_types_active");
        }
    }
}