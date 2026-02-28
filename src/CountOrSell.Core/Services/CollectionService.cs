using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using CountOrSell.Core.Data;
using CountOrSell.Core.Entities;
using CountOrSell.Core.Models;

namespace CountOrSell.Core.Services;

public interface ICollectionService
{
    // Boosters
    Task<List<BoosterDefinition>> GetBoostersForSetAsync(string userId, string setCode);
    Task<List<BoosterDefinition>> GetAllBoostersAsync(string userId);
    Task<BoosterDefinition> UpsertBoosterAsync(string userId, string setCode, string boosterType, string artVariant, string? imageUrl);
    Task<BoosterDefinition?> SetBoosterOwnedAsync(string userId, int id, bool owned);
    Task<bool> DeleteBoosterAsync(string userId, int id);

    // Reserve List
    Task<List<ReserveListCardOwnership>> GetAllReserveListOwnershipsAsync(string userId);
    Task<ReserveListCardOwnership?> GetReserveListOwnershipAsync(string userId, string scryfallCardId);
    Task<ReserveListCardOwnership> SetReserveListOwnedAsync(string userId, string scryfallCardId, string cardName, string setCode, bool owned);
    Task<List<string>> GetReserveListCardIdsForSetAsync(string userId, string setCode);

    // Card Ownership
    Task<List<CardOwnershipEntry>> GetCardQuantitiesForSetAsync(string userId, string setCode);
    Task<CardOwnership> SetCardVariantQuantityAsync(string userId, string scryfallCardId, string variant, int quantity, string cardName, string setCode, string collectorNumber);
    Task BulkSetCardsOwnedAsync(string userId, List<(string scryfallCardId, string cardName, string setCode, string collectorNumber)> cards, bool owned);

    // Collection View
    Task<List<CollectionCardEntry>> GetAllOwnedCardsAsync(string userId, CollectionFilter? filter = null);
    Task<CollectionSummary> GetCollectionSummaryAsync(string userId);
}

public class CollectionService : ICollectionService
{
    private readonly CountOrSellDbContext _db;

    public CollectionService(CountOrSellDbContext db)
    {
        _db = db;
    }

    // ---- Boosters ----

    public async Task<List<BoosterDefinition>> GetBoostersForSetAsync(string userId, string setCode)
    {
        return await _db.BoosterDefinitions
            .Where(b => b.UserId == userId && b.SetCode == setCode.ToLowerInvariant())
            .OrderBy(b => b.BoosterType)
            .ThenBy(b => b.ArtVariant)
            .ToListAsync();
    }

    public async Task<List<BoosterDefinition>> GetAllBoostersAsync(string userId)
    {
        return await _db.BoosterDefinitions
            .Where(b => b.UserId == userId)
            .OrderBy(b => b.SetCode)
            .ThenBy(b => b.BoosterType)
            .ThenBy(b => b.ArtVariant)
            .ToListAsync();
    }

    public async Task<BoosterDefinition> UpsertBoosterAsync(string userId, string setCode, string boosterType, string artVariant, string? imageUrl)
    {
        var normalizedSetCode = setCode.ToLowerInvariant();
        var existing = await _db.BoosterDefinitions
            .FirstOrDefaultAsync(b => b.UserId == userId && b.SetCode == normalizedSetCode && b.BoosterType == boosterType && b.ArtVariant == artVariant);

        if (existing != null)
        {
            existing.ImageUrl = imageUrl;
            await _db.SaveChangesAsync();
            return existing;
        }

        var booster = new BoosterDefinition
        {
            UserId = userId,
            SetCode = normalizedSetCode,
            BoosterType = boosterType,
            ArtVariant = artVariant,
            ImageUrl = imageUrl,
            Owned = false
        };

        _db.BoosterDefinitions.Add(booster);
        await _db.SaveChangesAsync();
        return booster;
    }

    public async Task<BoosterDefinition?> SetBoosterOwnedAsync(string userId, int id, bool owned)
    {
        var booster = await _db.BoosterDefinitions.FirstOrDefaultAsync(b => b.Id == id && b.UserId == userId);
        if (booster == null) return null;

        booster.Owned = owned;
        await _db.SaveChangesAsync();
        return booster;
    }

    public async Task<bool> DeleteBoosterAsync(string userId, int id)
    {
        var booster = await _db.BoosterDefinitions.FirstOrDefaultAsync(b => b.Id == id && b.UserId == userId);
        if (booster == null) return false;

        _db.BoosterDefinitions.Remove(booster);
        await _db.SaveChangesAsync();
        return true;
    }

    // ---- Reserve List ----

    public async Task<List<ReserveListCardOwnership>> GetAllReserveListOwnershipsAsync(string userId)
    {
        return await _db.ReserveListCardOwnerships
            .Where(r => r.UserId == userId)
            .ToListAsync();
    }

    public async Task<ReserveListCardOwnership?> GetReserveListOwnershipAsync(string userId, string scryfallCardId)
    {
        return await _db.ReserveListCardOwnerships
            .FirstOrDefaultAsync(r => r.UserId == userId && r.ScryfallCardId == scryfallCardId);
    }

    public async Task<ReserveListCardOwnership> SetReserveListOwnedAsync(string userId, string scryfallCardId, string cardName, string setCode, bool owned)
    {
        var existing = await _db.ReserveListCardOwnerships
            .FirstOrDefaultAsync(r => r.UserId == userId && r.ScryfallCardId == scryfallCardId);

        if (existing != null)
        {
            existing.Owned = owned;
            await _db.SaveChangesAsync();
            return existing;
        }

        var ownership = new ReserveListCardOwnership
        {
            UserId = userId,
            ScryfallCardId = scryfallCardId,
            CardName = cardName,
            SetCode = setCode.ToLowerInvariant(),
            Owned = owned
        };

        _db.ReserveListCardOwnerships.Add(ownership);
        await _db.SaveChangesAsync();
        return ownership;
    }

    public async Task<List<string>> GetReserveListCardIdsForSetAsync(string userId, string setCode)
    {
        return await _db.ReserveListCardOwnerships
            .Where(r => r.UserId == userId && r.SetCode == setCode.ToLowerInvariant())
            .Select(r => r.ScryfallCardId)
            .ToListAsync();
    }

    // ---- Card Ownership ----

    public async Task<List<CardOwnershipEntry>> GetCardQuantitiesForSetAsync(string userId, string setCode)
    {
        return await _db.CardOwnerships
            .Where(c => c.UserId == userId && c.SetCode == setCode.ToLowerInvariant() && c.Quantity > 0)
            .Select(c => new CardOwnershipEntry { ScryfallCardId = c.ScryfallCardId, Variant = c.Variant, Quantity = c.Quantity })
            .ToListAsync();
    }

    public async Task<CardOwnership> SetCardVariantQuantityAsync(string userId, string scryfallCardId, string variant, int quantity, string cardName, string setCode, string collectorNumber)
    {
        var existing = await _db.CardOwnerships
            .FirstOrDefaultAsync(c => c.UserId == userId && c.ScryfallCardId == scryfallCardId && c.Variant == variant);

        if (existing != null)
        {
            existing.Quantity = quantity;
            existing.Owned = quantity > 0;
            if (!string.IsNullOrEmpty(cardName)) existing.CardName = cardName;
            if (!string.IsNullOrEmpty(setCode)) existing.SetCode = setCode.ToLowerInvariant();
            if (!string.IsNullOrEmpty(collectorNumber)) existing.CollectorNumber = collectorNumber;
            await _db.SaveChangesAsync();
            return existing;
        }

        var ownership = new CardOwnership
        {
            UserId = userId,
            ScryfallCardId = scryfallCardId,
            CardName = cardName,
            SetCode = setCode.ToLowerInvariant(),
            CollectorNumber = collectorNumber,
            Variant = variant,
            Quantity = quantity,
            Owned = quantity > 0
        };

        _db.CardOwnerships.Add(ownership);
        await _db.SaveChangesAsync();
        return ownership;
    }

    public async Task BulkSetCardsOwnedAsync(string userId, List<(string scryfallCardId, string cardName, string setCode, string collectorNumber)> cards, bool owned)
    {
        var cardIds = cards.Select(c => c.scryfallCardId).ToList();
        var existing = await _db.CardOwnerships
            .Where(c => c.UserId == userId && cardIds.Contains(c.ScryfallCardId) && c.Variant == "Regular")
            .ToListAsync();

        var existingMap = existing.ToDictionary(c => c.ScryfallCardId);

        foreach (var card in cards)
        {
            if (existingMap.TryGetValue(card.scryfallCardId, out var record))
            {
                record.Quantity = owned ? 1 : 0;
                record.Owned = owned;
            }
            else if (owned)
            {
                _db.CardOwnerships.Add(new CardOwnership
                {
                    UserId = userId,
                    ScryfallCardId = card.scryfallCardId,
                    CardName = card.cardName,
                    SetCode = card.setCode.ToLowerInvariant(),
                    CollectorNumber = card.collectorNumber,
                    Variant = "Regular",
                    Quantity = 1,
                    Owned = true
                });
            }
        }

        await _db.SaveChangesAsync();
    }

    // ---- Collection View ----

    public async Task<List<CollectionCardEntry>> GetAllOwnedCardsAsync(string userId, CollectionFilter? filter = null)
    {
        var raw = await (
            from co in _db.CardOwnerships
            where co.UserId == userId && co.Quantity > 0
            join cc in _db.CachedCards on co.ScryfallCardId equals cc.Id into ccGroup
            from cc in ccGroup.DefaultIfEmpty()
            orderby co.SetCode, co.CollectorNumber, co.Variant
            select new
            {
                co.ScryfallCardId,
                co.CardName,
                co.SetCode,
                co.CollectorNumber,
                co.Variant,
                co.Quantity,
                SetName = cc != null ? cc.SetName : co.SetCode,
                Rarity = cc != null ? cc.Rarity : "",
                TypeLine = cc != null ? cc.TypeLine : null,
                ColorIdentity = cc != null ? cc.ColorIdentity : null,
                PriceUsd = cc != null ? cc.PriceUsd : null,
                PriceUsdFoil = cc != null ? cc.PriceUsdFoil : null,
                IsReserved = cc != null && cc.IsReserved,
            }
        ).ToListAsync();

        var entries = raw.Select(r =>
        {
            decimal? price = null;
            if (r.Variant.Contains("Foil", StringComparison.OrdinalIgnoreCase))
            {
                if (decimal.TryParse(r.PriceUsdFoil, out var fp)) price = fp;
                else if (decimal.TryParse(r.PriceUsd, out var rp)) price = rp;
            }
            else
            {
                if (decimal.TryParse(r.PriceUsd, out var rp)) price = rp;
            }

            return new CollectionCardEntry
            {
                ScryfallCardId = r.ScryfallCardId,
                CardName = r.CardName,
                SetCode = r.SetCode,
                SetName = r.SetName,
                CollectorNumber = r.CollectorNumber,
                Variant = r.Variant,
                Rarity = r.Rarity,
                TypeLine = r.TypeLine,
                ColorIdentity = r.ColorIdentity,
                Quantity = r.Quantity,
                PriceUsd = price,
                IsReserved = r.IsReserved,
            };
        }).ToList();

        if (filter != null)
        {
            if (filter.Rarity != "all")
                entries = entries.Where(e => string.Equals(e.Rarity, filter.Rarity, StringComparison.OrdinalIgnoreCase)).ToList();

            if (filter.Variant != "all")
                entries = entries.Where(e => string.Equals(e.Variant, filter.Variant, StringComparison.OrdinalIgnoreCase)).ToList();

            if (filter.SetCode != "all")
                entries = entries.Where(e => string.Equals(e.SetCode, filter.SetCode, StringComparison.OrdinalIgnoreCase)).ToList();

            if (filter.Type != "all")
                entries = entries.Where(e => ParsePrimaryType(e.TypeLine) == filter.Type).ToList();

            if (filter.Color != "all")
            {
                entries = entries.Where(e =>
                {
                    if (e.ColorIdentity == null) return filter.Color == "colorless";
                    try
                    {
                        var colors = JsonSerializer.Deserialize<List<string>>(e.ColorIdentity) ?? new();
                        if (filter.Color == "colorless") return colors.Count == 0;
                        if (filter.Color == "multicolor") return colors.Count > 1;
                        return colors.Contains(filter.Color, StringComparer.OrdinalIgnoreCase);
                    }
                    catch { return false; }
                }).ToList();
            }
        }

        return entries;
    }

    public async Task<CollectionSummary> GetCollectionSummaryAsync(string userId)
    {
        var entries = await GetAllOwnedCardsAsync(userId, null);

        var summary = new CollectionSummary
        {
            TotalCopies = entries.Sum(e => e.Quantity),
            TotalUniqueCards = entries.Select(e => e.ScryfallCardId).Distinct().Count(),
            TotalValue = entries.Sum(e => (e.PriceUsd ?? 0) * e.Quantity),
            ByRarity = entries.GroupBy(e => e.Rarity).ToDictionary(g => g.Key, g => g.Sum(e => e.Quantity)),
            ValueByRarity = entries.GroupBy(e => e.Rarity).ToDictionary(g => g.Key, g => g.Sum(e => (e.PriceUsd ?? 0) * e.Quantity)),
            ByType = entries.GroupBy(e => ParsePrimaryType(e.TypeLine)).ToDictionary(g => g.Key, g => g.Sum(e => e.Quantity)),
            ByVariant = entries.GroupBy(e => e.Variant).ToDictionary(g => g.Key, g => g.Sum(e => e.Quantity)),
        };

        var rlOwned = await _db.ReserveListCardOwnerships
            .Where(r => r.UserId == userId && r.Owned)
            .ToListAsync();

        summary.ReserveListOwned = rlOwned.Count;
        if (rlOwned.Count > 0)
        {
            var rlIds = rlOwned.Select(r => r.ScryfallCardId).ToList();
            var rlCards = await _db.CachedCards
                .Where(c => rlIds.Contains(c.Id))
                .ToListAsync();
            summary.ReserveListValue = rlCards.Sum(c => decimal.TryParse(c.PriceUsd, out var p) ? p : 0);
        }

        var boosters = await _db.BoosterDefinitions.Where(b => b.UserId == userId).ToListAsync();
        summary.BoostersTotal = boosters.Count;
        summary.BoostersOwned = boosters.Count(b => b.Owned);

        return summary;
    }

    private static string ParsePrimaryType(string? typeLine)
    {
        if (string.IsNullOrEmpty(typeLine)) return "other";
        var lower = typeLine.ToLowerInvariant();
        if (lower.Contains("creature")) return "creature";
        if (lower.Contains("instant")) return "instant";
        if (lower.Contains("sorcery")) return "sorcery";
        if (lower.Contains("enchantment")) return "enchantment";
        if (lower.Contains("artifact")) return "artifact";
        if (lower.Contains("planeswalker")) return "planeswalker";
        if (lower.Contains("land")) return "land";
        if (lower.Contains("battle")) return "battle";
        return "other";
    }
}
