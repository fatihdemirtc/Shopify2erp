using System.Text.Json.Serialization;

namespace ShopifyErpSync.Core.Models.Shopify;

public class ShopifyOrder
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("order_number")]
    public required string OrderNumber { get; set; }

    [JsonPropertyName("customer")]
    public required ShopifyCustomer Customer { get; set; }

    [JsonPropertyName("line_items")]
    public IList<ShopifyLineItem> LineItems { get; set; } = new List<ShopifyLineItem>();

    [JsonPropertyName("subtotal")]
    public decimal Subtotal { get; set; }

    [JsonPropertyName("vat")]
    public decimal Vat { get; set; }

    [JsonPropertyName("total")]
    public decimal Total { get; set; }
}
