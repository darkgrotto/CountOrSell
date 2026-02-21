using Microsoft.EntityFrameworkCore;
using CountOrSell.Core.Data;
using CountOrSell.Core.Entities;
using CountOrSell.Core.Models;

namespace CountOrSell.Core.Services;

public interface ISlabbedCardService
{
    Task<List<SlabbedCard>> GetAllAsync(string userId);
    Task<SlabbedCard?> GetByIdAsync(string userId, int id);
    Task<SlabbedCard> AddAsync(string userId, SlabbedCardRequest request);
    Task<SlabbedCard?> UpdateAsync(string userId, int id, SlabbedCardRequest request);
    Task<bool> DeleteAsync(string userId, int id);
}

public class SlabbedCardService : ISlabbedCardService
{
    private readonly CountOrSellDbContext _db;

    public SlabbedCardService(CountOrSellDbContext db)
    {
        _db = db;
    }

    public async Task<List<SlabbedCard>> GetAllAsync(string userId)
    {
        return await _db.SlabbedCards
            .Where(s => s.UserId == userId)
            .OrderBy(s => s.GradingCompany)
            .ThenByDescending(s => s.Grade)
            .ThenBy(s => s.CardName)
            .ToListAsync();
    }

    public async Task<SlabbedCard?> GetByIdAsync(string userId, int id)
    {
        return await _db.SlabbedCards
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Id == id);
    }

    public async Task<SlabbedCard> AddAsync(string userId, SlabbedCardRequest request)
    {
        var slab = new SlabbedCard
        {
            UserId = userId,
            ScryfallCardId = request.ScryfallCardId,
            CardName = request.CardName,
            SetCode = request.SetCode,
            SetName = request.SetName,
            CollectorNumber = request.CollectorNumber,
            CardVariant = request.CardVariant,
            GradingCompany = request.GradingCompany,
            Grade = request.Grade,
            CertificationNumber = request.CertificationNumber,
            PurchaseDate = request.PurchaseDate,
            PurchasedFrom = request.PurchasedFrom,
            PurchaseCost = request.PurchaseCost,
            Notes = request.Notes,
            CreatedAt = DateTime.UtcNow
        };

        _db.SlabbedCards.Add(slab);
        await _db.SaveChangesAsync();
        return slab;
    }

    public async Task<SlabbedCard?> UpdateAsync(string userId, int id, SlabbedCardRequest request)
    {
        var slab = await _db.SlabbedCards
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Id == id);
        if (slab == null) return null;

        slab.ScryfallCardId = request.ScryfallCardId;
        slab.CardName = request.CardName;
        slab.SetCode = request.SetCode;
        slab.SetName = request.SetName;
        slab.CollectorNumber = request.CollectorNumber;
        slab.CardVariant = request.CardVariant;
        slab.GradingCompany = request.GradingCompany;
        slab.Grade = request.Grade;
        slab.CertificationNumber = request.CertificationNumber;
        slab.PurchaseDate = request.PurchaseDate;
        slab.PurchasedFrom = request.PurchasedFrom;
        slab.PurchaseCost = request.PurchaseCost;
        slab.Notes = request.Notes;

        await _db.SaveChangesAsync();
        return slab;
    }

    public async Task<bool> DeleteAsync(string userId, int id)
    {
        var slab = await _db.SlabbedCards
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Id == id);
        if (slab == null) return false;

        _db.SlabbedCards.Remove(slab);
        await _db.SaveChangesAsync();
        return true;
    }
}
