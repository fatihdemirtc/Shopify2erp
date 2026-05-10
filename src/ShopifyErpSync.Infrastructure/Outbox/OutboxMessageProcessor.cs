using System.Text.Json;
using Microsoft.Extensions.Logging;
using ShopifyErpSync.Core.Interfaces;
using ShopifyErpSync.Core.Models;
using ShopifyErpSync.Core.Models.Shopify;

namespace ShopifyErpSync.Infrastructure.Outbox;

public class OutboxMessageProcessor
{
    private readonly ILogger<OutboxMessageProcessor> _logger;
    public const int MaxAttempts = 5;

    public OutboxMessageProcessor(ILogger<OutboxMessageProcessor> logger)
    {
        _logger = logger;
    }

    public static TimeSpan GetRetryDelay(int attemptCount)
        => TimeSpan.FromSeconds(Math.Pow(2, attemptCount) * 60);

    public async Task ProcessAsync(
        OutboxMessage message,
        IOutboxRepository outbox,
        IOrderSyncService orderSync,
        CancellationToken ct)
    {
        try
        {
            await outbox.MarkProcessingAsync(message.Id, ct);

            if (message.MessageType == "ProcessShopifyOrder")
            {
                var shopifyOrder = JsonSerializer.Deserialize<ShopifyOrder>(message.Payload)
                    ?? throw new InvalidOperationException("Invalid Shopify order payload");
                await orderSync.SyncOrderAsync(shopifyOrder, ct);
            }
            else
            {
                _logger.LogWarning("Unknown message type {Type} — skipping", message.MessageType);
            }

            await outbox.MarkCompletedAsync(message.Id, ct);
            _logger.LogInformation("Outbox message {Id} completed", message.Id);
        }
        catch (Exception ex)
        {
            var nextAttempt = message.AttemptCount + 1;
            _logger.LogWarning(ex, "Outbox message {Id} failed on attempt {Attempt}", message.Id, nextAttempt);

            if (nextAttempt >= MaxAttempts)
            {
                await outbox.MarkFailedAsync(message.Id, ex.Message, ct);
                _logger.LogError("Outbox message {Id} permanently failed after {Max} attempts", message.Id, MaxAttempts);
            }
            else
            {
                var delay = GetRetryDelay(nextAttempt);
                var nextRetry = DateTime.UtcNow.Add(delay);
                await outbox.RescheduleAsync(message.Id, nextRetry, ex.Message, ct);
            }
        }
    }
}
