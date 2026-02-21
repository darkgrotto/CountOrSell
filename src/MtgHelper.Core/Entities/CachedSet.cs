namespace MtgHelper.Core.Entities;

public class CachedSet
{
    public string Id { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ReleasedAt { get; set; }
    public string SetType { get; set; } = string.Empty;
    public int CardCount { get; set; }
    public string? IconSvgUri { get; set; }
    public string? ScryfallUri { get; set; }
    public DateTime LastSyncedAt { get; set; }

    public ICollection<SetTag> Tags { get; set; } = new List<SetTag>();
}
