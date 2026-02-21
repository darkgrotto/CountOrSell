namespace CountOrSell.Core.Entities;

public class BoosterDefinition
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string SetCode { get; set; } = string.Empty;
    public string BoosterType { get; set; } = string.Empty;
    public string ArtVariant { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public bool Owned { get; set; }
}
