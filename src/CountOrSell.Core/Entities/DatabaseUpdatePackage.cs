namespace CountOrSell.Core.Entities;

public class DatabaseUpdatePackage
{
    public int Id { get; set; }
    public string Version { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? Description { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string Checksum { get; set; } = string.Empty;
    public bool IsApplied { get; set; }
    public DateTime? AppliedAt { get; set; }
}
