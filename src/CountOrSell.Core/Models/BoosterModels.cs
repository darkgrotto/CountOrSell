using System.Text.Json.Serialization;

namespace CountOrSell.Core.Models;

public class BoosterRequest
{
    [JsonPropertyName("setCode")]
    public string SetCode { get; set; } = string.Empty;

    [JsonPropertyName("boosterType")]
    public string BoosterType { get; set; } = string.Empty;

    [JsonPropertyName("artVariant")]
    public string ArtVariant { get; set; } = string.Empty;

    [JsonPropertyName("imageUrl")]
    public string? ImageUrl { get; set; }
}

public class BoosterOwnedRequest
{
    [JsonPropertyName("owned")]
    public bool Owned { get; set; }
}

public class BoosterResponse
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("setCode")]
    public string SetCode { get; set; } = string.Empty;

    [JsonPropertyName("boosterType")]
    public string BoosterType { get; set; } = string.Empty;

    [JsonPropertyName("artVariant")]
    public string ArtVariant { get; set; } = string.Empty;

    [JsonPropertyName("imageUrl")]
    public string? ImageUrl { get; set; }

    [JsonPropertyName("owned")]
    public bool Owned { get; set; }
}
