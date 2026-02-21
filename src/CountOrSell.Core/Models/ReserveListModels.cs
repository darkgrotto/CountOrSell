using System.Text.Json.Serialization;

namespace CountOrSell.Core.Models;

public class ReserveListCardResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("set")]
    public string Set { get; set; } = string.Empty;

    [JsonPropertyName("set_name")]
    public string SetName { get; set; } = string.Empty;

    [JsonPropertyName("collector_number")]
    public string CollectorNumber { get; set; } = string.Empty;

    [JsonPropertyName("rarity")]
    public string Rarity { get; set; } = string.Empty;

    [JsonPropertyName("type_line")]
    public string? TypeLine { get; set; }

    [JsonPropertyName("mana_cost")]
    public string? ManaCost { get; set; }

    [JsonPropertyName("image_uris")]
    public ImageUris? ImageUris { get; set; }

    [JsonPropertyName("card_faces")]
    public List<CardFace>? CardFaces { get; set; }

    [JsonPropertyName("prices")]
    public CardPrices? Prices { get; set; }

    [JsonPropertyName("scryfall_uri")]
    public string? ScryfallUri { get; set; }

    [JsonPropertyName("owned")]
    public bool Owned { get; set; }
}

public class ReserveListOwnedRequest
{
    [JsonPropertyName("owned")]
    public bool Owned { get; set; }

    [JsonPropertyName("cardName")]
    public string CardName { get; set; } = string.Empty;

    [JsonPropertyName("setCode")]
    public string SetCode { get; set; } = string.Empty;
}
