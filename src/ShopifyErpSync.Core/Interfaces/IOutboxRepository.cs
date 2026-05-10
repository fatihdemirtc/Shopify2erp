using ShopifyErpSync.Core.Models;

namespace ShopifyErpSync.Core.Interfaces;

public interface IOutboxRepository
{
    Task EnqueueAsync(OutboxMessage message, CancellationToken ct = default);
    Task<List<OutboxMessage>> GetPendingAsync(int batchSize, CancellationToken ct = default);
    Task MarkProcessingAsync(Guid id, CancellationToken ct = default);
    Task MarkCompletedAsync(Guid id, CancellationToken ct = default);
    Task MarkFailedAsync(Guid id, string error, CancellationToken ct = default);
    Task RescheduleAsync(Guid id, DateTime nextRetryAt, string error, CancellationToken ct = default);
}
