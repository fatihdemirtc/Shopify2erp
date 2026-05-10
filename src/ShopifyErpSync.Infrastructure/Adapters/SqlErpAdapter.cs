using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ShopifyErpSync.Core.Interfaces;
using ShopifyErpSync.Core.Models.Erp;
using ShopifyErpSync.Core.Models.Shopify;
using ShopifyErpSync.Infrastructure.Data;

namespace ShopifyErpSync.Infrastructure.Adapters;

public class SqlErpAdapter : IErpAdapter
{
    private readonly AppDbContext _db;
    private readonly ILogger<SqlErpAdapter> _logger;

    public SqlErpAdapter(AppDbContext db, ILogger<SqlErpAdapter> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<Customer> GetOrCreateCustomerAsync(ShopifyCustomer shopifyCustomer, CancellationToken ct = default)
    {
        var externalId = shopifyCustomer.Id.ToString();
        var existing = await _db.Customers
            .FirstOrDefaultAsync(c => c.ExternalId == externalId, ct);

        if (existing is not null)
        {
            _logger.LogDebug("Customer {ExternalId} found in ERP", externalId);
            return existing;
        }

        var customer = new Customer
        {
            ExternalId = externalId,
            Name = shopifyCustomer.Name,
            Email = shopifyCustomer.Email,
            Phone = shopifyCustomer.Phone
        };
        _db.Customers.Add(customer);
        _logger.LogInformation("Customer {ExternalId} created in ERP", externalId);
        return customer;
    }

    public async Task<Order> CreateOrderAsync(ShopifyOrder shopifyOrder, Customer customer, CancellationToken ct = default)
    {
        var order = new Order
        {
            ShopifyOrderId = shopifyOrder.Id,
            OrderNumber = shopifyOrder.OrderNumber.ToString(),
            Customer = customer,
            Subtotal = shopifyOrder.Subtotal,
            Vat = shopifyOrder.Vat,
            Total = shopifyOrder.Total,
            Status = "pending"
        };

        foreach (var lineItem in shopifyOrder.LineItems)
        {
            if (string.IsNullOrEmpty(lineItem.Sku))
            {
                _logger.LogWarning("Line item '{Name}' has no SKU — skipping", lineItem.Name);
                continue;
            }

            var product = await _db.Products
                .FirstOrDefaultAsync(p => p.Sku == lineItem.Sku, ct);

            if (product is null)
            {
                _logger.LogWarning("SKU {Sku} not found — skipping line item", lineItem.Sku);
                continue;
            }

            order.OrderItems.Add(new OrderItem
            {
                Product = product,
                Quantity = lineItem.Quantity,
                UnitPrice = lineItem.Price,
                LineTotal = lineItem.Price * lineItem.Quantity
            });
        }

        _db.Orders.Add(order);
        _logger.LogInformation("Order {OrderNumber} created in ERP", order.OrderNumber);
        return order;
    }

    public async Task DecrementStockAsync(string sku, int quantity, CancellationToken ct = default)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Sku == sku, ct);
        if (product is null)
        {
            _logger.LogWarning("SKU {Sku} not found for stock decrement", sku);
            return;
        }

        product.StockQuantity -= quantity;
        _logger.LogInformation("Stock decremented: {Sku} -{Quantity} (now {Stock})", sku, quantity, product.StockQuantity);
    }

    public async Task<Invoice> GenerateInvoiceAsync(Order order, CancellationToken ct = default)
    {
        var invoiceNumber = $"INV-{DateTime.UtcNow:yyyyMMdd}-{order.OrderNumber}";
        var invoice = new Invoice
        {
            Order = order,
            InvoiceNumber = invoiceNumber,
            Total = order.Total,
            Vat = order.Vat,
            Status = "created"
        };
        _db.Invoices.Add(invoice);
        _logger.LogInformation("Invoice {InvoiceNumber} generated", invoiceNumber);
        return await Task.FromResult(invoice);
    }
}
