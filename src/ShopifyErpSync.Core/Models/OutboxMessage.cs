namespace ShopifyErpSync.Core.Models;

public class OutboxMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string MessageType { get; set; }
    public required string Payload { get; set; }
    public required string Status { get; set; } = "pending";
    public int AttemptCount { get; set; }
    public DateTime? NextRetryAt { get; set; }
    public DateTime? LastAttemptAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
