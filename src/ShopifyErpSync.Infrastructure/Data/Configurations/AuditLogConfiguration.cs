using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShopifyErpSync.Core.Models.Audit;

namespace ShopifyErpSync.Infrastructure.Data.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLogEntry>
{
    public void Configure(EntityTypeBuilder<AuditLogEntry> builder)
    {
        builder.ToTable("AuditLog");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Action).HasMaxLength(100).IsRequired();
        builder.Property(a => a.EntityType).HasMaxLength(50);
        builder.Property(a => a.EntityId).HasMaxLength(50);
        builder.HasIndex(a => a.CreatedAt).IsDescending()
            .HasDatabaseName("IX_AuditLog_CreatedAt");
    }
}
