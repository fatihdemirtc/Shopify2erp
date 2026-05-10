using Microsoft.Extensions.Logging;
using ShopifyErpSync.Core.Interfaces;
using ShopifyErpSync.Core.Models.Audit;
using ShopifyErpSync.Infrastructure.Data;

namespace ShopifyErpSync.Infrastructure.Services;

public class AuditService : IAuditService
{
    private readonly AppDbContext _db;
    private readonly ILogger<AuditService> _logger;

    public AuditService(AppDbContext db, ILogger<AuditService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task LogAsync(string action, string? entityType = null, string? entityId = null,
        string? details = null, bool success = true, CancellationToken ct = default)
    {
        try
        {
            _db.AuditLog.Add(new AuditLogEntry
            {
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                Details = details,
                Success = success
            });
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write audit log for {Action}", action);
        }
    }
}
