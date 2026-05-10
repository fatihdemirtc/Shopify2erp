namespace ShopifyErpSync.Core.Models.Erp;

public class Invoice
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public required string InvoiceNumber { get; set; }
    public DateTime InvoiceDate { get; set; } = DateTime.UtcNow;
    public decimal Total { get; set; }
    public decimal Vat { get; set; }
    public required string Status { get; set; } = "created";

    public Order Order { get; set; } = null!;
}
