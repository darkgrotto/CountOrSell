using System.Text.Json.Serialization;

namespace MtgHelper.Core.Models;

public class MtgSet
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("released_at")]
    public string? ReleasedAt { get; set; }

    [JsonPropertyName("set_type")]
    public string SetType { get; set; } = string.Empty;

    [JsonPropertyName("card_count")]
    public int CardCount { get; set; }

    [JsonPropertyName("icon_svg_uri")]
    public string? IconSvgUri { get; set; }

    [JsonPropertyName("scryfall_uri")]
    public string? ScryfallUri { get; set; }

    [JsonPropertyName("search_uri")]
    public string? SearchUri { get; set; }
}

public class ScryfallSetListResponse
{
    [JsonPropertyName("object")]
    public string Object { get; set; } = string.Empty;

    [JsonPropertyName("has_more")]
    public bool HasMore { get; set; }

    [JsonPropertyName("data")]
    public List<MtgSet> Data { get; set; } = new();
}
