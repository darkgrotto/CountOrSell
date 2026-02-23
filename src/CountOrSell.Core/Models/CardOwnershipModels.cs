using System.Text.Json.Serialization;

namespace CountOrSell.Core.Models;

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

public class CardOwnershipEntry
{
    [JsonPropertyName("scryfallCardId")]
    public string ScryfallCardId { get; set; } = string.Empty;

    [JsonPropertyName("variant")]
    public string Variant { get; set; } = string.Empty;

    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }
}

public class CardVariantQuantityRequest
{
    [JsonPropertyName("variant")]
    public string Variant { get; set; } = "Regular";

    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }

    [JsonPropertyName("cardName")]
    public string CardName { get; set; } = string.Empty;

    [JsonPropertyName("setCode")]
    public string SetCode { get; set; } = string.Empty;

    [JsonPropertyName("collectorNumber")]
    public string CollectorNumber { get; set; } = string.Empty;
}

public class ImportResult
{
    [JsonPropertyName("imported")]
    public int Imported { get; set; }

    [JsonPropertyName("unmatched")]
    public int Unmatched { get; set; }

    [JsonPropertyName("unmatchedCards")]
    public List<string> UnmatchedCards { get; set; } = new();

    [JsonPropertyName("detectedFormat")]
    public string? DetectedFormat { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}
