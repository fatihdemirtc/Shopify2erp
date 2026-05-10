using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ShopifyErpSync.Core.Interfaces;
using ShopifyErpSync.Core.Models;
using ShopifyErpSync.Infrastructure.Outbox;

namespace ShopifyErpSync.Tests;

public class OutboxProcessorTests
{
    private readonly Mock<IOutboxRepository> _outbox = new();
    private readonly Mock<IOrderSyncService> _orderSync = new();
    private readonly OutboxMessageProcessor _sut;

    private const string ValidPayload = """
        {
            "id": 12345678901,
            "order_number": "1001",
            "customer": { "id": 9876543210, "name": "Test User", "email": "test@example.com" },
            "line_items": [{ "sku": "TS-RM", "name": "Red T-Shirt", "quantity": 1, "price": 29.99 }],
            "subtotal": 29.99, "vat": 5.40, "total": 35.39
        }
        """;

    public OutboxProcessorTests()
    {
        _sut = new OutboxMessageProcessor(NullLogger<OutboxMessageProcessor>.Instance);
    }

    private OutboxMessage MakeMessage(int attemptCount = 0) => new()
    {
        Id = Guid.NewGuid(),
        MessageType = "ProcessShopifyOrder",
        Payload = ValidPayload,
        Status = "pending",
        AttemptCount = attemptCount
    };

    [Fact]
    public async Task ProcessAsync_Success_MarksCompleted()
    {
        var message = MakeMessage();

        await _sut.ProcessAsync(message, _outbox.Object, _orderSync.Object, default);

        _outbox.Verify(r => r.MarkCompletedAsync(message.Id, default), Times.Once);
        _outbox.Verify(r => r.MarkFailedAsync(It.IsAny<Guid>(), It.IsAny<string>(), default), Times.Never);
        _outbox.Verify(r => r.RescheduleAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<string>(), default), Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_FirstFailure_Reschedules()
    {
        var message = MakeMessage(attemptCount: 0);
        _orderSync.Setup(s => s.SyncOrderAsync(It.IsAny<ShopifyErpSync.Core.Models.Shopify.ShopifyOrder>(), default))
                  .ThrowsAsync(new Exception("ERP unavailable"));

        await _sut.ProcessAsync(message, _outbox.Object, _orderSync.Object, default);

        _outbox.Verify(r => r.RescheduleAsync(message.Id, It.IsAny<DateTime>(), "ERP unavailable", default), Times.Once);
        _outbox.Verify(r => r.MarkFailedAsync(It.IsAny<Guid>(), It.IsAny<string>(), default), Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_FifthFailure_MarksAsFailed()
    {
        var message = MakeMessage(attemptCount: OutboxMessageProcessor.MaxAttempts - 1);
        _orderSync.Setup(s => s.SyncOrderAsync(It.IsAny<ShopifyErpSync.Core.Models.Shopify.ShopifyOrder>(), default))
                  .ThrowsAsync(new Exception("Permanent failure"));

        await _sut.ProcessAsync(message, _outbox.Object, _orderSync.Object, default);

        _outbox.Verify(r => r.MarkFailedAsync(message.Id, "Permanent failure", default), Times.Once);
        _outbox.Verify(r => r.RescheduleAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<string>(), default), Times.Never);
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 4)]
    [InlineData(3, 8)]
    [InlineData(4, 16)]
    public void GetRetryDelay_IsExponential(int attempt, int expectedMinutes)
    {
        var delay = OutboxMessageProcessor.GetRetryDelay(attempt);

        delay.TotalMinutes.Should().Be(expectedMinutes);
    }

    [Fact]
    public async Task ProcessAsync_MarksProcessingBeforeSync()
    {
        var message = MakeMessage();
        var callOrder = new List<string>();

        _outbox.Setup(r => r.MarkProcessingAsync(message.Id, default))
               .Callback(() => callOrder.Add("MarkProcessing"))
               .Returns(Task.CompletedTask);
        _orderSync.Setup(s => s.SyncOrderAsync(It.IsAny<ShopifyErpSync.Core.Models.Shopify.ShopifyOrder>(), default))
                  .Callback(() => callOrder.Add("SyncOrder"))
                  .Returns(Task.CompletedTask);

        await _sut.ProcessAsync(message, _outbox.Object, _orderSync.Object, default);

        callOrder.Should().ContainInOrder("MarkProcessing", "SyncOrder");
    }
}
