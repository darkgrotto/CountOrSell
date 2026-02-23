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
}
