namespace CountOrSell.Core.Entities;

public class SphCatalogSnapshot
{
    public int Id { get; set; }

    /// <summary>Version string from the sphupdate manifest (e.g. "2026.02.27.1430").</summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>Full JSON content of sph-products.json, stored as a text blob.</summary>
    public string CatalogJson { get; set; } = string.Empty;

    /// <summary>Number of products in this snapshot.</summary>
    public int ProductCount { get; set; }

    /// <summary>UTC timestamp when this snapshot was downloaded and applied.</summary>
    public DateTime AppliedAt { get; set; }
}
