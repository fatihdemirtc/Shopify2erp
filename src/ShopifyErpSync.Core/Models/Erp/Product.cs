namespace ShopifyErpSync.Core.Models.Erp;

public class Product
{
    public int Id { get; set; }
    public required string Sku { get; set; }
    public required string Name { get; set; }
    public int StockQuantity { get; set; }
    public decimal Price { get; set; }
}
