using Microsoft.AspNetCore.Mvc;
using ShopifyErpSync.Core.Interfaces;
using ShopifyErpSync.Core.Models;

namespace ShopifyErpSync.Api.Controllers;

[ApiController]
[Route("api/shopify")]
public class ShopifyWebhookController : ControllerBase
{
    private readonly IOutboxRepository _outbox;
    private readonly IAuditService _audit;
    private readonly ILogger<ShopifyWebhookController> _logger;

    public ShopifyWebhookController(
        IOutboxRepository outbox,
        IAuditService audit,
        ILogger<ShopifyWebhookController> logger)
    {
        _outbox = outbox;
        _audit = audit;
        _logger = logger;
    }

    [HttpPost("order-created")]
    public async Task<IActionResult> OrderCreated()
    {
        Request.EnableBuffering();
        using var reader = new StreamReader(Request.Body, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        Request.Body.Position = 0;

        await _outbox.EnqueueAsync(new OutboxMessage
        {
            MessageType = "ProcessShopifyOrder",
            Payload = body,
            Status = "pending"
        });

        await _audit.LogAsync("WebhookReceived", "Webhook", "order-created",
            $"{body.Length} bytes queued");

        _logger.LogInformation("Shopify order webhook queued");
        return Ok();
    }
}
