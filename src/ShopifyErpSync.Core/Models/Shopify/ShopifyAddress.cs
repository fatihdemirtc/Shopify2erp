using System.Text.Json.Serialization;

namespace ShopifyErpSync.Core.Models.Shopify;

public class ShopifyAddress
{
    [JsonPropertyName("first_name")]
    public string? FirstName { get; set; }

    [JsonPropertyName("last_name")]
    public string? LastName { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("phone")]
    public string? Phone { get; set; }

    public string Name => $"{FirstName} {LastName}".Trim();
}
