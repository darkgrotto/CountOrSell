namespace CountOrSell.Core.Entities;

public class CardOwnership
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string ScryfallCardId { get; set; } = string.Empty;
    public string CardName { get; set; } = string.Empty;
    public string SetCode { get; set; } = string.Empty;
    public string CollectorNumber { get; set; } = string.Empty;
    public bool Owned { get; set; }
}
