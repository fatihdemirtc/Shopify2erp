using System.Text.Json.Serialization;

namespace ShopifyErpSync.Core.Models.Shopify;

public class ShopifyLineItem
{
    [JsonPropertyName("sku")]
    public required string Sku { get; set; }

    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }

    [JsonPropertyName("price")]
    public decimal Price { get; set; }
}
