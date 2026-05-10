using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShopifyErpSync.Core.Models.Erp;

namespace ShopifyErpSync.Infrastructure.Data.Configurations;

public class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.HasKey(i => i.Id);
        builder.Property(i => i.InvoiceNumber).HasMaxLength(50).IsRequired();
        builder.Property(i => i.Status).HasMaxLength(50).IsRequired();
        builder.Property(i => i.Total).HasColumnType("decimal(18,2)");
        builder.Property(i => i.Vat).HasColumnType("decimal(18,2)");
        builder.HasIndex(i => i.InvoiceNumber).IsUnique();
        builder.HasOne(i => i.Order)
            .WithOne(o => o.Invoice)
            .HasForeignKey<Invoice>(i => i.OrderId);
    }
}
