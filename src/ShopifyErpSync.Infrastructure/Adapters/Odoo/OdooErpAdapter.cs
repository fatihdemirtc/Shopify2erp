using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ShopifyErpSync.Core.Interfaces;
using ShopifyErpSync.Core.Models.Erp;
using ShopifyErpSync.Core.Models.Shopify;
using ShopifyErpSync.Infrastructure.Data;

namespace ShopifyErpSync.Infrastructure.Adapters.Odoo;

public class OdooErpAdapter : IErpAdapter
{
    private readonly OdooClient _odoo;
    private readonly AppDbContext _db;
    private readonly OdooSettings _settings;
    private readonly ILogger<OdooErpAdapter> _logger;

    public OdooErpAdapter(
        OdooClient odoo,
        AppDbContext db,
        IOptions<OdooSettings> settings,
        ILogger<OdooErpAdapter> logger)
    {
        _odoo = odoo;
        _db = db;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<Customer> GetOrCreateCustomerAsync(
        ShopifyCustomer shopifyCustomer, CancellationToken ct = default)
    {
        var shopifyRef = $"shopify_{shopifyCustomer.Id}";

        var localCustomer = await _db.Customers
            .FirstOrDefaultAsync(c => c.ExternalId == shopifyRef, ct);
        if (localCustomer is not null)
        {
            _logger.LogDebug("Customer {ExternalId} found in local DB", shopifyRef);
            var existing = await _odoo.SearchReadAsync(
                "res.partner", [new object[] { "ref", "=", shopifyRef }], ["id"], limit: 1, ct: ct);
            if (existing.Count == 0)
            {
                await _odoo.CreateAsync("res.partner", new
                {
                    name = localCustomer.Name,
                    email = localCustomer.Email,
                    phone = localCustomer.Phone,
                    @ref = shopifyRef,
                    customer_rank = 1
                }, ct);
                _logger.LogInformation("Odoo partner re-created for {Ref} (ERP recovery)", shopifyRef);
            }
            return localCustomer;
        }

        var partners = await _odoo.SearchReadAsync(
            "res.partner",
            [new object[] { "ref", "=", shopifyRef }],
            ["id"],
            limit: 1,
            ct: ct);

        if (partners.Count == 0)
        {
            var partnerId = await _odoo.CreateAsync("res.partner", new
            {
                name = shopifyCustomer.Name,
                email = shopifyCustomer.Email,
                phone = shopifyCustomer.Phone,
                @ref = shopifyRef,
                customer_rank = 1
            }, ct);
            _logger.LogInformation("Odoo partner {PartnerId} created for {Ref}", partnerId, shopifyRef);
        }
        else
        {
            _logger.LogDebug("Odoo partner exists for {Ref}", shopifyRef);
        }

        var customer = new Customer
        {
            ExternalId = shopifyRef,
            Name = shopifyCustomer.Name,
            Email = shopifyCustomer.Email,
            Phone = shopifyCustomer.Phone
        };
        _db.Customers.Add(customer);
        return customer;
    }

    public async Task<Order> CreateOrderAsync(
        ShopifyOrder shopifyOrder, Customer customer, CancellationToken ct = default)
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

        var orderLines = new List<object>();

        foreach (var lineItem in shopifyOrder.LineItems)
        {
            var sku = ResolveSku(lineItem);
            var odooProductId = await GetOrCreateOdooProductAsync(sku, lineItem.Name, lineItem.Price, ct);

            var localProduct = await _db.Products.FirstOrDefaultAsync(p => p.Sku == sku, ct);
            if (localProduct is null)
            {
                localProduct = new Product
                {
                    Sku = sku,
                    Name = lineItem.Name.Length > 200 ? lineItem.Name[..200] : lineItem.Name,
                    Price = lineItem.Price,
                    StockQuantity = 0
                };
                _db.Products.Add(localProduct);
            }

            order.OrderItems.Add(new OrderItem
            {
                Product = localProduct,
                Quantity = lineItem.Quantity,
                UnitPrice = lineItem.Price,
                LineTotal = lineItem.Price * lineItem.Quantity
            });

            // ORM command [0, 0, values] = create new line
            orderLines.Add(new object[] { 0, 0, new
            {
                product_id = odooProductId,
                product_uom_qty = (double)lineItem.Quantity,
                price_unit = (double)lineItem.Price,
                name = lineItem.Name
            }});
        }

        var partnerId = await GetOdooPartnerIdAsync(customer.ExternalId, ct);
        var saleOrderId = await _odoo.CreateAsync("sale.order", new
        {
            partner_id = partnerId,
            origin = $"Shopify #{shopifyOrder.OrderNumber}",
            order_line = orderLines
        }, ct);

        await _odoo.ExecuteAsync("sale.order", saleOrderId, "action_confirm", ct);
        _logger.LogInformation("Odoo SO{SaleOrderId} created and confirmed", saleOrderId);

        _db.Orders.Add(order);
        return order;
    }

    public async Task DecrementStockAsync(string sku, int quantity, CancellationToken ct = default)
    {
        var localProduct = await _db.Products.FirstOrDefaultAsync(p => p.Sku == sku, ct);
        if (localProduct is not null)
            localProduct.StockQuantity -= quantity;

        var products = await _odoo.SearchReadAsync(
            "product.product",
            [new object[] { "default_code", "=", sku }],
            ["id"],
            limit: 1,
            ct: ct);

        if (products.Count == 0)
        {
            _logger.LogWarning("SKU {Sku} not in Odoo — skipping Odoo stock update", sku);
            return;
        }

        var odooProductId = Convert.ToInt32(products[0]["id"]);
        var quants = await _odoo.SearchReadAsync(
            "stock.quant",
            [
                new object[] { "product_id", "=", odooProductId },
                new object[] { "location_id", "=", _settings.StockLocationId }
            ],
            ["id", "quantity"],
            limit: 1,
            ct: ct);

        if (quants.Count == 0)
        {
            _logger.LogWarning("No stock.quant for {Sku} in location {LocationId}", sku, _settings.StockLocationId);
            return;
        }

        var quantId = Convert.ToInt32(quants[0]["id"]);
        var currentQty = Convert.ToDouble(quants[0]["quantity"]);
        var newQty = Math.Max(0, currentQty - quantity);

        await _odoo.WriteAsync("stock.quant", quantId, new { inventory_quantity = newQty }, ct);
        await _odoo.ExecuteAsync("stock.quant", quantId, "action_apply_inventory", ct);

        _logger.LogInformation("Odoo stock: {Sku} -{Qty} (now {NewQty})", sku, quantity, newQty);
    }

    public async Task<Invoice> GenerateInvoiceAsync(Order order, CancellationToken ct = default)
    {
        var invoiceLines = new List<object>();
        foreach (var item in order.OrderItems)
        {
            var odooProductId = await GetOrCreateOdooProductAsync(
                item.Product.Sku, item.Product.Name, item.UnitPrice, ct);

            // ORM command [0, 0, values] = create new line
            invoiceLines.Add(new object[] { 0, 0, new
            {
                product_id = odooProductId,
                quantity = (double)item.Quantity,
                price_unit = (double)item.UnitPrice,
                name = item.Product.Name
            }});
        }

        var partnerId = await GetOdooPartnerIdAsync(order.Customer.ExternalId, ct);
        var moveId = await _odoo.CreateAsync("account.move", new
        {
            move_type = "out_invoice",
            partner_id = partnerId,
            invoice_origin = $"Shopify #{order.OrderNumber}",
            invoice_line_ids = invoiceLines
        }, ct);

        await _odoo.ExecuteAsync("account.move", moveId, "action_post", ct);
        _logger.LogInformation("Odoo invoice posted (account.move {MoveId})", moveId);

        var invoice = new Invoice
        {
            Order = order,
            InvoiceNumber = $"INV-{DateTime.UtcNow:yyyyMMdd}-{order.OrderNumber}",
            Total = order.Total,
            Vat = order.Vat,
            Status = "created"
        };
        _db.Invoices.Add(invoice);
        return invoice;
    }

    private async Task<int> GetOrCreateOdooProductAsync(
        string sku, string name, decimal price, CancellationToken ct)
    {
        var products = await _odoo.SearchReadAsync(
            "product.product",
            [new object[] { "default_code", "=", sku }],
            ["id"],
            limit: 1,
            ct: ct);

        if (products.Count > 0)
            return Convert.ToInt32(products[0]["id"]);

        var templateId = await _odoo.CreateAsync("product.template", new
        {
            name = name.Length > 200 ? name[..200] : name,
            default_code = sku,
            list_price = (double)price,
            type = "consu",
            sale_ok = true,
            purchase_ok = false
        }, ct);

        // product.template auto-creates a product.product variant
        var variants = await _odoo.SearchReadAsync(
            "product.product",
            [new object[] { "product_tmpl_id", "=", templateId }],
            ["id"],
            limit: 1,
            ct: ct);

        var variantId = variants.Count > 0 ? Convert.ToInt32(variants[0]["id"]) : templateId;
        _logger.LogInformation("Odoo product {VariantId} created for SKU {Sku}", variantId, sku);
        return variantId;
    }

    private async Task<int> GetOdooPartnerIdAsync(string externalId, CancellationToken ct)
    {
        var partners = await _odoo.SearchReadAsync(
            "res.partner",
            [new object[] { "ref", "=", externalId }],
            ["id"],
            limit: 1,
            ct: ct);

        if (partners.Count == 0)
            throw new InvalidOperationException($"Odoo partner not found for ref '{externalId}'");

        return Convert.ToInt32(partners[0]["id"]);
    }

    private static string ResolveSku(ShopifyLineItem lineItem) =>
        !string.IsNullOrEmpty(lineItem.Sku) ? lineItem.Sku
        : lineItem.VariantId.HasValue ? $"VARIANT-{lineItem.VariantId}"
        : $"ITEM-{lineItem.Id}";
}
