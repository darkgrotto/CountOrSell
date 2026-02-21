using System.IO.Compression;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using CountOrSell.Core.Data;
using CountOrSell.Core.Entities;

namespace CountOrSell.Api.Controllers;

[ApiController]
[Route("api/updates")]
public class UpdatesController : ControllerBase
{
    private readonly CountOrSellDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private const string ManifestUrl = "https://www.countorsell.com/dbupdate";

    public UpdatesController(CountOrSellDbContext db, IHttpClientFactory httpClientFactory)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
    }

    [HttpGet("check")]
    public async Task<IActionResult> Check()
    {
        // Get local current version
        var localVersion = await _db.DatabaseUpdatePackages
            .Where(p => p.IsApplied)
            .OrderByDescending(p => p.AppliedAt)
            .Select(p => p.Version)
            .FirstOrDefaultAsync() ?? "0";

        RemoteManifest? manifest;
        try
        {
            var client = _httpClientFactory.CreateClient();
            var response = await client.GetAsync(ManifestUrl);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            manifest = JsonSerializer.Deserialize<RemoteManifest>(json, JsonOptions);
        }
        catch
        {
            return Ok(new
            {
                updateAvailable = false,
                currentVersion = localVersion,
                error = "Could not reach update server"
            });
        }

        if (manifest == null || string.Compare(manifest.CurrentVersion, localVersion, StringComparison.Ordinal) <= 0)
        {
            return Ok(new
            {
                updateAvailable = false,
                currentVersion = localVersion
            });
        }

        // Find best package: prefer delta matching local version
        var bestPackage = manifest.Packages?
            .FirstOrDefault(p => p.Type == "delta" && p.FromVersion == localVersion);
        bestPackage ??= manifest.Packages?.FirstOrDefault(p => p.Type == "full");

        if (bestPackage == null)
        {
            return Ok(new
            {
                updateAvailable = false,
                currentVersion = localVersion,
                error = "No compatible package found"
            });
        }

        return Ok(new
        {
            updateAvailable = true,
            currentVersion = localVersion,
            availableVersion = manifest.CurrentVersion,
            packageType = bestPackage.Type,
            description = bestPackage.Description,
            fileSizeBytes = bestPackage.FileSizeBytes
        });
    }

    [HttpPost("apply")]
    [Authorize]
    public async Task<IActionResult> Apply()
    {
        var localVersion = await _db.DatabaseUpdatePackages
            .Where(p => p.IsApplied)
            .OrderByDescending(p => p.AppliedAt)
            .Select(p => p.Version)
            .FirstOrDefaultAsync() ?? "0";

        RemoteManifest? manifest;
        try
        {
            var client = _httpClientFactory.CreateClient();
            var response = await client.GetAsync(ManifestUrl);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            manifest = JsonSerializer.Deserialize<RemoteManifest>(json, JsonOptions);
        }
        catch
        {
            return StatusCode(502, new { error = "Could not reach update server" });
        }

        if (manifest == null || string.Compare(manifest.CurrentVersion, localVersion, StringComparison.Ordinal) <= 0)
        {
            return Ok(new { applied = false, message = "Already up to date" });
        }

        // Find best package
        var bestPackage = manifest.Packages?
            .FirstOrDefault(p => p.Type == "delta" && p.FromVersion == localVersion);
        bestPackage ??= manifest.Packages?.FirstOrDefault(p => p.Type == "full");

        if (bestPackage?.DownloadUrl == null)
        {
            return StatusCode(502, new { error = "No compatible package found" });
        }

        var tempDir = Path.Combine(Path.GetTempPath(), $"countorsell-update-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            // Download the ZIP
            var client = _httpClientFactory.CreateClient();
            var zipBytes = await client.GetByteArrayAsync(bestPackage.DownloadUrl);
            var zipPath = Path.Combine(tempDir, "update.zip");
            await System.IO.File.WriteAllBytesAsync(zipPath, zipBytes);

            // Extract
            var extractDir = Path.Combine(tempDir, "extracted");
            ZipFile.ExtractToDirectory(zipPath, extractDir);

            var dataDbPath = Path.Combine(extractDir, "data.db");
            if (!System.IO.File.Exists(dataDbPath))
            {
                return StatusCode(502, new { error = "Package missing data.db" });
            }

            // Read and upsert from the downloaded data.db
            var setsUpdated = 0;
            var cardsUpdated = 0;

            using var sourceConn = new SqliteConnection($"Data Source={dataDbPath}");
            await sourceConn.OpenAsync();

            // Upsert CachedSets
            using (var cmd = sourceConn.CreateCommand())
            {
                cmd.CommandText = "SELECT Id, Code, Name, ReleasedAt, SetType, CardCount, IconSvgUri, ScryfallUri, LastSyncedAt FROM CachedSets";
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var id = reader.GetString(0);
                    var existing = await _db.CachedSets.FindAsync(id);
                    if (existing != null)
                    {
                        existing.Code = reader.GetString(1);
                        existing.Name = reader.GetString(2);
                        existing.ReleasedAt = reader.IsDBNull(3) ? null : reader.GetString(3);
                        existing.SetType = reader.GetString(4);
                        existing.CardCount = reader.GetInt32(5);
                        existing.IconSvgUri = reader.IsDBNull(6) ? null : reader.GetString(6);
                        existing.ScryfallUri = reader.IsDBNull(7) ? null : reader.GetString(7);
                        existing.LastSyncedAt = DateTime.Parse(reader.GetString(8));
                    }
                    else
                    {
                        _db.CachedSets.Add(new CachedSet
                        {
                            Id = id,
                            Code = reader.GetString(1),
                            Name = reader.GetString(2),
                            ReleasedAt = reader.IsDBNull(3) ? null : reader.GetString(3),
                            SetType = reader.GetString(4),
                            CardCount = reader.GetInt32(5),
                            IconSvgUri = reader.IsDBNull(6) ? null : reader.GetString(6),
                            ScryfallUri = reader.IsDBNull(7) ? null : reader.GetString(7),
                            LastSyncedAt = DateTime.Parse(reader.GetString(8))
                        });
                    }
                    setsUpdated++;
                }
            }
            await _db.SaveChangesAsync();

            // Upsert CachedCards in batches
            using (var cmd = sourceConn.CreateCommand())
            {
                cmd.CommandText = "SELECT Id, Name, SetCode, SetName, CollectorNumber, Rarity, TypeLine, ManaCost, OracleText, ColorIdentity, ImageUrisJson, CardFacesJson, PriceUsd, PriceUsdFoil, ScryfallUri, IsReserved, LocalImagePath, LastSyncedAt FROM CachedCards";
                using var reader = await cmd.ExecuteReaderAsync();
                var batch = 0;
                while (await reader.ReadAsync())
                {
                    var id = reader.GetString(0);
                    var existing = await _db.CachedCards.FindAsync(id);
                    if (existing != null)
                    {
                        existing.Name = reader.GetString(1);
                        existing.SetCode = reader.GetString(2);
                        existing.SetName = reader.GetString(3);
                        existing.CollectorNumber = reader.GetString(4);
                        existing.Rarity = reader.GetString(5);
                        existing.TypeLine = reader.IsDBNull(6) ? null : reader.GetString(6);
                        existing.ManaCost = reader.IsDBNull(7) ? null : reader.GetString(7);
                        existing.OracleText = reader.IsDBNull(8) ? null : reader.GetString(8);
                        existing.ColorIdentity = reader.IsDBNull(9) ? null : reader.GetString(9);
                        existing.ImageUrisJson = reader.IsDBNull(10) ? null : reader.GetString(10);
                        existing.CardFacesJson = reader.IsDBNull(11) ? null : reader.GetString(11);
                        existing.PriceUsd = reader.IsDBNull(12) ? null : reader.GetString(12);
                        existing.PriceUsdFoil = reader.IsDBNull(13) ? null : reader.GetString(13);
                        existing.ScryfallUri = reader.IsDBNull(14) ? null : reader.GetString(14);
                        existing.IsReserved = reader.GetBoolean(15);
                        existing.LocalImagePath = reader.IsDBNull(16) ? null : reader.GetString(16);
                        existing.LastSyncedAt = DateTime.Parse(reader.GetString(17));
                    }
                    else
                    {
                        _db.CachedCards.Add(new CachedCard
                        {
                            Id = id,
                            Name = reader.GetString(1),
                            SetCode = reader.GetString(2),
                            SetName = reader.GetString(3),
                            CollectorNumber = reader.GetString(4),
                            Rarity = reader.GetString(5),
                            TypeLine = reader.IsDBNull(6) ? null : reader.GetString(6),
                            ManaCost = reader.IsDBNull(7) ? null : reader.GetString(7),
                            OracleText = reader.IsDBNull(8) ? null : reader.GetString(8),
                            ColorIdentity = reader.IsDBNull(9) ? null : reader.GetString(9),
                            ImageUrisJson = reader.IsDBNull(10) ? null : reader.GetString(10),
                            CardFacesJson = reader.IsDBNull(11) ? null : reader.GetString(11),
                            PriceUsd = reader.IsDBNull(12) ? null : reader.GetString(12),
                            PriceUsdFoil = reader.IsDBNull(13) ? null : reader.GetString(13),
                            ScryfallUri = reader.IsDBNull(14) ? null : reader.GetString(14),
                            IsReserved = reader.GetBoolean(15),
                            LocalImagePath = reader.IsDBNull(16) ? null : reader.GetString(16),
                            LastSyncedAt = DateTime.Parse(reader.GetString(17))
                        });
                    }
                    cardsUpdated++;
                    batch++;

                    if (batch >= 1000)
                    {
                        await _db.SaveChangesAsync();
                        batch = 0;
                    }
                }
            }
            await _db.SaveChangesAsync();

            // Record the applied version
            _db.DatabaseUpdatePackages.Add(new DatabaseUpdatePackage
            {
                Version = manifest.CurrentVersion,
                Description = $"Applied {bestPackage.Type} update: {setsUpdated} sets, {cardsUpdated} cards",
                FilePath = "",
                FileSizeBytes = zipBytes.Length,
                Checksum = "",
                IsApplied = true,
                AppliedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();

            return Ok(new
            {
                applied = true,
                version = manifest.CurrentVersion,
                setsUpdated,
                cardsUpdated
            });
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { /* cleanup best effort */ }
        }
    }

    [HttpGet("history")]
    public async Task<IActionResult> History()
    {
        var packages = await _db.DatabaseUpdatePackages
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new
            {
                p.Id,
                p.Version,
                p.Description,
                p.CreatedAt,
                p.IsApplied,
                p.AppliedAt,
                p.FileSizeBytes
            })
            .ToListAsync();

        return Ok(packages);
    }

    private record RemoteManifest(string CurrentVersion, List<RemotePackage>? Packages);
    private record RemotePackage(
        string Version, string Type, string? FromVersion,
        string? Description, string? DownloadUrl, long FileSizeBytes, string? Checksum);
}
