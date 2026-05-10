using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ShopifyErpSync.Core.Interfaces;
using ShopifyErpSync.Core.Models.Shopify;
using ShopifyErpSync.Infrastructure.Adapters;
using ShopifyErpSync.Infrastructure.Data;
using ShopifyErpSync.Infrastructure.Services;

namespace ShopifyErpSync.Tests;

public class IdempotencyTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly OrderSyncService _sut;

    public IdempotencyTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _db = new AppDbContext(options);

        var adapter = new SqlErpAdapter(_db, NullLogger<SqlErpAdapter>.Instance);
        var notifications = new Mock<INotificationService>();
        var audit = new Mock<IAuditService>();

        _sut = new OrderSyncService(
            _db, adapter, notifications.Object, audit.Object,
            NullLogger<OrderSyncService>.Instance);

        SeedProduct();
    }

    private void SeedProduct()
    {
        _db.Products.Add(new ShopifyErpSync.Core.Models.Erp.Product
        {
            Sku = "TS-RM",
            Name = "Red T-Shirt M",
            StockQuantity = 50,
            Price = 29.99m
        });
        _db.SaveChanges();
    }

    private static ShopifyOrder MakeOrder(long id, string orderNumber) => new()
    {
        Id = id,
        OrderNumber = orderNumber,
        Customer = new ShopifyCustomer { Id = 9999, Name = "Test User", Email = "test@example.com" },
        LineItems = new List<ShopifyLineItem>
        {
            new() { Sku = "TS-RM", Name = "Red T-Shirt M", Quantity = 1, Price = 29.99m }
        },
        Subtotal = 29.99m,
        Vat = 5.40m,
        Total = 35.39m
    };

    [Fact]
    public async Task SyncOrder_SameOrderTwice_CreatesOnlyOneRecord()
    {
        var order = MakeOrder(111222333, "1001");

        await _sut.SyncOrderAsync(order);
        await _sut.SyncOrderAsync(order);

        var count = await _db.Orders.CountAsync(o => o.ShopifyOrderId == order.Id);
        count.Should().Be(1);
    }

    [Fact]
    public async Task SyncOrder_DifferentOrders_CreatesSeparateRecords()
    {
        var order1 = MakeOrder(111000001, "1001");
        var order2 = MakeOrder(111000002, "1002");

        await _sut.SyncOrderAsync(order1);
        await _sut.SyncOrderAsync(order2);

        var count = await _db.Orders.CountAsync();
        count.Should().Be(2);
    }

    [Fact]
    public async Task SyncOrder_Duplicate_DoesNotDecrementStockTwice()
    {
        var order = MakeOrder(999888777, "1003");
        var stockBefore = (await _db.Products.FirstAsync(p => p.Sku == "TS-RM")).StockQuantity;

        await _sut.SyncOrderAsync(order);
        var stockAfterFirst = (await _db.Products.FirstAsync(p => p.Sku == "TS-RM")).StockQuantity;

        await _sut.SyncOrderAsync(order);
        var stockAfterSecond = (await _db.Products.FirstAsync(p => p.Sku == "TS-RM")).StockQuantity;

        stockAfterFirst.Should().Be(stockBefore - 1);
        stockAfterSecond.Should().Be(stockAfterFirst, "duplicate sync must not decrement again");
    }

    public void Dispose() => _db.Dispose();
}
