using Microsoft.EntityFrameworkCore;
using CountOrSell.Core.Data;
using CountOrSell.Core.Entities;

namespace CountOrSell.Api.Services;

/// <summary>
/// Background service that runs in demo mode.
/// Periodically wipes all user-owned data and reseeds demo content.
/// Triggered by the COS_DEMO_MODE=true environment variable.
/// </summary>
public class DemoResetService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<DemoResetService> _logger;
    private readonly TimeSpan _interval;

    public static DateTime? NextResetAt { get; private set; }

    public DemoResetService(IServiceProvider services, ILogger<DemoResetService> logger)
    {
        _services = services;
        _logger = logger;

        var minutes = int.TryParse(
            Environment.GetEnvironmentVariable("COS_DEMO_RESET_INTERVAL_MINUTES"), out var m) ? m : 15;
        _interval = TimeSpan.FromMinutes(Math.Max(1, minutes));
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _logger.LogInformation("Demo mode active — resetting every {interval}", _interval);
        ScheduleNext();

        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(_interval, ct).ContinueWith(_ => { }, CancellationToken.None);
            if (ct.IsCancellationRequested) break;

            await ResetAsync();
            ScheduleNext();
        }
    }

    private static void ScheduleNext() =>
        NextResetAt = DateTime.UtcNow.Add(
            TimeSpan.FromMinutes(
                int.TryParse(Environment.GetEnvironmentVariable("COS_DEMO_RESET_INTERVAL_MINUTES"), out var m)
                    ? Math.Max(1, m) : 15));

    private async Task ResetAsync()
    {
        _logger.LogInformation("Demo reset starting at {time}", DateTime.UtcNow);
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CountOrSellDbContext>();

        // Wipe all per-user data (keep CachedSets, CachedCards — the card catalog)
        await db.CardOwnerships.ExecuteDeleteAsync();
        await db.BoosterDefinitions.ExecuteDeleteAsync();
        await db.ReserveListCardOwnerships.ExecuteDeleteAsync();
        await db.SlabbedCards.ExecuteDeleteAsync();
        await db.UserSubmissions.ExecuteDeleteAsync();
        await db.RefreshTokens.ExecuteDeleteAsync();

        // Reset all users except cosadm back to the demo password
        var demoPassword = BCrypt.Net.BCrypt.HashPassword("demo");
        var nonAdminUsers = await db.Users.Where(u => u.Username != "cosadm").ToListAsync();
        foreach (var u in nonAdminUsers)
        {
            u.PasswordHash = demoPassword;
            u.IsDisabled = false;
        }

        // Ensure the demo user exists for visitors to explore with
        if (!await db.Users.AnyAsync(u => u.Username == "demo"))
        {
            db.Users.Add(new User
            {
                Username = "demo",
                PasswordHash = demoPassword,
                DisplayName = "Demo User",
                IsAdmin = false,
                CreatedAt = DateTime.UtcNow,
            });
        }

        await db.SaveChangesAsync();

        // Reseed demo data (a few owned cards, boosters, slabs)
        var demoUser = await db.Users.FirstOrDefaultAsync(u => u.Username == "demo");
        if (demoUser != null)
            await SeedDemoDataAsync(db, demoUser.Id);

        _logger.LogInformation("Demo reset complete");
    }

    private static async Task SeedDemoDataAsync(CountOrSellDbContext db, string userId)
    {
        // A handful of example card ownerships using well-known Scryfall IDs
        // These are placeholders — actual IDs depend on the synced card catalog
        var sampleCards = await db.CachedCards.OrderBy(c => c.Id).Take(10).ToListAsync();
        foreach (var card in sampleCards)
        {
            db.CardOwnerships.Add(new CardOwnership
            {
                UserId = userId,
                ScryfallCardId = card.Id,
                CardName = card.Name,
                SetCode = card.SetCode,
                CollectorNumber = card.CollectorNumber,
                Owned = true,
                Quantity = 1,
                Variant = "Regular",
            });
        }

        // A couple of boosters
        var sampleSet = await db.CachedSets.FirstOrDefaultAsync();
        if (sampleSet != null)
        {
            db.BoosterDefinitions.Add(new BoosterDefinition
            {
                UserId = userId,
                SetCode = sampleSet.Code,
                BoosterType = "Play Booster",
                ArtVariant = "Default",
                Owned = true,
            });
        }

        await db.SaveChangesAsync();
    }
}
