namespace CountOrSell.Core.Entities;

public class SlabbedCard
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string ScryfallCardId { get; set; } = string.Empty;
    public string CardName { get; set; } = string.Empty;
    public string SetCode { get; set; } = string.Empty;
    public string SetName { get; set; } = string.Empty;
    public string CollectorNumber { get; set; } = string.Empty;
    public string CardVariant { get; set; } = string.Empty;   // e.g. "Foil", "Fracture Foil"
    public string GradingCompany { get; set; } = string.Empty; // PSA, BGS, CGC, SGC, GAI, CSG
    public string Grade { get; set; } = string.Empty;          // "9", "9.5", "10"
    public string CertificationNumber { get; set; } = string.Empty;
    public DateTime? PurchaseDate { get; set; }
    public string? PurchasedFrom { get; set; }
    public decimal? PurchaseCost { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
