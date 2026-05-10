namespace ShopifyErpSync.Core.Models.Erp;

public class Customer
{
    public int Id { get; set; }
    public required string ExternalId { get; set; }
    public required string Name { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Order> Orders { get; set; } = new List<Order>();
}
