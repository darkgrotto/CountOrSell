using System.Text.Json.Serialization;

namespace CountOrSell.Core.Models;

public class CollectionCardEntry
{
    [JsonPropertyName("scryfallCardId")]
    public string ScryfallCardId { get; set; } = "";

    [JsonPropertyName("cardName")]
    public string CardName { get; set; } = "";

    [JsonPropertyName("setCode")]
    public string SetCode { get; set; } = "";

    [JsonPropertyName("setName")]
    public string SetName { get; set; } = "";

    [JsonPropertyName("collectorNumber")]
    public string CollectorNumber { get; set; } = "";

    [JsonPropertyName("variant")]
    public string Variant { get; set; } = "";

    [JsonPropertyName("rarity")]
    public string Rarity { get; set; } = "";

    [JsonPropertyName("typeLine")]
    public string? TypeLine { get; set; }

    [JsonPropertyName("colorIdentity")]
    public string? ColorIdentity { get; set; }

    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }

    [JsonPropertyName("priceUsd")]
    public decimal? PriceUsd { get; set; }

    [JsonPropertyName("isReserved")]
    public bool IsReserved { get; set; }
}

public class CollectionSummary
{
    [JsonPropertyName("totalCopies")]
    public int TotalCopies { get; set; }

    [JsonPropertyName("totalUniqueCards")]
    public int TotalUniqueCards { get; set; }

    [JsonPropertyName("totalValue")]
    public decimal TotalValue { get; set; }

    [JsonPropertyName("byRarity")]
    public Dictionary<string, int> ByRarity { get; set; } = new();

    [JsonPropertyName("valueByRarity")]
    public Dictionary<string, decimal> ValueByRarity { get; set; } = new();

    [JsonPropertyName("byType")]
    public Dictionary<string, int> ByType { get; set; } = new();

    [JsonPropertyName("byVariant")]
    public Dictionary<string, int> ByVariant { get; set; } = new();

    [JsonPropertyName("reserveListOwned")]
    public int ReserveListOwned { get; set; }

    [JsonPropertyName("reserveListValue")]
    public decimal ReserveListValue { get; set; }

    [JsonPropertyName("boostersOwned")]
    public int BoostersOwned { get; set; }

    [JsonPropertyName("boostersTotal")]
    public int BoostersTotal { get; set; }
}

public class CollectionFilter
{
    public string Rarity  { get; set; } = "all";
    public string Type    { get; set; } = "all";
    public string Color   { get; set; } = "all";
    public string Variant { get; set; } = "all";
    public string SetCode { get; set; } = "all";
}
