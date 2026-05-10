using System.Text.Json.Serialization;

namespace ShopifyErpSync.Core.Models.Shopify;

public class ShopifyOrder
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("order_number")]
    public long OrderNumber { get; set; }

    [JsonPropertyName("total_price")]
    [System.Text.Json.Serialization.JsonNumberHandling(System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString)]
    public decimal Total { get; set; }

    [JsonPropertyName("subtotal_price")]
    [System.Text.Json.Serialization.JsonNumberHandling(System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString)]
    public decimal Subtotal { get; set; }

    [JsonPropertyName("total_tax")]
    [System.Text.Json.Serialization.JsonNumberHandling(System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString)]
    public decimal Vat { get; set; }

    [JsonPropertyName("customer")]
    public ShopifyCustomer? Customer { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("billing_address")]
    public ShopifyAddress? BillingAddress { get; set; }

    [JsonPropertyName("line_items")]
    public IList<ShopifyLineItem> LineItems { get; set; } = new List<ShopifyLineItem>();

}
