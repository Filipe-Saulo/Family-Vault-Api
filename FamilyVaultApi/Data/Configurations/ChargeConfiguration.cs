using FamilyVaultApi.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyVaultApi.Data.Configurations
{
    public class ChargeConfiguration : IEntityTypeConfiguration<Charge>
    {
        public void Configure(EntityTypeBuilder<Charge> builder)
        {
            builder.ToTable("charges");

            builder.HasKey(c => c.ChargeId);
            builder.Property(c => c.ChargeId).HasColumnName("charge_id").ValueGeneratedOnAdd();

            builder.Property(c => c.UserId)
                .HasColumnName("user_id")
                .IsRequired()
                .HasMaxLength(450);

            builder.Property(c => c.InboundDocumentId).HasColumnName("inbound_document_id").IsRequired();

            builder.Property(c => c.Issuer).HasColumnName("issuer").HasMaxLength(200);
            builder.Property(c => c.Description).HasColumnName("description").HasMaxLength(500);
            builder.Property(c => c.Amount).HasColumnName("amount").HasPrecision(15, 2);
            builder.Property(c => c.DueDate).HasColumnName("due_date");
            builder.Property(c => c.Barcode).HasColumnName("barcode").HasMaxLength(48);
            builder.Property(c => c.SuggestedCategoryId).HasColumnName("suggested_category_id");
            builder.Property(c => c.PossibleDuplicateTransactionId).HasColumnName("possible_duplicate_transaction_id");

            builder.Property(c => c.Status)
                .HasColumnName("status")
                .HasConversion<string>()
                .IsRequired()
                .HasMaxLength(20);

            builder.Property(c => c.TransactionId).HasColumnName("transaction_id");

            builder.Property(c => c.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Indexes
            builder.HasIndex(c => c.InboundDocumentId).IsUnique().HasDatabaseName("idx_charges_inbound_document");
            builder.HasIndex(c => new { c.UserId, c.Status }).HasDatabaseName("idx_charges_user_status");
            builder.HasIndex(c => new { c.UserId, c.Barcode }).HasDatabaseName("idx_charges_user_barcode");

            // Foreign keys
            builder.HasOne(c => c.User)
                .WithMany()
                .HasForeignKey(c => c.UserId)
                .HasConstraintName("fk_charges_user")
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(c => c.InboundDocument)
                .WithOne(d => d.Charge)
                .HasForeignKey<Charge>(c => c.InboundDocumentId)
                .HasConstraintName("fk_charges_inbound_document")
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(c => c.SuggestedCategory)
                .WithMany()
                .HasForeignKey(c => c.SuggestedCategoryId)
                .HasConstraintName("fk_charges_suggested_category")
                .OnDelete(DeleteBehavior.SetNull);

            builder.HasOne(c => c.PossibleDuplicateTransaction)
                .WithMany()
                .HasForeignKey(c => c.PossibleDuplicateTransactionId)
                .HasConstraintName("fk_charges_possible_duplicate_transaction")
                .OnDelete(DeleteBehavior.SetNull);

            builder.HasOne(c => c.Transaction)
                .WithMany()
                .HasForeignKey(c => c.TransactionId)
                .HasConstraintName("fk_charges_transaction")
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
