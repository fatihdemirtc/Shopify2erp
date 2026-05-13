namespace ShopifyErpSync.Infrastructure.Adapters.Odoo;

public class OdooSettings
{
    public string BaseUrl { get; set; } = "http://localhost:8069";
    public string Database { get; set; } = "odoo";
    public string Username { get; set; } = "admin";
    public string ApiKey { get; set; } = "";
    public int StockLocationId { get; set; } = 8;
}
