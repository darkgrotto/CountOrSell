using System.Text;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using CountOrSell.Core.Data;

namespace CountOrSell.Core.Services;

public interface IExportService
{
    Task<byte[]> ExportOwnedCardsAsCsvAsync(string userId);
    Task<byte[]> ExportOwnedCardsAsXmlAsync(string userId);
    Task<byte[]> ExportBoostersAsCsvAsync(string userId);
    Task<byte[]> ExportReserveListAsCsvAsync(string userId);
    Task<byte[]> ExportSlabbedCardsAsPdfAsync(string userId);
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
            .Where(c => c.UserId == userId && c.Quantity > 0)
            .OrderBy(c => c.SetCode)
            .ThenBy(c => c.CardName)
            .ThenBy(c => c.Variant)
            .ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("CardName,SetCode,CollectorNumber,ScryfallCardId,Variant,Quantity");
        foreach (var card in cards)
        {
            sb.AppendLine($"\"{EscapeCsv(card.CardName)}\",{card.SetCode},{card.CollectorNumber},{card.ScryfallCardId},\"{EscapeCsv(card.Variant)}\",{card.Quantity}");
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    public async Task<byte[]> ExportOwnedCardsAsXmlAsync(string userId)
    {
        var cards = await _db.CardOwnerships
            .Where(c => c.UserId == userId && c.Quantity > 0)
            .OrderBy(c => c.SetCode)
            .ThenBy(c => c.CardName)
            .ThenBy(c => c.Variant)
            .ToListAsync();

        var doc = new XDocument(
            new XElement("Collection",
                cards.Select(c => new XElement("Card",
                    new XElement("Name", c.CardName),
                    new XElement("SetCode", c.SetCode),
                    new XElement("CollectorNumber", c.CollectorNumber),
                    new XElement("ScryfallId", c.ScryfallCardId),
                    new XElement("Variant", c.Variant),
                    new XElement("Quantity", c.Quantity)
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

    public async Task<byte[]> ExportSlabbedCardsAsPdfAsync(string userId)
    {
        var slabs = await _db.SlabbedCards
            .Where(s => s.UserId == userId)
            .OrderBy(s => s.GradingCompany)
            .ThenByDescending(s => s.Grade)
            .ThenBy(s => s.CardName)
            .ToListAsync();

        QuestPDF.Settings.License = LicenseType.Community;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(1, Unit.Centimetre);

                page.Header()
                    .Text("Slabbed Collection")
                    .Bold()
                    .FontSize(18);

                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(2); // Cert ID
                        columns.RelativeColumn(4); // Card Name
                        columns.RelativeColumn(1); // Set
                        columns.RelativeColumn(2); // Variant
                        columns.RelativeColumn(1); // Company
                        columns.RelativeColumn(1); // Grade
                        columns.RelativeColumn(2); // Date Acquired
                        columns.RelativeColumn(2); // Price
                    });

                    table.Header(header =>
                    {
                        static IContainer HeaderCell(IContainer c) =>
                            c.DefaultTextStyle(x => x.Bold()).Padding(5).Background(Colors.Grey.Lighten2);

                        header.Cell().Element(HeaderCell).Text("Cert ID");
                        header.Cell().Element(HeaderCell).Text("Card Name");
                        header.Cell().Element(HeaderCell).Text("Set");
                        header.Cell().Element(HeaderCell).Text("Variant");
                        header.Cell().Element(HeaderCell).Text("Company");
                        header.Cell().Element(HeaderCell).Text("Grade");
                        header.Cell().Element(HeaderCell).Text("Date Acquired");
                        header.Cell().Element(HeaderCell).Text("Price");
                    });

                    foreach (var slab in slabs)
                    {
                        table.Cell().Padding(5).Text(slab.CertificationNumber);
                        table.Cell().Padding(5).Text(slab.CardName);
                        table.Cell().Padding(5).Text(slab.SetCode.ToUpperInvariant());
                        table.Cell().Padding(5).Text(slab.CardVariant);
                        table.Cell().Padding(5).Text(slab.GradingCompany);
                        table.Cell().Padding(5).Text(slab.Grade);
                        table.Cell().Padding(5).Text(slab.PurchaseDate?.ToString("yyyy-MM-dd") ?? "");
                        table.Cell().Padding(5).Text(slab.PurchaseCost.HasValue ? $"${slab.PurchaseCost:F2}" : "");
                    }
                });
            });
        }).GeneratePdf();
    }

    private static string EscapeCsv(string value)
    {
        return value.Replace("\"", "\"\"");
    }
}
