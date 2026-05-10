using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ShopifyErpSync.Core.Interfaces;
using ShopifyErpSync.Core.Models.Erp;
using ShopifyErpSync.Core.Models.Shopify;
using ShopifyErpSync.Infrastructure.Data;

namespace ShopifyErpSync.Infrastructure.Adapters;

/// <summary>
/// Simulates a cloud ERP REST API (NetSuite / SAP / Odoo pattern).
/// Writes to local DB so the dashboard can show results.
/// </summary>
public class RestApiErpAdapter : IErpAdapter
{
    private readonly AppDbContext _db;
    private readonly ILogger<RestApiErpAdapter> _logger;
    private static readonly Random _rng = new();

    public RestApiErpAdapter(AppDbContext db, ILogger<RestApiErpAdapter> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<Customer> GetOrCreateCustomerAsync(ShopifyCustomer shopifyCustomer, CancellationToken ct = default)
    {
        await SimulateApiCallAsync("GET", $"/erp/customers?externalId={shopifyCustomer.Id}", ct);

        var externalId = shopifyCustomer.Id.ToString();
        var existing = await _db.Customers
            .FirstOrDefaultAsync(c => c.ExternalId == externalId, ct);

        if (existing is not null)
        {
            _logger.LogInformation("[REST ERP] GET /erp/customers/{Id} → 200 OK (existing)", externalId);
            return existing;
        }

        await SimulateApiCallAsync("POST", "/erp/customers", ct);

        var customer = new Customer
        {
            ExternalId = externalId,
            Name = shopifyCustomer.Name,
            Email = shopifyCustomer.Email,
            Phone = shopifyCustomer.Phone
        };
        _db.Customers.Add(customer);
        _logger.LogInformation("[REST ERP] POST /erp/customers → 201 Created ({Name})", customer.Name);
        return customer;
    }

    public async Task<Order> CreateOrderAsync(ShopifyOrder shopifyOrder, Customer customer, CancellationToken ct = default)
    {
        await SimulateApiCallAsync("POST", "/erp/orders", ct);

        var order = new Order
        {
            ShopifyOrderId = shopifyOrder.Id,
            OrderNumber = shopifyOrder.OrderNumber,
            Customer = customer,
            Subtotal = shopifyOrder.Subtotal,
            Vat = shopifyOrder.Vat,
            Total = shopifyOrder.Total,
            Status = "pending"
        };

        foreach (var lineItem in shopifyOrder.LineItems)
        {
            var product = await _db.Products
                .FirstOrDefaultAsync(p => p.Sku == lineItem.Sku, ct);

            if (product is null)
            {
                _logger.LogWarning("[REST ERP] SKU {Sku} not found — skipping", lineItem.Sku);
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
        _logger.LogInformation("[REST ERP] POST /erp/orders → 201 Created (#{OrderNumber})", order.OrderNumber);
        return order;
    }

    public async Task DecrementStockAsync(string sku, int quantity, CancellationToken ct = default)
    {
        await SimulateApiCallAsync("PATCH", $"/erp/inventory/{sku}", ct);

        var product = await _db.Products.FirstOrDefaultAsync(p => p.Sku == sku, ct);
        if (product is null)
        {
            _logger.LogWarning("[REST ERP] PATCH /erp/inventory/{Sku} → 404 Not Found", sku);
            return;
        }

        product.StockQuantity -= quantity;
        _logger.LogInformation("[REST ERP] PATCH /erp/inventory/{Sku} → 200 OK (-{Qty})", sku, quantity);
    }

    public async Task<Invoice> GenerateInvoiceAsync(Order order, CancellationToken ct = default)
    {
        await SimulateApiCallAsync("POST", "/erp/invoices", ct);

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
        _logger.LogInformation("[REST ERP] POST /erp/invoices → 201 Created ({Number})", invoiceNumber);
        return invoice;
    }

    private async Task SimulateApiCallAsync(string method, string path, CancellationToken ct)
    {
        var delayMs = _rng.Next(50, 200);
        _logger.LogDebug("[REST ERP] {Method} {Path} (simulated {Delay}ms)", method, path, delayMs);
        await Task.Delay(delayMs, ct);
    }
}
