using System.Text;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using CountOrSell.Core.Data;

namespace CountOrSell.Core.Services;

public interface IExportService
{
    Task<byte[]> ExportOwnedCardsAsCsvAsync(string userId);
    Task<byte[]> ExportOwnedCardsAsXmlAsync(string userId);
    Task<byte[]> ExportBoostersAsCsvAsync(string userId);
    Task<byte[]> ExportReserveListAsCsvAsync(string userId);
}

public class ExportService : IExportService
{
    private readonly CountOrSellDbContext _db;

    public ExportService(CountOrSellDbContext db)
    {
        _db = db;
    }

    public async Task<byte[]> ExportOwnedCardsAsCsvAsync(string userId)
    {
        var cards = await _db.CardOwnerships
            .Where(c => c.UserId == userId && c.Owned)
            .OrderBy(c => c.SetCode)
            .ThenBy(c => c.CardName)
            .ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("CardName,SetCode,CollectorNumber,ScryfallCardId");
        foreach (var card in cards)
        {
            sb.AppendLine($"\"{EscapeCsv(card.CardName)}\",{card.SetCode},{card.CollectorNumber},{card.ScryfallCardId}");
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    public async Task<byte[]> ExportOwnedCardsAsXmlAsync(string userId)
    {
        var cards = await _db.CardOwnerships
            .Where(c => c.UserId == userId && c.Owned)
            .OrderBy(c => c.SetCode)
            .ThenBy(c => c.CardName)
            .ToListAsync();

        var doc = new XDocument(
            new XElement("Collection",
                cards.Select(c => new XElement("Card",
                    new XElement("Name", c.CardName),
                    new XElement("SetCode", c.SetCode),
                    new XElement("CollectorNumber", c.CollectorNumber),
                    new XElement("ScryfallId", c.ScryfallCardId)
                ))
            )
        );

        return Encoding.UTF8.GetBytes(doc.ToString());
    }

    public async Task<byte[]> ExportBoostersAsCsvAsync(string userId)
    {
        var boosters = await _db.BoosterDefinitions
            .Where(b => b.UserId == userId)
            .OrderBy(b => b.SetCode)
            .ThenBy(b => b.BoosterType)
            .ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("SetCode,BoosterType,ArtVariant,Owned");
        foreach (var b in boosters)
        {
            sb.AppendLine($"{b.SetCode},\"{EscapeCsv(b.BoosterType)}\",\"{EscapeCsv(b.ArtVariant)}\",{b.Owned}");
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    public async Task<byte[]> ExportReserveListAsCsvAsync(string userId)
    {
        var items = await _db.ReserveListCardOwnerships
            .Where(r => r.UserId == userId && r.Owned)
            .OrderBy(r => r.SetCode)
            .ThenBy(r => r.CardName)
            .ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("CardName,SetCode,ScryfallCardId,Owned");
        foreach (var item in items)
        {
            sb.AppendLine($"\"{EscapeCsv(item.CardName)}\",{item.SetCode},{item.ScryfallCardId},{item.Owned}");
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static string EscapeCsv(string value)
    {
        return value.Replace("\"", "\"\"");
    }
}
