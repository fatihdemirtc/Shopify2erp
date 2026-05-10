using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ShopifyErpSync.Core.Interfaces;
using ShopifyErpSync.Core.Models.Erp;
using ShopifyErpSync.Core.Models.Shopify;
using ShopifyErpSync.Infrastructure.Adapters;
using ShopifyErpSync.Infrastructure.Data;
using ShopifyErpSync.Infrastructure.Services;

namespace ShopifyErpSync.Tests;

public class OrderSyncServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly Mock<INotificationService> _notifications = new();
    private readonly Mock<IAuditService> _audit = new();

    public OrderSyncServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _db = new AppDbContext(options);
        SeedProducts();
    }

    private void SeedProducts()
    {
        _db.Products.AddRange(
            new Product { Sku = "TS-RM", Name = "Red T-Shirt M", StockQuantity = 50, Price = 29.99m },
            new Product { Sku = "TS-BL", Name = "Blue T-Shirt L", StockQuantity = 30, Price = 29.99m }
        );
        _db.SaveChanges();
    }

    private OrderSyncService CreateSut() => new(
        _db,
        new SqlErpAdapter(_db, NullLogger<SqlErpAdapter>.Instance),
        _notifications.Object,
        _audit.Object,
        NullLogger<OrderSyncService>.Instance);

    private static ShopifyOrder MakeOrder(long id = 100001, long customerId = 9001) => new()
    {
        Id = id,
        OrderNumber = id.ToString(),
        Customer = new ShopifyCustomer { Id = customerId, Name = "Jane Doe", Email = "jane@example.com" },
        LineItems = new List<ShopifyLineItem>
        {
            new() { Sku = "TS-RM", Name = "Red T-Shirt M", Quantity = 2, Price = 29.99m },
            new() { Sku = "TS-BL", Name = "Blue T-Shirt L", Quantity = 1, Price = 29.99m }
        },
        Subtotal = 89.97m,
        Vat = 16.19m,
        Total = 106.16m
    };

    [Fact]
    public async Task SyncOrder_NewCustomer_CreatesCustomerRecord()
    {
        var sut = CreateSut();

        await sut.SyncOrderAsync(MakeOrder());

        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.ExternalId == "9001");
        customer.Should().NotBeNull();
        customer!.Name.Should().Be("Jane Doe");
        customer.Email.Should().Be("jane@example.com");
    }

    [Fact]
    public async Task SyncOrder_ExistingCustomer_ReusesRecord()
    {
        var sut = CreateSut();
        var order1 = MakeOrder(id: 100001, customerId: 9001);
        var order2 = MakeOrder(id: 100002, customerId: 9001);

        await sut.SyncOrderAsync(order1);
        await sut.SyncOrderAsync(order2);

        var customerCount = await _db.Customers.CountAsync(c => c.ExternalId == "9001");
        customerCount.Should().Be(1, "same Shopify customer ID must reuse existing record");

        var orderCount = await _db.Orders.CountAsync();
        orderCount.Should().Be(2, "two distinct orders must be created");
    }

    [Fact]
    public async Task SyncOrder_StockDecrementsCorrectly()
    {
        var sut = CreateSut();
        var tsRmBefore = (await _db.Products.FirstAsync(p => p.Sku == "TS-RM")).StockQuantity;
        var tsBlBefore = (await _db.Products.FirstAsync(p => p.Sku == "TS-BL")).StockQuantity;

        await sut.SyncOrderAsync(MakeOrder());

        var tsRmAfter = (await _db.Products.FirstAsync(p => p.Sku == "TS-RM")).StockQuantity;
        var tsBlAfter = (await _db.Products.FirstAsync(p => p.Sku == "TS-BL")).StockQuantity;

        tsRmAfter.Should().Be(tsRmBefore - 2);
        tsBlAfter.Should().Be(tsBlBefore - 1);
    }

    [Fact]
    public async Task SyncOrder_GeneratesInvoiceWithCorrectVat()
    {
        var sut = CreateSut();
        var order = MakeOrder();

        await sut.SyncOrderAsync(order);

        var invoice = await _db.Invoices.FirstOrDefaultAsync();
        invoice.Should().NotBeNull();
        invoice!.Vat.Should().Be(order.Vat);
        invoice.Total.Should().Be(order.Total);
    }

    [Fact]
    public async Task SyncOrder_Success_SendsNotification()
    {
        var sut = CreateSut();

        await sut.SyncOrderAsync(MakeOrder());

        _notifications.Verify(n => n.SendSuccessAsync(It.IsAny<string>(), default), Times.Once);
    }

    public void Dispose() => _db.Dispose();
}
