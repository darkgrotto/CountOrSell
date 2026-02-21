namespace MtgHelper.Core.Entities;

public class SetTag
{
    public int Id { get; set; }

    /// <summary>Set code (e.g. "khm"), FK → CachedSets.Code</summary>
    public string SetCode { get; set; } = string.Empty;

    /// <summary>Tag value — one of the constants in <see cref="Models.KnownSetTags"/></summary>
    public string Tag { get; set; } = string.Empty;

    // Navigation
    public CachedSet Set { get; set; } = null!;
}
