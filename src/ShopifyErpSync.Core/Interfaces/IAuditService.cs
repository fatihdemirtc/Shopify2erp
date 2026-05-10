namespace ShopifyErpSync.Core.Interfaces;

public interface IAuditService
{
    Task LogAsync(string action, string? entityType = null, string? entityId = null,
        string? details = null, bool success = true, CancellationToken ct = default);
}
