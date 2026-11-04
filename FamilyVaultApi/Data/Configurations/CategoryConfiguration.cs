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

            // Nulo = categoria global (criada por Administrator)
            builder.Property(c => c.UserId)
                .HasColumnName("user_id")
                .HasMaxLength(255);

            // Indexes
            builder.HasIndex(c => c.UserId).HasDatabaseName("idx_categories_user");
            builder.HasIndex(c => c.CategoryPurposeId).HasDatabaseName("idx_categories_purpose");
            builder.HasIndex(c => c.Description).HasDatabaseName("idx_categories_description");

            // Foreign keys
            builder.HasOne(c => c.Purpose)
                .WithMany(cp => cp.Categories)
                .HasForeignKey(c => c.CategoryPurposeId)
                .HasConstraintName("fk_categories_purpose")
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(c => c.User)
                .WithMany()
                .HasForeignKey(c => c.UserId)
                .HasConstraintName("fk_categories_user")
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}