using System.CommandLine;
using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.EntityFrameworkCore;
using Spectre.Console;
using Spectre.Console.Rendering;
using CountOrSell.Cli.Services;
using CountOrSell.Core.Data;
using CountOrSell.Core.Entities;
using CountOrSell.Core.Models;

// --- Shared setup ---
static string FindRepoRoot()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir != null)
    {
        if (Directory.Exists(Path.Combine(dir.FullName, ".git")))
            return dir.FullName;
        dir = dir.Parent;
    }
    throw new InvalidOperationException("Could not find repository root (.git directory).");
}

var repoRoot   = FindRepoRoot();
var dbPath     = Path.Combine(repoRoot, "src", "CountOrSell.Api", "database", "CountOrSell.db");
var imagesRoot = Path.Combine(repoRoot, "src", "CountOrSell.Api", "images");
Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
Directory.CreateDirectory(imagesRoot);

var dbOptions = new DbContextOptionsBuilder<CountOrSellDbContext>()
    .UseSqlite($"Data Source={dbPath}")
    .Options;

using (var db = new CountOrSellDbContext(dbOptions))
{
    db.Database.EnsureCreated();
    db.EnsureSchemaUpToDate();
}

var scryfallClient = new HttpClient
{
    BaseAddress = new Uri("https://api.scryfall.com/")
};
scryfallClient.DefaultRequestHeaders.Add("User-Agent", "CountOrSell/2.0");
scryfallClient.DefaultRequestHeaders.Add("Accept", "application/json");
var scryfall = new ScryfallService(scryfallClient);

var mtgJsonClient = new HttpClient
{
    BaseAddress = new Uri("https://mtgjson.com/api/v5/")
};
mtgJsonClient.DefaultRequestHeaders.Add("User-Agent", "CountOrSell/2.0");
var mtgJson = new MtgJsonService(mtgJsonClient);

var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

// ========== ROOT COMMAND ==========
var rootCommand = new RootCommand("CountOrSell CLI - Admin tool for managing MTG card data");

// ========== SYNC COMMAND ==========
var syncCommand = new Command("sync", "Pull data from Scryfall into the database");
var syncSetsOption = new Option<bool>("--sets", "Sync sets only");
var syncCardsOption = new Option<bool>("--cards", "Sync cards only (for previously synced sets)");
var syncSetCodeOption = new Option<string?>("--set-code", "Sync a specific set code");
var syncAllOption = new Option<bool>("--all", "Sync all sets and their cards");
syncCommand.AddOption(syncSetsOption);
syncCommand.AddOption(syncCardsOption);
syncCommand.AddOption(syncSetCodeOption);
syncCommand.AddOption(syncAllOption);

syncCommand.SetHandler(async (bool sets, bool cards, string? setCode, bool all) =>
{
    using var db = new CountOrSellDbContext(dbOptions);

    // ── sync --all or default (no options) ───────────────────────────────────
    if (all || (!sets && !cards && setCode == null))
    {
        var count = 0;
        var tagged = 0;
        var totalCardsSynced = 0;

        await AnsiConsole.Progress()
            .AutoClear(false)
            .HideCompleted(false)
            .Columns(new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn(), new CounterColumn(), new ElapsedTimeColumn())
            .StartAsync(async ctx =>
            {
                var fetchTask = ctx.AddTask("Fetching sets from Scryfall", maxValue: 0);
                var scryfallSets = await scryfall.GetSetsAsync();
                fetchTask.MaxValue = 1;
                fetchTask.Increment(1);

                var upsertTask = ctx.AddTask("Syncing sets", maxValue: scryfallSets.Count);
                var now = DateTime.UtcNow;
                foreach (var s in scryfallSets)
                {
                    var existing = await db.CachedSets.FindAsync(s.Id);
                    if (existing != null)
                    {
                        existing.Code = s.Code; existing.Name = s.Name; existing.ReleasedAt = s.ReleasedAt;
                        existing.SetType = s.SetType; existing.CardCount = s.CardCount;
                        existing.IconSvgUri = s.IconSvgUri; existing.ScryfallUri = s.ScryfallUri;
                        existing.LastSyncedAt = now;
                    }
                    else
                    {
                        db.CachedSets.Add(new CachedSet
                        {
                            Id = s.Id, Code = s.Code, Name = s.Name, ReleasedAt = s.ReleasedAt,
                            SetType = s.SetType, CardCount = s.CardCount, IconSvgUri = s.IconSvgUri,
                            ScryfallUri = s.ScryfallUri, LastSyncedAt = now
                        });
                    }
                    count++;
                    upsertTask.Increment(1);
                }
                await db.SaveChangesAsync();
                tagged = await ApplyAutoTagsAsync(db, scryfallSets);

                if (all)
                {
                    var allSetCodes = await db.CachedSets.Where(s => s.CardCount > 0).Select(s => s.Code).ToListAsync();
                    var cardsTask = ctx.AddTask("Syncing cards", maxValue: allSetCodes.Count);
                    foreach (var code in allSetCodes)
                    {
                        cardsTask.Description = $"Syncing cards  \u2514 {code.ToUpperInvariant()}";
                        var cardCount = await SyncCardsForSet(db, code);
                        totalCardsSynced += cardCount;
                        cardsTask.Increment(1);
                    }
                    cardsTask.Description = "Syncing cards";
                }
            });

        AnsiConsole.WriteLine($"Synced {count} sets.");
        if (tagged > 0) AnsiConsole.WriteLine($"Auto-tagged {tagged} set(s).");
        if (all) AnsiConsole.WriteLine($"Synced {totalCardsSynced} total cards across all sets.");
        return;
    }

    // ── sync --sets ──────────────────────────────────────────────────────────
    if (sets)
    {
        var count = 0;
        var tagged = 0;

        await AnsiConsole.Progress()
            .AutoClear(false)
            .HideCompleted(false)
            .Columns(new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn(), new CounterColumn(), new ElapsedTimeColumn())
            .StartAsync(async ctx =>
            {
                var fetchTask = ctx.AddTask("Fetching sets from Scryfall", maxValue: 0);
                var scryfallSets = await scryfall.GetSetsAsync();
                fetchTask.MaxValue = 1;
                fetchTask.Increment(1);

                var upsertTask = ctx.AddTask("Syncing sets", maxValue: scryfallSets.Count);
                var now = DateTime.UtcNow;
                foreach (var s in scryfallSets)
                {
                    var existing = await db.CachedSets.FindAsync(s.Id);
                    if (existing != null)
                    {
                        existing.Code = s.Code; existing.Name = s.Name; existing.ReleasedAt = s.ReleasedAt;
                        existing.SetType = s.SetType; existing.CardCount = s.CardCount;
                        existing.IconSvgUri = s.IconSvgUri; existing.ScryfallUri = s.ScryfallUri;
                        existing.LastSyncedAt = now;
                    }
                    else
                    {
                        db.CachedSets.Add(new CachedSet
                        {
                            Id = s.Id, Code = s.Code, Name = s.Name, ReleasedAt = s.ReleasedAt,
                            SetType = s.SetType, CardCount = s.CardCount, IconSvgUri = s.IconSvgUri,
                            ScryfallUri = s.ScryfallUri, LastSyncedAt = now
                        });
                    }
                    count++;
                    upsertTask.Increment(1);
                }
                await db.SaveChangesAsync();
                tagged = await ApplyAutoTagsAsync(db, scryfallSets);
            });

        AnsiConsole.WriteLine($"Synced {count} sets.");
        if (tagged > 0) AnsiConsole.WriteLine($"Auto-tagged {tagged} set(s).");
    }

    // ── sync --cards (re-sync all sets that already have cards) ──────────────
    if (cards && setCode == null)
    {
        var syncedSetCodes = await db.CachedCards.Select(c => c.SetCode).Distinct().ToListAsync();
        var totalSynced = 0;

        await AnsiConsole.Progress()
            .AutoClear(false)
            .HideCompleted(false)
            .Columns(new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn(), new CounterColumn(), new ElapsedTimeColumn())
            .StartAsync(async ctx =>
            {
                var cardsTask = ctx.AddTask("Syncing cards", maxValue: syncedSetCodes.Count);
                foreach (var code in syncedSetCodes)
                {
                    cardsTask.Description = $"Syncing cards  \u2514 {code.ToUpperInvariant()}";
                    var cardCount = await SyncCardsForSet(db, code);
                    totalSynced += cardCount;
                    cardsTask.Increment(1);
                }
                cardsTask.Description = "Syncing cards";
            });

        AnsiConsole.WriteLine($"Synced {totalSynced} total cards.");
    }

    // ── sync --set-code X ────────────────────────────────────────────────────
    if (setCode != null)
    {
        int cardCount = 0;
        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync($"Syncing {setCode.ToUpperInvariant()}...", async _ =>
            {
                cardCount = await SyncCardsForSet(db, setCode);
            });
        AnsiConsole.WriteLine($"Synced {cardCount} cards for {setCode.ToUpperInvariant()}.");
    }
}, syncSetsOption, syncCardsOption, syncSetCodeOption, syncAllOption);

rootCommand.AddCommand(syncCommand);

// ========== IMAGES COMMAND ==========
var imagesCommand = new Command("images", "Download card images");
var imgSetCodeOption = new Option<string?>("--set-code", "Download images for a specific set");
var imgAllOption = new Option<bool>("--all", "Download images for all cached cards");
var imgMissingOption = new Option<bool>("--missing-only", () => true, "Only download missing images");
imagesCommand.AddOption(imgSetCodeOption);
imagesCommand.AddOption(imgAllOption);
imagesCommand.AddOption(imgMissingOption);

imagesCommand.SetHandler(async (string? setCode, bool all, bool missingOnly) =>
{
    using var db = new CountOrSellDbContext(dbOptions);

    // Pre-scan: check disk for images that exist but aren't tracked in DB
    var healed = 0;
    if (missingOnly)
    {
        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("Scanning disk for existing images...", async _ =>
            {
                var setCodeNorm = setCode?.ToLowerInvariant();
                var untracked = await db.CachedCards
                    .Where(c => (c.LocalImagePath == null || c.LocalImagePath == "") &&
                                (setCodeNorm == null || c.SetCode == setCodeNorm))
                    .ToListAsync();

                foreach (var card in untracked)
                {
                    var rel = Path.Combine(card.SetCode, $"{card.Id}.jpg");
                    if (File.Exists(Path.Combine(imagesRoot, rel)))
                    {
                        card.LocalImagePath = rel;
                        healed++;
                    }
                }
                if (healed > 0)
                    await db.SaveChangesAsync();
            });

        if (healed > 0)
            AnsiConsole.WriteLine($"Found {healed} image(s) already on disk — updated database.");
    }

    IQueryable<CachedCard> query = db.CachedCards;
    if (setCode != null) query = query.Where(c => c.SetCode == setCode.ToLowerInvariant());
    if (missingOnly) query = query.Where(c => c.LocalImagePath == null || c.LocalImagePath == "");

    var cards = await query.OrderBy(c => c.SetCode).ThenBy(c => c.CollectorNumber).ToListAsync();

    if (cards.Count == 0)
    {
        AnsiConsole.WriteLine("No images to download.");
        return;
    }

    var downloaded = 0;
    var noUrl = 0;
    var failed = 0;

    await AnsiConsole.Progress()
        .AutoClear(false)
        .HideCompleted(false)
        .Columns(new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn(), new CounterColumn(), new ElapsedTimeColumn())
        .StartAsync(async ctx =>
        {
            var totalTask = ctx.AddTask("Downloading images", maxValue: cards.Count);
            var bySet = cards.GroupBy(c => c.SetCode).OrderBy(g => g.Key).ToList();

            foreach (var setGroup in bySet)
            {
                var setCards = setGroup.ToList();
                var setUpper = setGroup.Key.ToUpperInvariant();
                var setDownloaded = 0;

                totalTask.Description = $"Downloading images  \u2514 {setUpper}: {setDownloaded}/{setCards.Count}";

                foreach (var card in setCards)
                {
                    var imageUrl = GetImageUrl(card);
                    if (imageUrl == null)
                    {
                        noUrl++;
                    }
                    else
                    {
                        var setDir = Path.Combine(imagesRoot, card.SetCode);
                        Directory.CreateDirectory(setDir);
                        var relativePath = Path.Combine(card.SetCode, $"{card.Id}.jpg");
                        var absolutePath = Path.Combine(imagesRoot, relativePath);

                        if (missingOnly && File.Exists(absolutePath))
                        {
                            card.LocalImagePath = relativePath;
                        }
                        else
                        {
                            var bytes = await scryfall.DownloadImageAsync(imageUrl);
                            if (bytes != null)
                            {
                                await File.WriteAllBytesAsync(absolutePath, bytes);
                                card.LocalImagePath = relativePath;
                                downloaded++;
                                setDownloaded++;
                                totalTask.Description = $"Downloading images  \u2514 {setUpper}: {setDownloaded}/{setCards.Count}";
                            }
                            else
                            {
                                failed++;
                            }
                            await Task.Delay(75); // Rate limit
                        }
                    }
                    totalTask.Increment(1);
                }
            }

            await db.SaveChangesAsync();
        });

    var parts = new List<string> { $"Downloaded {downloaded} image{(downloaded == 1 ? "" : "s")}." };
    if (noUrl > 0) parts.Add($"{noUrl} skipped (no image URL in Scryfall data).");
    if (failed > 0) parts.Add($"{failed} failed (download error).");
    AnsiConsole.WriteLine(string.Join("  ", parts));
}, imgSetCodeOption, imgAllOption, imgMissingOption);

rootCommand.AddCommand(imagesCommand);

// ========== PUBLISH COMMAND ==========
var publishCommand = new Command("publish", "Build full/delta update packages and dbupdate.json manifest");
var pubOutputDirOption = new Option<string>("--output-dir", "Output directory for packages and manifest") { IsRequired = true };
var pubVersionOption = new Option<string?>("--version", "Version string (default: yyyy.MM.dd.HHmm)");
var pubDeltaFromOption = new Option<string?>("--delta-from", "Previous version to generate delta from");
var pubBaseDbOption = new Option<string?>("--base-db", "Path to the previous version's database (required with --delta-from)");
var pubPushOption = new Option<bool>("--push", "Upload packages to Azure Blob Storage and update the website repo");
publishCommand.AddOption(pubOutputDirOption);
publishCommand.AddOption(pubVersionOption);
publishCommand.AddOption(pubDeltaFromOption);
publishCommand.AddOption(pubBaseDbOption);
publishCommand.AddOption(pubPushOption);

publishCommand.SetHandler(async (string outputDir, string? version, string? deltaFrom, string? baseDbPath, bool push) =>
{
    using var db = new CountOrSellDbContext(dbOptions);
    var ver = version ?? DateTime.UtcNow.ToString("yyyy.MM.dd.HHmm");

    Directory.CreateDirectory(outputDir);
    var packagesDir = Path.Combine(outputDir, "packages");
    Directory.CreateDirectory(packagesDir);

    var setCount = await db.CachedSets.CountAsync();
    var cardCount = await db.CachedCards.CountAsync();
    var manifestPackages = new List<object>();

    // --- Always generate a full package ---
    Console.WriteLine($"Building full update package v{ver}...");
    var fullZipName = $"full-{ver}.zip";
    var fullZipPath = Path.Combine(packagesDir, fullZipName);
    var fullTempDir = Path.Combine(Path.GetTempPath(), $"CountOrSell-full-{Guid.NewGuid():N}");
    Directory.CreateDirectory(fullTempDir);

    try
    {
        // Create a clean data.db with only CachedSets and CachedCards
        var fullDataDbPath = Path.Combine(fullTempDir, "data.db");
        await CreateDataOnlyDb(db, fullDataDbPath);
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        // Write inner manifest
        var innerManifest = new { version = ver, type = "full", setCount, cardCount, createdAt = DateTime.UtcNow };
        await File.WriteAllTextAsync(
            Path.Combine(fullTempDir, "manifest.json"),
            JsonSerializer.Serialize(innerManifest, new JsonSerializerOptions { WriteIndented = true }));

        if (File.Exists(fullZipPath)) File.Delete(fullZipPath);
        ZipFile.CreateFromDirectory(fullTempDir, fullZipPath);

        var fullFileInfo = new FileInfo(fullZipPath);
        string fullChecksum;
        using (var fs = new FileStream(fullZipPath, FileMode.Open, FileAccess.Read))
            fullChecksum = Convert.ToHexString(await SHA256.HashDataAsync(fs));

        manifestPackages.Add(new
        {
            version = ver,
            type = "full",
            description = $"Full database: {setCount} sets, {cardCount} cards",
            downloadUrl = $"https://www.countorsell.com/dbupdate/packages/{fullZipName}",
            fileSizeBytes = fullFileInfo.Length,
            checksum = fullChecksum
        });

        Console.WriteLine($"Full package created: {fullZipPath} ({fullFileInfo.Length / 1024}KB)");
    }
    finally
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        Directory.Delete(fullTempDir, true);
    }

    // --- Optionally generate a delta package ---
    if (deltaFrom != null)
    {
        if (baseDbPath == null || !File.Exists(baseDbPath))
        {
            Console.WriteLine("Error: --base-db is required with --delta-from and must point to an existing file.");
            return;
        }

        Console.WriteLine($"Building delta package from v{deltaFrom} to v{ver}...");
        var deltaZipName = $"delta-{deltaFrom}-to-{ver}.zip";
        var deltaZipPath = Path.Combine(packagesDir, deltaZipName);
        var deltaTempDir = Path.Combine(Path.GetTempPath(), $"CountOrSell-delta-{Guid.NewGuid():N}");
        Directory.CreateDirectory(deltaTempDir);

        try
        {
            // Open old database to find what's changed
            var oldDbOptions = new DbContextOptionsBuilder<CountOrSellDbContext>()
                .UseSqlite($"Data Source={baseDbPath}")
                .Options;
            using var oldDb = new CountOrSellDbContext(oldDbOptions);

            // Get max LastSyncedAt from old DB
            var oldMaxSetSync = await oldDb.CachedSets.AnyAsync()
                ? await oldDb.CachedSets.MaxAsync(s => s.LastSyncedAt)
                : DateTime.MinValue;
            var oldMaxCardSync = await oldDb.CachedCards.AnyAsync()
                ? await oldDb.CachedCards.MaxAsync(c => c.LastSyncedAt)
                : DateTime.MinValue;

            // Get old IDs for detecting new rows
            var oldSetIds = (await oldDb.CachedSets.Select(s => s.Id).ToListAsync()).ToHashSet();
            var oldCardIds = (await oldDb.CachedCards.Select(c => c.Id).ToListAsync()).ToHashSet();

            // Find changed/new sets (filter in memory since we need HashSet lookup)
            var allSets = await db.CachedSets.AsNoTracking().ToListAsync();
            var deltaSets = allSets
                .Where(s => s.LastSyncedAt > oldMaxSetSync || !oldSetIds.Contains(s.Id))
                .ToList();

            // Find changed/new cards (filter in memory)
            var allCards = await db.CachedCards.AsNoTracking().ToListAsync();
            var deltaCards = allCards
                .Where(c => c.LastSyncedAt > oldMaxCardSync || !oldCardIds.Contains(c.Id))
                .ToList();

            // Create delta data.db
            var deltaDataDbPath = Path.Combine(deltaTempDir, "data.db");
            var deltaSetCodes = deltaSets.Select(s => s.Code).ToHashSet();
            var deltaTags = await db.SetTags.AsNoTracking()
                .Where(t => deltaSetCodes.Contains(t.SetCode))
                .ToListAsync();
            await CreateDeltaDb(deltaSets, deltaCards, deltaTags, deltaDataDbPath);
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

            // Write inner manifest
            var deltaInnerManifest = new
            {
                version = ver, type = "delta", fromVersion = deltaFrom,
                setCount = deltaSets.Count, cardCount = deltaCards.Count, createdAt = DateTime.UtcNow
            };
            await File.WriteAllTextAsync(
                Path.Combine(deltaTempDir, "manifest.json"),
                JsonSerializer.Serialize(deltaInnerManifest, new JsonSerializerOptions { WriteIndented = true }));

            if (File.Exists(deltaZipPath)) File.Delete(deltaZipPath);
            ZipFile.CreateFromDirectory(deltaTempDir, deltaZipPath);

            var deltaFileInfo = new FileInfo(deltaZipPath);
            string deltaChecksum;
            using (var fs = new FileStream(deltaZipPath, FileMode.Open, FileAccess.Read))
                deltaChecksum = Convert.ToHexString(await SHA256.HashDataAsync(fs));

            manifestPackages.Add(new
            {
                version = ver,
                fromVersion = deltaFrom,
                type = "delta",
                description = $"Delta from {deltaFrom}: {deltaSets.Count} new/updated sets, {deltaCards.Count} new/updated cards",
                downloadUrl = $"https://www.countorsell.com/dbupdate/packages/{deltaZipName}",
                fileSizeBytes = deltaFileInfo.Length,
                checksum = deltaChecksum
            });

            Console.WriteLine($"Delta package created: {deltaZipPath} ({deltaFileInfo.Length / 1024}KB)");
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            Directory.Delete(deltaTempDir, true);
        }
    }

    // --- Generate dbupdate.json manifest ---
    var dbupdateManifest = new { currentVersion = ver, packages = manifestPackages };
    var dbupdateJson = JsonSerializer.Serialize(dbupdateManifest, new JsonSerializerOptions { WriteIndented = true });
    var dbupdatePath = Path.Combine(outputDir, "dbupdate.json");
    await File.WriteAllTextAsync(dbupdatePath, dbupdateJson);
    Console.WriteLine($"Manifest written: {dbupdatePath}");

    // Record in DB
    db.DatabaseUpdatePackages.Add(new DatabaseUpdatePackage
    {
        Version = ver,
        Description = $"Published: {setCount} sets, {cardCount} cards",
        FilePath = Path.Combine(packagesDir, $"full-{ver}.zip"),
        FileSizeBytes = new FileInfo(Path.Combine(packagesDir, $"full-{ver}.zip")).Length,
        Checksum = "",
        IsApplied = true,
        AppliedAt = DateTime.UtcNow
    });
    await db.SaveChangesAsync();

    Console.WriteLine("Publish complete!");

    // ── Push to Azure + update website repo ──────────────────
    if (push)
    {
        var storageAccount = Environment.GetEnvironmentVariable("COS_AZURE_STORAGE_ACCOUNT");
        var storageKey     = Environment.GetEnvironmentVariable("COS_AZURE_STORAGE_KEY");
        var websiteRepo    = Environment.GetEnvironmentVariable("COS_WEBSITE_REPO_PATH");

        if (string.IsNullOrEmpty(storageAccount) || string.IsNullOrEmpty(storageKey))
        {
            Console.Error.WriteLine("Error: COS_AZURE_STORAGE_ACCOUNT and COS_AZURE_STORAGE_KEY must be set for --push.");
            return;
        }
        if (string.IsNullOrEmpty(websiteRepo) || !Directory.Exists(websiteRepo))
        {
            Console.Error.WriteLine($"Error: COS_WEBSITE_REPO_PATH must point to an existing directory. Got: '{websiteRepo}'");
            return;
        }

        // Upload ZIPs to Azure Blob Storage ($web container, packages/ prefix)
        var blobServiceClient = new BlobServiceClient(
            new Uri($"https://{storageAccount}.blob.core.windows.net"),
            new Azure.Storage.StorageSharedKeyCredential(storageAccount, storageKey));
        var containerClient = blobServiceClient.GetBlobContainerClient("$web");

        var zipFiles = Directory.GetFiles(packagesDir, "*.zip");
        Console.WriteLine($"Uploading {zipFiles.Length} package(s) to Azure Blob Storage...");
        foreach (var zipFile in zipFiles)
        {
            var blobName = $"packages/{Path.GetFileName(zipFile)}";
            var blobClient = containerClient.GetBlobClient(blobName);
            using var stream = File.OpenRead(zipFile);
            await blobClient.UploadAsync(stream, new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = "application/zip" },
                TransferOptions = new Azure.Storage.StorageTransferOptions { MaximumConcurrency = 4 }
            });
            Console.WriteLine($"  Uploaded: {blobName}");
        }

        // Update dbupdate manifest in the website repo
        var websiteDbupdatePath = Path.Combine(websiteRepo, "dbupdate");
        var websitePackagesDir  = Path.Combine(websiteRepo, "packages");
        Directory.CreateDirectory(websitePackagesDir);

        // Read existing manifest (if present) and upsert new packages
        List<object> existingPackages = new();
        if (File.Exists(websiteDbupdatePath))
        {
            try
            {
                var existing = JsonSerializer.Deserialize<JsonElement>(await File.ReadAllTextAsync(websiteDbupdatePath));
                if (existing.TryGetProperty("packages", out var pkgsEl))
                {
                    foreach (var pkg in pkgsEl.EnumerateArray())
                        existingPackages.Add(pkg);
                }
            }
            catch { /* ignore malformed existing manifest */ }
        }
        // Append new packages (avoid duplicates by version+type)
        var newVersions = manifestPackages
            .Cast<dynamic>()
            .Select(p => $"{p.version}:{p.type}")
            .ToHashSet();
        var mergedPackages = existingPackages
            .Where(p => {
                if (p is JsonElement je)
                {
                    var key = $"{(je.TryGetProperty("version", out var v) ? v.GetString() : "")}:{(je.TryGetProperty("type", out var t) ? t.GetString() : "")}";
                    return !newVersions.Contains(key);
                }
                return true;
            })
            .Concat(manifestPackages)
            .ToList();

        var websiteManifest = new { currentVersion = ver, packages = mergedPackages };
        await File.WriteAllTextAsync(websiteDbupdatePath,
            JsonSerializer.Serialize(websiteManifest, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Updated website dbupdate manifest: {websiteDbupdatePath}");

        // Copy ZIPs to website repo packages/ directory
        foreach (var zipFile in zipFiles)
        {
            var destPath = Path.Combine(websitePackagesDir, Path.GetFileName(zipFile));
            File.Copy(zipFile, destPath, overwrite: true);
            Console.WriteLine($"  Copied to website repo: {Path.GetFileName(zipFile)}");
        }

        // Git commit and push the website repo
        Console.WriteLine("Committing and pushing website repo...");
        static (int exitCode, string output) RunGit(string workingDir, string arguments)
        {
            var psi = new ProcessStartInfo("git", arguments)
            {
                WorkingDirectory = workingDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            using var proc = Process.Start(psi)!;
            var stdout = proc.StandardOutput.ReadToEnd();
            var stderr = proc.StandardError.ReadToEnd();
            proc.WaitForExit();
            return (proc.ExitCode, stdout + stderr);
        }

        var (addCode, addOut) = RunGit(websiteRepo, "add dbupdate packages/");
        if (addCode != 0) { Console.Error.WriteLine($"git add failed: {addOut}"); return; }

        var (commitCode, commitOut) = RunGit(websiteRepo, $"commit -m \"Update manifest to {ver}\"");
        if (commitCode != 0 && !commitOut.Contains("nothing to commit"))
        {
            Console.Error.WriteLine($"git commit failed: {commitOut}");
            return;
        }

        var (pushCode, pushOut) = RunGit(websiteRepo, "push");
        if (pushCode != 0) { Console.Error.WriteLine($"git push failed: {pushOut}"); return; }

        Console.WriteLine("Website repo pushed — GitHub Actions will deploy automatically.");
    }
}, pubOutputDirOption, pubVersionOption, pubDeltaFromOption, pubBaseDbOption, pubPushOption);

rootCommand.AddCommand(publishCommand);

// ========== REVIEW COMMAND ==========
var reviewCommand = new Command("review", "Review user submissions");
var reviewListOption = new Option<bool>("--list", "List pending submissions");
var reviewApproveOption = new Option<int?>("--approve", "Approve a submission by ID");
var reviewRejectOption = new Option<int?>("--reject", "Reject a submission by ID");
var reviewShowOption = new Option<int?>("--show", "Show details of a submission");
var reviewApproveAllOption = new Option<bool>("--approve-all", "Approve all pending submissions");
reviewCommand.AddOption(reviewListOption);
reviewCommand.AddOption(reviewApproveOption);
reviewCommand.AddOption(reviewRejectOption);
reviewCommand.AddOption(reviewShowOption);
reviewCommand.AddOption(reviewApproveAllOption);

reviewCommand.SetHandler(async (bool list, int? approve, int? reject, int? show, bool approveAll) =>
{
    using var db = new CountOrSellDbContext(dbOptions);

    if (list || (!approve.HasValue && !reject.HasValue && !show.HasValue && !approveAll))
    {
        var submissions = await db.UserSubmissions
            .Include(s => s.Items)
            .Where(s => s.Status == "Pending")
            .OrderBy(s => s.SubmittedAt)
            .ToListAsync();

        Console.WriteLine($"Pending submissions: {submissions.Count}");
        foreach (var s in submissions)
        {
            Console.WriteLine($"  #{s.Id} - {s.Username} - {s.SubmittedAt:g} - {s.Items.Count} items");
        }
        return;
    }

    if (show.HasValue)
    {
        var s = await db.UserSubmissions.Include(s => s.Items).FirstOrDefaultAsync(s => s.Id == show.Value);
        if (s == null) { Console.WriteLine("Submission not found."); return; }

        Console.WriteLine($"Submission #{s.Id}");
        Console.WriteLine($"  User: {s.Username}");
        Console.WriteLine($"  Submitted: {s.SubmittedAt:g}");
        Console.WriteLine($"  Status: {s.Status}");
        Console.WriteLine($"  Items ({s.Items.Count}):");
        foreach (var item in s.Items)
        {
            Console.WriteLine($"    - {item.ChangeType} {item.EntityType} {item.EntityId ?? "(new)"} [{item.Status}]");
        }
        return;
    }

    if (approve.HasValue)
    {
        var s = await db.UserSubmissions.Include(s => s.Items).FirstOrDefaultAsync(s => s.Id == approve.Value);
        if (s == null) { Console.WriteLine("Submission not found."); return; }
        s.Status = "Approved"; s.ReviewedAt = DateTime.UtcNow;
        foreach (var item in s.Items) item.Status = "Approved";
        await db.SaveChangesAsync();
        Console.WriteLine($"Submission #{s.Id} approved.");
    }

    if (reject.HasValue)
    {
        var s = await db.UserSubmissions.Include(s => s.Items).FirstOrDefaultAsync(s => s.Id == reject.Value);
        if (s == null) { Console.WriteLine("Submission not found."); return; }
        s.Status = "Rejected"; s.ReviewedAt = DateTime.UtcNow;
        foreach (var item in s.Items) item.Status = "Rejected";
        await db.SaveChangesAsync();
        Console.WriteLine($"Submission #{s.Id} rejected.");
    }

    if (approveAll)
    {
        var pending = await db.UserSubmissions.Include(s => s.Items).Where(s => s.Status == "Pending").ToListAsync();
        foreach (var s in pending)
        {
            s.Status = "Approved"; s.ReviewedAt = DateTime.UtcNow;
            foreach (var item in s.Items) item.Status = "Approved";
        }
        await db.SaveChangesAsync();
        Console.WriteLine($"Approved {pending.Count} submissions.");
    }
}, reviewListOption, reviewApproveOption, reviewRejectOption, reviewShowOption, reviewApproveAllOption);

rootCommand.AddCommand(reviewCommand);

// ========== MIGRATE-V1 COMMAND ==========
var migrateCommand = new Command("migrate-v1", "Migrate data from v1 database");
var migrateSourceOption = new Option<string>("--source", "Path to v1 CountOrSell.db file") { IsRequired = true };
migrateCommand.AddOption(migrateSourceOption);

migrateCommand.SetHandler(async (string source) =>
{
    if (!File.Exists(source))
    {
        Console.WriteLine($"Source file not found: {source}");
        return;
    }

    using var db = new CountOrSellDbContext(dbOptions);

    // Create a default user for migrated data
    var defaultUser = new User
    {
        Id = Guid.NewGuid().ToString(),
        Username = "admin",
        PasswordHash = BCrypt.Net.BCrypt.HashPassword("changeme"),
        DisplayName = "Admin (Migrated)",
        CreatedAt = DateTime.UtcNow
    };

    if (!await db.Users.AnyAsync(u => u.Username == "admin"))
    {
        db.Users.Add(defaultUser);
        await db.SaveChangesAsync();
        Console.WriteLine($"Created default user 'admin' (password: changeme)");
    }
    else
    {
        defaultUser = await db.Users.FirstAsync(u => u.Username == "admin");
        Console.WriteLine("Using existing 'admin' user.");
    }

    // Connect to v1 database
    var v1Options = new DbContextOptionsBuilder<CountOrSellDbContext>()
        .UseSqlite($"Data Source={source}")
        .Options;

    // Use raw SQLite to read v1 data (different schema - no UserId columns)
    using var v1Connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={source}");
    await v1Connection.OpenAsync();

    // Migrate CachedSets
    using (var cmd = v1Connection.CreateCommand())
    {
        cmd.CommandText = "SELECT Id, Code, Name, ReleasedAt, SetType, CardCount, IconSvgUri, ScryfallUri, LastSyncedAt FROM CachedSets";
        using var reader = await cmd.ExecuteReaderAsync();
        var count = 0;
        while (await reader.ReadAsync())
        {
            var id = reader.GetString(0);
            if (await db.CachedSets.AnyAsync(s => s.Id == id)) continue;
            db.CachedSets.Add(new CachedSet
            {
                Id = id,
                Code = reader.GetString(1),
                Name = reader.GetString(2),
                ReleasedAt = reader.IsDBNull(3) ? null : reader.GetString(3),
                SetType = reader.GetString(4),
                CardCount = reader.GetInt32(5),
                IconSvgUri = reader.IsDBNull(6) ? null : reader.GetString(6),
                ScryfallUri = reader.IsDBNull(7) ? null : reader.GetString(7),
                LastSyncedAt = reader.GetDateTime(8)
            });
            count++;
        }
        await db.SaveChangesAsync();
        Console.WriteLine($"Migrated {count} sets.");
    }

    // Migrate CachedCards
    using (var cmd = v1Connection.CreateCommand())
    {
        cmd.CommandText = "SELECT Id, Name, SetCode, SetName, CollectorNumber, Rarity, TypeLine, ManaCost, OracleText, ColorIdentity, ImageUrisJson, CardFacesJson, PriceUsd, PriceUsdFoil, ScryfallUri, IsReserved, LocalImagePath, LastSyncedAt FROM CachedCards";
        using var reader = await cmd.ExecuteReaderAsync();
        var count = 0;
        while (await reader.ReadAsync())
        {
            var id = reader.GetString(0);
            if (await db.CachedCards.AnyAsync(c => c.Id == id)) continue;
            db.CachedCards.Add(new CachedCard
            {
                Id = id, Name = reader.GetString(1), SetCode = reader.GetString(2),
                SetName = reader.GetString(3), CollectorNumber = reader.GetString(4),
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
                LastSyncedAt = reader.GetDateTime(17)
            });
            count++;
            if (count % 1000 == 0)
            {
                await db.SaveChangesAsync();
                Console.WriteLine($"  Migrated {count} cards...");
            }
        }
        await db.SaveChangesAsync();
        Console.WriteLine($"Migrated {count} cards.");
    }

    // Migrate BoosterDefinitions (add UserId)
    using (var cmd = v1Connection.CreateCommand())
    {
        cmd.CommandText = "SELECT SetCode, BoosterType, ArtVariant, ImageUrl, Owned FROM BoosterDefinitions";
        using var reader = await cmd.ExecuteReaderAsync();
        var count = 0;
        while (await reader.ReadAsync())
        {
            db.BoosterDefinitions.Add(new BoosterDefinition
            {
                UserId = defaultUser.Id,
                SetCode = reader.GetString(0),
                BoosterType = reader.GetString(1),
                ArtVariant = reader.GetString(2),
                ImageUrl = reader.IsDBNull(3) ? null : reader.GetString(3),
                Owned = reader.GetBoolean(4)
            });
            count++;
        }
        await db.SaveChangesAsync();
        Console.WriteLine($"Migrated {count} boosters.");
    }

    // Migrate CardOwnerships (add UserId)
    using (var cmd = v1Connection.CreateCommand())
    {
        cmd.CommandText = "SELECT ScryfallCardId, CardName, SetCode, CollectorNumber, Owned FROM CardOwnerships";
        using var reader = await cmd.ExecuteReaderAsync();
        var count = 0;
        while (await reader.ReadAsync())
        {
            db.CardOwnerships.Add(new CardOwnership
            {
                UserId = defaultUser.Id,
                ScryfallCardId = reader.GetString(0),
                CardName = reader.GetString(1),
                SetCode = reader.GetString(2),
                CollectorNumber = reader.GetString(3),
                Owned = reader.GetBoolean(4)
            });
            count++;
        }
        await db.SaveChangesAsync();
        Console.WriteLine($"Migrated {count} card ownerships.");
    }

    // Migrate ReserveListCardOwnerships (add UserId)
    using (var cmd = v1Connection.CreateCommand())
    {
        cmd.CommandText = "SELECT ScryfallCardId, CardName, SetCode, Owned FROM ReserveListCardOwnerships";
        using var reader = await cmd.ExecuteReaderAsync();
        var count = 0;
        while (await reader.ReadAsync())
        {
            db.ReserveListCardOwnerships.Add(new ReserveListCardOwnership
            {
                UserId = defaultUser.Id,
                ScryfallCardId = reader.GetString(0),
                CardName = reader.GetString(1),
                SetCode = reader.GetString(2),
                Owned = reader.GetBoolean(3)
            });
            count++;
        }
        await db.SaveChangesAsync();
        Console.WriteLine($"Migrated {count} reserve list ownerships.");
    }

    Console.WriteLine("Migration complete!");
}, migrateSourceOption);

rootCommand.AddCommand(migrateCommand);

// ========== RUN ==========
return await rootCommand.InvokeAsync(args);

// ========== HELPER FUNCTIONS ==========

/// <summary>
/// For each synced set that has a set_type with a known tag mapping, insert the corresponding
/// SetTag row if it doesn't already exist. Manually applied tags are never removed.
/// </summary>
async Task<int> ApplyAutoTagsAsync(CountOrSellDbContext db, List<MtgSet> sets)
{
    var existingTags = (await db.SetTags.ToListAsync())
        .Select(t => (t.SetCode, t.Tag))
        .ToHashSet();

    var added = 0;
    foreach (var s in sets)
    {
        var autoTag = KnownSetTags.FromSetType(s.SetType);
        if (autoTag == null) continue;
        if (existingTags.Contains((s.Code, autoTag))) continue;

        db.SetTags.Add(new SetTag { SetCode = s.Code, Tag = autoTag });
        existingTags.Add((s.Code, autoTag));
        added++;
    }

    if (added > 0)
        await db.SaveChangesAsync();

    return added;
}

async Task<int> SyncCardsForSet(CountOrSellDbContext db, string setCode)
{
    var cards = await scryfall.GetCardsAsync(setCode.ToLowerInvariant());
    var now = DateTime.UtcNow;
    var count = 0;

    foreach (var card in cards)
    {
        var existing = await db.CachedCards.FindAsync(card.Id);
        var imageUrisJson = card.ImageUris != null ? JsonSerializer.Serialize(card.ImageUris) : null;
        var cardFacesJson = card.CardFaces != null ? JsonSerializer.Serialize(card.CardFaces) : null;
        var colorIdentityJson = card.ColorIdentity != null ? JsonSerializer.Serialize(card.ColorIdentity) : null;

        if (existing != null)
        {
            existing.Name = card.Name; existing.SetCode = card.Set; existing.SetName = card.SetName;
            existing.CollectorNumber = card.CollectorNumber; existing.Rarity = card.Rarity;
            existing.TypeLine = card.TypeLine; existing.ManaCost = card.ManaCost;
            existing.OracleText = card.OracleText; existing.ColorIdentity = colorIdentityJson;
            existing.ImageUrisJson = imageUrisJson; existing.CardFacesJson = cardFacesJson;
            existing.PriceUsd = card.Prices?.Usd; existing.PriceUsdFoil = card.Prices?.UsdFoil;
            existing.ScryfallUri = card.ScryfallUri; existing.IsReserved = card.Reserved;
            existing.LastSyncedAt = now;
        }
        else
        {
            db.CachedCards.Add(new CachedCard
            {
                Id = card.Id, Name = card.Name, SetCode = card.Set, SetName = card.SetName,
                CollectorNumber = card.CollectorNumber, Rarity = card.Rarity,
                TypeLine = card.TypeLine, ManaCost = card.ManaCost, OracleText = card.OracleText,
                ColorIdentity = colorIdentityJson, ImageUrisJson = imageUrisJson,
                CardFacesJson = cardFacesJson, PriceUsd = card.Prices?.Usd,
                PriceUsdFoil = card.Prices?.UsdFoil, ScryfallUri = card.ScryfallUri,
                IsReserved = card.Reserved, LastSyncedAt = now
            });
        }
        count++;
    }

    await db.SaveChangesAsync();
    return count;
}

async Task CreateDataOnlyDb(CountOrSellDbContext sourceDb, string outputPath)
{
    using var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={outputPath}");
    await conn.OpenAsync();

    using (var cmd = conn.CreateCommand())
    {
        cmd.CommandText = """
            CREATE TABLE CachedSets (
                Id TEXT PRIMARY KEY, Code TEXT NOT NULL UNIQUE, Name TEXT NOT NULL,
                ReleasedAt TEXT, SetType TEXT NOT NULL, CardCount INTEGER NOT NULL,
                IconSvgUri TEXT, ScryfallUri TEXT, LastSyncedAt TEXT NOT NULL
            );
            CREATE TABLE CachedCards (
                Id TEXT PRIMARY KEY, Name TEXT NOT NULL, SetCode TEXT NOT NULL,
                SetName TEXT NOT NULL, CollectorNumber TEXT NOT NULL, Rarity TEXT NOT NULL,
                TypeLine TEXT, ManaCost TEXT, OracleText TEXT, ColorIdentity TEXT,
                ImageUrisJson TEXT, CardFacesJson TEXT, PriceUsd TEXT, PriceUsdFoil TEXT,
                ScryfallUri TEXT, IsReserved INTEGER NOT NULL, LocalImagePath TEXT,
                LastSyncedAt TEXT NOT NULL
            );
            CREATE TABLE SetTags (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                SetCode TEXT NOT NULL,
                Tag TEXT NOT NULL,
                UNIQUE(SetCode, Tag),
                FOREIGN KEY(SetCode) REFERENCES CachedSets(Code) ON DELETE CASCADE
            );
            """;
        await cmd.ExecuteNonQueryAsync();
    }

    // Insert sets in batches
    var sets = await sourceDb.CachedSets.AsNoTracking().ToListAsync();
    foreach (var batch in sets.Chunk(500))
    {
        using var tx = await conn.BeginTransactionAsync();
        foreach (var s in batch)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO CachedSets VALUES ($id,$code,$name,$rel,$type,$count,$icon,$uri,$sync)";
            cmd.Parameters.AddWithValue("$id", s.Id);
            cmd.Parameters.AddWithValue("$code", s.Code);
            cmd.Parameters.AddWithValue("$name", s.Name);
            cmd.Parameters.AddWithValue("$rel", (object?)s.ReleasedAt ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$type", s.SetType);
            cmd.Parameters.AddWithValue("$count", s.CardCount);
            cmd.Parameters.AddWithValue("$icon", (object?)s.IconSvgUri ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$uri", (object?)s.ScryfallUri ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$sync", s.LastSyncedAt.ToString("O"));
            await cmd.ExecuteNonQueryAsync();
        }
        await tx.CommitAsync();
    }

    // Insert cards in batches
    var cards = await sourceDb.CachedCards.AsNoTracking().ToListAsync();
    foreach (var batch in cards.Chunk(1000))
    {
        using var tx = await conn.BeginTransactionAsync();
        foreach (var c in batch)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO CachedCards VALUES ($id,$name,$setCode,$setName,$cn,$rarity,$type,$mana,$oracle,$color,$imgs,$faces,$price,$priceFoil,$uri,$reserved,$localImg,$sync)";
            cmd.Parameters.AddWithValue("$id", c.Id);
            cmd.Parameters.AddWithValue("$name", c.Name);
            cmd.Parameters.AddWithValue("$setCode", c.SetCode);
            cmd.Parameters.AddWithValue("$setName", c.SetName);
            cmd.Parameters.AddWithValue("$cn", c.CollectorNumber);
            cmd.Parameters.AddWithValue("$rarity", c.Rarity);
            cmd.Parameters.AddWithValue("$type", (object?)c.TypeLine ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$mana", (object?)c.ManaCost ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$oracle", (object?)c.OracleText ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$color", (object?)c.ColorIdentity ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$imgs", (object?)c.ImageUrisJson ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$faces", (object?)c.CardFacesJson ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$price", (object?)c.PriceUsd ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$priceFoil", (object?)c.PriceUsdFoil ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$uri", (object?)c.ScryfallUri ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$reserved", c.IsReserved ? 1 : 0);
            cmd.Parameters.AddWithValue("$localImg", (object?)c.LocalImagePath ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$sync", c.LastSyncedAt.ToString("O"));
            await cmd.ExecuteNonQueryAsync();
        }
        await tx.CommitAsync();
    }

    // Insert set tags
    var tags = await sourceDb.SetTags.AsNoTracking().ToListAsync();
    foreach (var batch in tags.Chunk(500))
    {
        using var tx = await conn.BeginTransactionAsync();
        foreach (var t in batch)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT OR IGNORE INTO SetTags (SetCode, Tag) VALUES ($code, $tag)";
            cmd.Parameters.AddWithValue("$code", t.SetCode);
            cmd.Parameters.AddWithValue("$tag", t.Tag);
            await cmd.ExecuteNonQueryAsync();
        }
        await tx.CommitAsync();
    }

    Console.WriteLine($"  Data DB created: {sets.Count} sets, {cards.Count} cards, {tags.Count} tags");
}

async Task CreateDeltaDb(List<CachedSet> deltaSets, List<CachedCard> deltaCards, List<SetTag> deltaTags, string outputPath)
{
    using var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={outputPath}");
    await conn.OpenAsync();

    using (var cmd = conn.CreateCommand())
    {
        cmd.CommandText = """
            CREATE TABLE CachedSets (
                Id TEXT PRIMARY KEY, Code TEXT NOT NULL UNIQUE, Name TEXT NOT NULL,
                ReleasedAt TEXT, SetType TEXT NOT NULL, CardCount INTEGER NOT NULL,
                IconSvgUri TEXT, ScryfallUri TEXT, LastSyncedAt TEXT NOT NULL
            );
            CREATE TABLE CachedCards (
                Id TEXT PRIMARY KEY, Name TEXT NOT NULL, SetCode TEXT NOT NULL,
                SetName TEXT NOT NULL, CollectorNumber TEXT NOT NULL, Rarity TEXT NOT NULL,
                TypeLine TEXT, ManaCost TEXT, OracleText TEXT, ColorIdentity TEXT,
                ImageUrisJson TEXT, CardFacesJson TEXT, PriceUsd TEXT, PriceUsdFoil TEXT,
                ScryfallUri TEXT, IsReserved INTEGER NOT NULL, LocalImagePath TEXT,
                LastSyncedAt TEXT NOT NULL
            );
            CREATE TABLE SetTags (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                SetCode TEXT NOT NULL,
                Tag TEXT NOT NULL,
                UNIQUE(SetCode, Tag),
                FOREIGN KEY(SetCode) REFERENCES CachedSets(Code) ON DELETE CASCADE
            );
            """;
        await cmd.ExecuteNonQueryAsync();
    }

    foreach (var batch in deltaSets.Chunk(500))
    {
        using var tx = await conn.BeginTransactionAsync();
        foreach (var s in batch)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO CachedSets VALUES ($id,$code,$name,$rel,$type,$count,$icon,$uri,$sync)";
            cmd.Parameters.AddWithValue("$id", s.Id);
            cmd.Parameters.AddWithValue("$code", s.Code);
            cmd.Parameters.AddWithValue("$name", s.Name);
            cmd.Parameters.AddWithValue("$rel", (object?)s.ReleasedAt ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$type", s.SetType);
            cmd.Parameters.AddWithValue("$count", s.CardCount);
            cmd.Parameters.AddWithValue("$icon", (object?)s.IconSvgUri ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$uri", (object?)s.ScryfallUri ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$sync", s.LastSyncedAt.ToString("O"));
            await cmd.ExecuteNonQueryAsync();
        }
        await tx.CommitAsync();
    }

    foreach (var batch in deltaCards.Chunk(1000))
    {
        using var tx = await conn.BeginTransactionAsync();
        foreach (var c in batch)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO CachedCards VALUES ($id,$name,$setCode,$setName,$cn,$rarity,$type,$mana,$oracle,$color,$imgs,$faces,$price,$priceFoil,$uri,$reserved,$localImg,$sync)";
            cmd.Parameters.AddWithValue("$id", c.Id);
            cmd.Parameters.AddWithValue("$name", c.Name);
            cmd.Parameters.AddWithValue("$setCode", c.SetCode);
            cmd.Parameters.AddWithValue("$setName", c.SetName);
            cmd.Parameters.AddWithValue("$cn", c.CollectorNumber);
            cmd.Parameters.AddWithValue("$rarity", c.Rarity);
            cmd.Parameters.AddWithValue("$type", (object?)c.TypeLine ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$mana", (object?)c.ManaCost ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$oracle", (object?)c.OracleText ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$color", (object?)c.ColorIdentity ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$imgs", (object?)c.ImageUrisJson ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$faces", (object?)c.CardFacesJson ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$price", (object?)c.PriceUsd ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$priceFoil", (object?)c.PriceUsdFoil ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$uri", (object?)c.ScryfallUri ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$reserved", c.IsReserved ? 1 : 0);
            cmd.Parameters.AddWithValue("$localImg", (object?)c.LocalImagePath ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$sync", c.LastSyncedAt.ToString("O"));
            await cmd.ExecuteNonQueryAsync();
        }
        await tx.CommitAsync();
    }

    // Insert tags for delta sets
    foreach (var batch in deltaTags.Chunk(500))
    {
        using var tx = await conn.BeginTransactionAsync();
        foreach (var t in batch)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT OR IGNORE INTO SetTags (SetCode, Tag) VALUES ($code, $tag)";
            cmd.Parameters.AddWithValue("$code", t.SetCode);
            cmd.Parameters.AddWithValue("$tag", t.Tag);
            await cmd.ExecuteNonQueryAsync();
        }
        await tx.CommitAsync();
    }

    Console.WriteLine($"  Delta DB created: {deltaSets.Count} sets, {deltaCards.Count} cards, {deltaTags.Count} tags");
}

string? GetImageUrl(CachedCard card)
{
    if (!string.IsNullOrEmpty(card.ImageUrisJson))
    {
        var uris = JsonSerializer.Deserialize<ImageUris>(card.ImageUrisJson, jsonOptions);
        if (uris?.Normal != null) return uris.Normal;
    }
    if (!string.IsNullOrEmpty(card.CardFacesJson))
    {
        var faces = JsonSerializer.Deserialize<List<CardFace>>(card.CardFacesJson, jsonOptions);
        if (faces?[0]?.ImageUris?.Normal != null) return faces[0].ImageUris!.Normal;
    }
    return null;
}

/// <summary>
/// Progress column that displays "current/max" counts.
/// Renders blank for indeterminate (spinner) tasks.
/// </summary>
sealed class CounterColumn : ProgressColumn
{
    public override IRenderable Render(RenderOptions options, ProgressTask task, TimeSpan deltaTime)
    {
        if (task.IsIndeterminate) return new Text("        ");
        return new Text($"{(int)task.Value}/{(int)task.MaxValue}", new Style(Color.Green));
    }
}
