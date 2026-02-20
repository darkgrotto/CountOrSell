using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MtgHelper.Core.Data;
using MtgHelper.Core.Models;

namespace MtgHelper.Core.Services;

public interface ICardDataService
{
    Task<List<MtgSet>> GetSetsAsync();
    Task<MtgSet?> GetSetAsync(string setCode);
    Task<List<MtgCard>> GetCardsForSetAsync(string setCode);
    Task<List<MtgCard>> GetReserveListCardsAsync();
    Task<string?> GetCardImagePathAsync(string cardId);
}

public class CardDataService : ICardDataService
{
    private readonly MtgHelperDbContext _db;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public CardDataService(MtgHelperDbContext db)
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
                ScryfallUri = s.ScryfallUri
            })
            .ToListAsync();
    }

    public async Task<MtgSet?> GetSetAsync(string setCode)
    {
        var code = setCode.ToLowerInvariant();
        var cached = await _db.CachedSets.FirstOrDefaultAsync(s => s.Code == code);
        if (cached == null) return null;

        return new MtgSet
        {
            Id = cached.Id,
            Code = cached.Code,
            Name = cached.Name,
            ReleasedAt = cached.ReleasedAt,
            SetType = cached.SetType,
            CardCount = cached.CardCount,
            IconSvgUri = cached.IconSvgUri,
            ScryfallUri = cached.ScryfallUri
        };
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
