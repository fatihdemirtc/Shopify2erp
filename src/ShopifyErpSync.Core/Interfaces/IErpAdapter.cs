using ShopifyErpSync.Core.Models.Erp;
using ShopifyErpSync.Core.Models.Shopify;

namespace ShopifyErpSync.Core.Interfaces;

public interface IErpAdapter
{
    Task<Customer> GetOrCreateCustomerAsync(ShopifyCustomer shopifyCustomer, CancellationToken ct = default);
    Task<Order> CreateOrderAsync(ShopifyOrder shopifyOrder, Customer customer, CancellationToken ct = default);
    Task DecrementStockAsync(string sku, int quantity, CancellationToken ct = default);
    Task<Invoice> GenerateInvoiceAsync(Order order, CancellationToken ct = default);
}
