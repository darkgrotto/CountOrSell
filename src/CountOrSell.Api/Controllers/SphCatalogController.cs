using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CountOrSell.Core.Data;
using CountOrSell.Core.Entities;

namespace CountOrSell.Api.Controllers;

[ApiController]
[Route("api/sphcatalog")]
public class SphCatalogController : ControllerBase
{
    private readonly CountOrSellDbContext _db;
    private readonly IHttpClientFactory _http;

    private const string ManifestUrl = "https://www.countorsell.com/sphupdate";

    public SphCatalogController(CountOrSellDbContext db, IHttpClientFactory http)
    {
        _db = db;
        _http = http;
    }

    /// <summary>
    /// Returns the current and remote sphupdate versions so the admin can decide
    /// whether to apply an update.
    /// </summary>
    [HttpGet("check")]
    [Authorize]
    public async Task<IActionResult> Check()
    {
        var local = await _db.SphCatalogSnapshots
            .OrderByDescending(s => s.AppliedAt)
            .FirstOrDefaultAsync();

        string? remoteVersion = null;
        int? remoteProductCount = null;
        string? remoteCatalogUrl = null;
        string? fetchError = null;

        try
        {
            using var client = _http.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(10);
            var json = await client.GetStringAsync(ManifestUrl);
            using var doc = JsonDocument.Parse(json);
            doc.RootElement.TryGetProperty("currentVersion", out var ver);
            remoteVersion = ver.GetString();
            if (doc.RootElement.TryGetProperty("productCount", out var pc))
                remoteProductCount = pc.GetInt32();
            if (doc.RootElement.TryGetProperty("catalogUrl", out var cu))
                remoteCatalogUrl = cu.GetString();
        }
        catch (Exception ex)
        {
            fetchError = ex.Message;
        }

        return Ok(new
        {
            localVersion = local?.Version,
            localProductCount = local?.ProductCount,
            localAppliedAt = local?.AppliedAt,
            remoteVersion,
            remoteProductCount,
            remoteCatalogUrl,
            updateAvailable = remoteVersion != null && remoteVersion != local?.Version,
            fetchError,
        });
    }

    /// <summary>
    /// Downloads the latest sph-products.json and stores it as a SphCatalogSnapshot.
    /// Admin only.
    /// </summary>
    [HttpPost("apply")]
    [Authorize]
    public async Task<IActionResult> Apply()
    {
        if (!User.IsInRole("Admin") && !User.HasClaim("IsAdmin", "true"))
        {
            // Fallback: check admin flag via the Users table
            var username = User.Identity?.Name;
            var user = username != null
                ? await _db.Users.FirstOrDefaultAsync(u => u.Username == username)
                : null;
            if (user == null || !user.IsAdmin)
                return Forbid();
        }

        // Fetch the manifest to get the catalog URL and version
        string manifestJson;
        try
        {
            using var client = _http.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(10);
            manifestJson = await client.GetStringAsync(ManifestUrl);
        }
        catch (Exception ex)
        {
            return StatusCode(502, new { error = $"Failed to fetch sphupdate manifest: {ex.Message}" });
        }

        string? version;
        string? catalogUrl;
        using (var doc = JsonDocument.Parse(manifestJson))
        {
            doc.RootElement.TryGetProperty("currentVersion", out var ver);
            version = ver.GetString();
            doc.RootElement.TryGetProperty("catalogUrl", out var cu);
            catalogUrl = cu.GetString();
        }

        if (string.IsNullOrEmpty(catalogUrl))
            return BadRequest(new { error = "sphupdate manifest does not contain a catalogUrl." });

        // Download the catalog JSON
        string catalogJson;
        try
        {
            using var client = _http.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(60);
            catalogJson = await client.GetStringAsync(catalogUrl);
        }
        catch (Exception ex)
        {
            return StatusCode(502, new { error = $"Failed to download catalog from {catalogUrl}: {ex.Message}" });
        }

        int productCount = 0;
        try
        {
            using var doc = JsonDocument.Parse(catalogJson);
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
                productCount = doc.RootElement.GetArrayLength();
        }
        catch { /* leave count at 0 */ }

        var snapshot = new SphCatalogSnapshot
        {
            Version = version ?? "unknown",
            CatalogJson = catalogJson,
            ProductCount = productCount,
            AppliedAt = DateTime.UtcNow,
        };
        _db.SphCatalogSnapshots.Add(snapshot);
        await _db.SaveChangesAsync();

        return Ok(new
        {
            version = snapshot.Version,
            productCount = snapshot.ProductCount,
            appliedAt = snapshot.AppliedAt,
        });
    }

    /// <summary>
    /// Returns the locally cached SPH catalog.
    /// Used when SPH_API_URL is not configured (static / offline mode).
    /// </summary>
    [HttpGet("products")]
    [Authorize]
    public async Task<IActionResult> Products()
    {
        var snapshot = await _db.SphCatalogSnapshots
            .OrderByDescending(s => s.AppliedAt)
            .FirstOrDefaultAsync();

        if (snapshot == null)
            return Ok(Array.Empty<object>());

        // Return the raw JSON array directly
        return Content(snapshot.CatalogJson, "application/json");
    }
}
