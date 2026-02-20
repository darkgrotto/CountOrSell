namespace MtgHelper.Core.Entities;

public class CachedCard
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string SetCode { get; set; } = string.Empty;
    public string SetName { get; set; } = string.Empty;
    public string CollectorNumber { get; set; } = string.Empty;
    public string Rarity { get; set; } = string.Empty;
    public string? TypeLine { get; set; }
    public string? ManaCost { get; set; }
    public string? OracleText { get; set; }
    public string? ColorIdentity { get; set; }
    public string? ImageUrisJson { get; set; }
    public string? CardFacesJson { get; set; }
    public string? PriceUsd { get; set; }
    public string? PriceUsdFoil { get; set; }
    public string? ScryfallUri { get; set; }
    public bool IsReserved { get; set; }
    public string? LocalImagePath { get; set; }
    public DateTime LastSyncedAt { get; set; }
}
