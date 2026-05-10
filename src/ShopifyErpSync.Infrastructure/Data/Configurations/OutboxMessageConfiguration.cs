using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShopifyErpSync.Core.Models;

namespace ShopifyErpSync.Infrastructure.Data.Configurations;

public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.HasKey(m => m.Id);
        builder.Property(m => m.MessageType).HasMaxLength(100).IsRequired();
        builder.Property(m => m.Status).HasMaxLength(20).IsRequired();
        builder.HasIndex(m => new { m.Status, m.NextRetryAt })
            .HasDatabaseName("IX_Outbox_Status_NextRetry");
    }
}
