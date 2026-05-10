namespace ShopifyErpSync.Core.Models.Erp;

public class Order
{
    public int Id { get; set; }
    public long ShopifyOrderId { get; set; }
    public required string OrderNumber { get; set; }
    public int CustomerId { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Vat { get; set; }
    public decimal Total { get; set; }
    public required string Status { get; set; } = "pending";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Customer Customer { get; set; } = null!;
    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    public Invoice? Invoice { get; set; }
}
