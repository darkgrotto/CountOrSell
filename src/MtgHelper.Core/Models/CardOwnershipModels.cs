using System.Text.Json.Serialization;

namespace MtgHelper.Core.Models;

public class CardOwnedRequest
{
    [JsonPropertyName("owned")]
    public bool Owned { get; set; }

    [JsonPropertyName("cardName")]
    public string CardName { get; set; } = string.Empty;

    [JsonPropertyName("setCode")]
    public string SetCode { get; set; } = string.Empty;

    [JsonPropertyName("collectorNumber")]
    public string CollectorNumber { get; set; } = string.Empty;
}

public class BulkCardOwnedRequest
{
    [JsonPropertyName("setCode")]
    public string SetCode { get; set; } = string.Empty;

    [JsonPropertyName("scryfallCardIds")]
    public List<BulkCardEntry> Cards { get; set; } = new();

    [JsonPropertyName("owned")]
    public bool Owned { get; set; }
}

public class BulkCardEntry
{
    [JsonPropertyName("scryfallCardId")]
    public string ScryfallCardId { get; set; } = string.Empty;

    [JsonPropertyName("cardName")]
    public string CardName { get; set; } = string.Empty;

    [JsonPropertyName("collectorNumber")]
    public string CollectorNumber { get; set; } = string.Empty;
}
