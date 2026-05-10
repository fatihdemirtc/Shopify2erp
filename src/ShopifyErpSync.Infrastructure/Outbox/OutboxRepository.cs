using Microsoft.EntityFrameworkCore;
using ShopifyErpSync.Core.Interfaces;
using ShopifyErpSync.Core.Models;
using ShopifyErpSync.Infrastructure.Data;

namespace ShopifyErpSync.Infrastructure.Outbox;

public class OutboxRepository : IOutboxRepository
{
    private readonly AppDbContext _db;

    public OutboxRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task EnqueueAsync(OutboxMessage message, CancellationToken ct = default)
    {
        _db.OutboxMessages.Add(message);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<List<OutboxMessage>> GetPendingAsync(int batchSize, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        return await _db.OutboxMessages
            .Where(m => m.Status == "pending" &&
                        (m.NextRetryAt == null || m.NextRetryAt <= now))
            .OrderBy(m => m.CreatedAt)
            .Take(batchSize)
            .ToListAsync(ct);
    }

    public async Task MarkProcessingAsync(Guid id, CancellationToken ct = default)
    {
        await _db.OutboxMessages
            .Where(m => m.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(m => m.Status, "processing")
                .SetProperty(m => m.LastAttemptAt, DateTime.UtcNow), ct);
    }

    public async Task MarkCompletedAsync(Guid id, CancellationToken ct = default)
    {
        await _db.OutboxMessages
            .Where(m => m.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(m => m.Status, "completed")
                .SetProperty(m => m.ProcessedAt, DateTime.UtcNow), ct);
    }

    public async Task MarkFailedAsync(Guid id, string error, CancellationToken ct = default)
    {
        await _db.OutboxMessages
            .Where(m => m.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(m => m.Status, "failed")
                .SetProperty(m => m.ErrorMessage, error)
                .SetProperty(m => m.LastAttemptAt, DateTime.UtcNow), ct);
    }

    public async Task RescheduleAsync(Guid id, DateTime nextRetryAt, string error, CancellationToken ct = default)
    {
        await _db.OutboxMessages
            .Where(m => m.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(m => m.Status, "pending")
                .SetProperty(m => m.NextRetryAt, nextRetryAt)
                .SetProperty(m => m.ErrorMessage, error)
                .SetProperty(m => m.LastAttemptAt, DateTime.UtcNow)
                .SetProperty(m => m.AttemptCount, m => m.AttemptCount + 1), ct);
    }
}
