using ShopifyErpSync.Core.Models.Shopify;

namespace ShopifyErpSync.Core.Interfaces;

public interface IOrderSyncService
{
    Task SyncOrderAsync(ShopifyOrder shopifyOrder, CancellationToken ct = default);
}
