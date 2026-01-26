using FamilyVaultApi.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyVaultApi.Data.Configurations
{
    public class CategoryConfiguration : IEntityTypeConfiguration<Category>
    {
        public void Configure(EntityTypeBuilder<Category> builder)
        {
            builder.ToTable("categories");

            builder.HasKey(c => c.CategoryId);
            builder.Property(c => c.CategoryId).HasColumnName("category_id").ValueGeneratedOnAdd();

            builder.Property(c => c.Description)
                .HasColumnName("description")
                .IsRequired()
                .HasMaxLength(255);

            builder.Property(c => c.CategoryPurposeId)
                .HasColumnName("category_purpose_id")
                .IsRequired();

            builder.Property(c => c.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            builder.Property(c => c.UpdatedAt)
                .HasColumnName("updated_at")
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .ValueGeneratedOnAddOrUpdate();

            // Indexes
            builder.HasIndex(c => c.CategoryPurposeId).HasDatabaseName("idx_categories_purpose");
            builder.HasIndex(c => c.Description).HasDatabaseName("idx_categories_description");

            // Foreign keys
            builder.HasOne(c => c.Purpose)
                .WithMany(cp => cp.Categories)
                .HasForeignKey(c => c.CategoryPurposeId)
                .HasConstraintName("fk_categories_purpose")
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}