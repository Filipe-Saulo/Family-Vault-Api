using FamilyVaultApi.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyVaultApi.Data.Configurations
{
    public class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
    {
        public void Configure(EntityTypeBuilder<Transaction> builder)
        {
            builder.ToTable("transactions");

            builder.HasKey(t => t.TransactionId);
            builder.Property(t => t.TransactionId).HasColumnName("transaction_id").ValueGeneratedOnAdd();

            builder.Property(t => t.UserId)
                .HasColumnName("user_id")
                .IsRequired()
                .HasMaxLength(450);

            builder.Property(t => t.CategoryId).HasColumnName("category_id").IsRequired();
            builder.Property(t => t.TransactionTypeId).HasColumnName("transaction_type_id").IsRequired();

            builder.Property(t => t.Description)
                .HasColumnName("description")
                .IsRequired()
                .HasMaxLength(500);

            builder.Property(t => t.Amount)
                .HasColumnName("amount")
                .IsRequired()
                .HasPrecision(15, 2);

            builder.Property(t => t.TransactionDate)
                .HasColumnName("transaction_date")
                .HasDefaultValueSql("CURRENT_DATE");

            builder.Property(t => t.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            builder.Property(t => t.UpdatedAt)
                .HasColumnName("updated_at")
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .ValueGeneratedOnAddOrUpdate();

            // Indexes
            builder.HasIndex(t => t.UserId).HasDatabaseName("idx_transactions_user_id");
            builder.HasIndex(t => t.CategoryId).HasDatabaseName("idx_transactions_category_id");
            builder.HasIndex(t => t.TransactionTypeId).HasDatabaseName("idx_transactions_type_id");
            builder.HasIndex(t => t.TransactionDate).HasDatabaseName("idx_transactions_date");
            builder.HasIndex(t => new { t.UserId, t.TransactionDate }).HasDatabaseName("idx_user_transaction_date");

            // Foreign keys
            builder.HasOne(t => t.User)
                .WithMany(u => u.Transactions)
                .HasForeignKey(t => t.UserId)
                .HasConstraintName("fk_transactions_user")
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(t => t.Category)
                .WithMany(c => c.Transactions)
                .HasForeignKey(t => t.CategoryId)
                .HasConstraintName("fk_transactions_category")
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(t => t.TransactionType)
                .WithMany(tt => tt.Transactions)
                .HasForeignKey(t => t.TransactionTypeId)
                .HasConstraintName("fk_transactions_type")
                .OnDelete(DeleteBehavior.Restrict);

                        
        }
    }
}