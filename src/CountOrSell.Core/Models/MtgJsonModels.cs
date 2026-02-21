using System.Text.Json.Serialization;

namespace CountOrSell.Core.Models;

public class MtgJsonBoosterType
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("subtype")]
    public string Subtype { get; set; } = string.Empty;
}

public class MtgJsonSetListResponse
{
    [JsonPropertyName("data")]
    public List<MtgJsonSetListEntry> Data { get; set; } = new();
}

public class MtgJsonSetListEntry
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("sealedProduct")]
    public List<MtgJsonSealedProduct> SealedProduct { get; set; } = new();
}

public class MtgJsonSealedProduct
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("subtype")]
    public string Subtype { get; set; } = string.Empty;

    [JsonPropertyName("uuid")]
    public string Uuid { get; set; } = string.Empty;
}
