using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SpendingApi.Domain.Entities;

namespace SpendingApi.Infrastructure.Persistence.Configurations;

public sealed class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("transactions");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasColumnName("id");

        builder.Property(t => t.CustomerId)
            .HasColumnName("customer_id")
            .IsRequired();

        builder.OwnsOne(t => t.Amount, money =>
        {
            money.Property(m => m.Amount)
                .HasColumnName("amount")
                .HasPrecision(18, 2)
                .IsRequired();

            money.Property(m => m.Currency)
                .HasColumnName("currency")
                .HasMaxLength(3)
                .IsRequired();
        });

        builder.Property(t => t.MerchantName)
            .HasColumnName("merchant_name")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(t => t.CategoryId)
            .HasColumnName("category_id")
            .IsRequired();

        builder.Property(t => t.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .IsRequired();

        builder.Property(t => t.TransactionDate)
            .HasColumnName("transaction_date")
            .IsRequired();

        builder.Property(t => t.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(t => t.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Property(t => t.DeletedAt)
            .HasColumnName("deleted_at");

        builder.HasIndex(t => t.CustomerId)
            .HasDatabaseName("ix_transactions_customer_id");

        builder.HasIndex(t => new { t.CustomerId, t.TransactionDate })
            .HasDatabaseName("ix_transactions_customer_id_date");

        builder.HasQueryFilter(t => t.DeletedAt == null);
    }
}
