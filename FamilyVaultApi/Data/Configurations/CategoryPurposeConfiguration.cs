using FamilyVaultApi.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyVaultApi.Data.Configurations
{
    public class CategoryPurposeConfiguration : IEntityTypeConfiguration<CategoryPurpose>
    {
        public void Configure(EntityTypeBuilder<CategoryPurpose> builder)
        {
            builder.ToTable("category_purposes");

            builder.HasKey(cp => cp.CategoryPurposeId);
            builder.Property(cp => cp.CategoryPurposeId).HasColumnName("category_purpose_id").ValueGeneratedOnAdd();

            builder.Property(cp => cp.Code)
                .HasColumnName("code")
                .IsRequired()
                .HasMaxLength(20);

            builder.Property(cp => cp.Name)
                .HasColumnName("name")
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(cp => cp.Description)
                .HasColumnName("description")
                .HasMaxLength(200);

            builder.Property(cp => cp.IsActive)
                .HasColumnName("is_active")
                .HasDefaultValue(true);

            builder.Property(cp => cp.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            builder.Property(cp => cp.UpdatedAt)
                .HasColumnName("updated_at")
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .ValueGeneratedOnAddOrUpdate();

            // Indexes
            builder.HasIndex(cp => cp.Code).IsUnique().HasDatabaseName("uk_category_purposes_code");
            builder.HasIndex(cp => cp.IsActive).HasDatabaseName("idx_category_purposes_active");
        }
    }
}