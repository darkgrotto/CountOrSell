using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using CountOrSell.Core.Data;
using CountOrSell.Core.Entities;
using CountOrSell.Core.Models;

namespace CountOrSell.Core.Services;

public interface ICardDataService
{
    Task<List<MtgSet>> GetSetsAsync();
    Task<MtgSet?> GetSetAsync(string setCode);
    Task<List<MtgCard>> GetCardsForSetAsync(string setCode);
    Task<List<MtgCard>> GetReserveListCardsAsync();
    Task<string?> GetCardImagePathAsync(string cardId);
    Task<bool> AddTagAsync(string setCode, string tag);
    Task<bool> RemoveTagAsync(string setCode, string tag);
}

public class CardDataService : ICardDataService
{
    private readonly CountOrSellDbContext _db;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public CardDataService(CountOrSellDbContext db)
    {
        _db = db;
    }

    public async Task<List<MtgSet>> GetSetsAsync()
    {
        return await _db.CachedSets
            .Select(s => new MtgSet
            {
                Id = s.Id,
                Code = s.Code,
                Name = s.Name,
                ReleasedAt = s.ReleasedAt,
                SetType = s.SetType,
                CardCount = s.CardCount,
                IconSvgUri = s.IconSvgUri,
                ScryfallUri = s.ScryfallUri,
                Tags = s.Tags.Select(t => t.Tag).ToList()
            })
            .ToListAsync();
    }

    public async Task<MtgSet?> GetSetAsync(string setCode)
    {
        var code = setCode.ToLowerInvariant();
        var cached = await _db.CachedSets
            .Where(s => s.Code == code)
            .Select(s => new MtgSet
            {
                Id = s.Id,
                Code = s.Code,
                Name = s.Name,
                ReleasedAt = s.ReleasedAt,
                SetType = s.SetType,
                CardCount = s.CardCount,
                IconSvgUri = s.IconSvgUri,
                ScryfallUri = s.ScryfallUri,
                Tags = s.Tags.Select(t => t.Tag).ToList()
            })
            .FirstOrDefaultAsync();
        return cached;
    }

    public async Task<bool> AddTagAsync(string setCode, string tag)
    {
        var code = setCode.ToLowerInvariant();
        var setExists = await _db.CachedSets.AnyAsync(s => s.Code == code);
        if (!setExists) return false;

        var alreadyTagged = await _db.SetTags.AnyAsync(t => t.SetCode == code && t.Tag == tag);
        if (alreadyTagged) return true;

        _db.SetTags.Add(new SetTag { SetCode = code, Tag = tag });
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RemoveTagAsync(string setCode, string tag)
    {
        var code = setCode.ToLowerInvariant();
        var existing = await _db.SetTags.FirstOrDefaultAsync(t => t.SetCode == code && t.Tag == tag);
        if (existing == null) return false;

        _db.SetTags.Remove(existing);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<List<MtgCard>> GetCardsForSetAsync(string setCode)
    {
        var code = setCode.ToLowerInvariant();
        var cachedCards = await _db.CachedCards.Where(c => c.SetCode == code).ToListAsync();

        return cachedCards
            .Select(MapToMtgCard)
            .OrderBy(c => int.TryParse(
                c.CollectorNumber.TrimEnd('a', 'b', 's', 'p'),
                out var num) ? num : 9999)
            .ThenBy(c => c.CollectorNumber)
            .ToList();
    }

    public async Task<List<MtgCard>> GetReserveListCardsAsync()
    {
        var cachedCards = await _db.CachedCards.Where(c => c.IsReserved).ToListAsync();
        return cachedCards.Select(MapToMtgCard).ToList();
    }

    public async Task<string?> GetCardImagePathAsync(string cardId)
    {
        var card = await _db.CachedCards.FindAsync(cardId);
        if (card == null) return null;

        if (!string.IsNullOrEmpty(card.LocalImagePath))
        {
            var fullPath = Path.Combine(AppContext.BaseDirectory, card.LocalImagePath);
            if (File.Exists(fullPath))
                return fullPath;
        }

        return null;
    }

    private MtgCard MapToMtgCard(Entities.CachedCard cached)
    {
        ImageUris? imageUris = null;
        if (!string.IsNullOrEmpty(cached.ImageUrisJson))
            imageUris = JsonSerializer.Deserialize<ImageUris>(cached.ImageUrisJson, _jsonOptions);

        List<CardFace>? cardFaces = null;
        if (!string.IsNullOrEmpty(cached.CardFacesJson))
            cardFaces = JsonSerializer.Deserialize<List<CardFace>>(cached.CardFacesJson, _jsonOptions);

        List<string>? colorIdentity = null;
        if (!string.IsNullOrEmpty(cached.ColorIdentity))
            colorIdentity = JsonSerializer.Deserialize<List<string>>(cached.ColorIdentity, _jsonOptions);

        return new MtgCard
        {
            Id = cached.Id,
            Name = cached.Name,
            Set = cached.SetCode,
            SetName = cached.SetName,
            CollectorNumber = cached.CollectorNumber,
            Rarity = cached.Rarity,
            TypeLine = cached.TypeLine,
            ManaCost = cached.ManaCost,
            OracleText = cached.OracleText,
            ColorIdentity = colorIdentity,
            Reserved = cached.IsReserved,
            ImageUris = imageUris,
            CardFaces = cardFaces,
            Prices = new CardPrices
            {
                Usd = cached.PriceUsd,
                UsdFoil = cached.PriceUsdFoil
            },
            ScryfallUri = cached.ScryfallUri
        };
    }
}
