using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ShopifyErpSync.Core.Interfaces;
using ShopifyErpSync.Core.Models.Shopify;
using ShopifyErpSync.Infrastructure.Data;

namespace ShopifyErpSync.Infrastructure.Services;

public class OrderSyncService : IOrderSyncService
{
    private readonly AppDbContext _db;
    private readonly IErpAdapter _erp;
    private readonly INotificationService _notifications;
    private readonly IAuditService _audit;
    private readonly ILogger<OrderSyncService> _logger;

    public OrderSyncService(
        AppDbContext db,
        IErpAdapter erp,
        INotificationService notifications,
        IAuditService audit,
        ILogger<OrderSyncService> logger)
    {
        _db = db;
        _erp = erp;
        _notifications = notifications;
        _audit = audit;
        _logger = logger;
    }

    public async Task SyncOrderAsync(ShopifyOrder shopifyOrder, CancellationToken ct = default)
    {
        var existing = await _db.Orders
            .FirstOrDefaultAsync(o => o.ShopifyOrderId == shopifyOrder.Id, ct);

        if (existing is not null)
        {
            _logger.LogInformation("Order {Id} already synced — skipping", shopifyOrder.Id);
            await _audit.LogAsync("DuplicateOrderSkipped", "ShopifyOrder",
                shopifyOrder.Id.ToString(),
                $"Order #{shopifyOrder.OrderNumber} already exists", true, ct);
            return;
        }

        using var transaction = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var shopifyCustomer = shopifyOrder.Customer ?? new ShopifyCustomer
            {
                Id = 0,
                FirstName = shopifyOrder.BillingAddress?.FirstName ?? "Guest",
                LastName = shopifyOrder.BillingAddress?.LastName ?? "",
                Email = shopifyOrder.Email ?? shopifyOrder.BillingAddress?.Email,
                Phone = shopifyOrder.BillingAddress?.Phone
            };
            var customer = await _erp.GetOrCreateCustomerAsync(shopifyCustomer, ct);
            var order = await _erp.CreateOrderAsync(shopifyOrder, customer, ct);

            foreach (var lineItem in shopifyOrder.LineItems)
            {
                var sku = !string.IsNullOrEmpty(lineItem.Sku)
                    ? lineItem.Sku
                    : lineItem.VariantId.HasValue
                        ? $"VARIANT-{lineItem.VariantId}"
                        : $"ITEM-{lineItem.Id}";
                await _erp.DecrementStockAsync(sku, lineItem.Quantity, ct);
            }

            await _erp.GenerateInvoiceAsync(order, ct);

            order.Status = "completed";
            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            await _audit.LogAsync("OrderSynced", "ShopifyOrder", shopifyOrder.Id.ToString(),
                $"Order #{shopifyOrder.OrderNumber} synced — ${shopifyOrder.Total:F2}", true, ct);

            await _notifications.SendSuccessAsync(
                $"✅ Order #{shopifyOrder.OrderNumber} synced — ${shopifyOrder.Total:F2} — {customer.Name}", ct);

            _logger.LogInformation("Order {OrderNumber} synced successfully", shopifyOrder.OrderNumber);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            await _audit.LogAsync("OrderSyncFailed", "ShopifyOrder", shopifyOrder.Id.ToString(),
                ex.Message, false, ct);
            _logger.LogError(ex, "Order {OrderNumber} sync failed", shopifyOrder.OrderNumber);
            throw;
        }
    }
}
