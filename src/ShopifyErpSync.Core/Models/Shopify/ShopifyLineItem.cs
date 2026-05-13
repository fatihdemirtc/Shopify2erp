using System.Text.Json.Serialization;

namespace ShopifyErpSync.Core.Models.Shopify;

public class ShopifyLineItem
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("variant_id")]
    public long? VariantId { get; set; }

    [JsonPropertyName("sku")]
    public string? Sku { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }

    [JsonPropertyName("price")]
    [System.Text.Json.Serialization.JsonNumberHandling(System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString)]
    public decimal Price { get; set; }
}
