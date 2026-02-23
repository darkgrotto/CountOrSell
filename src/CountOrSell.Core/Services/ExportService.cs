using System.Text;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using CountOrSell.Core.Data;
using CountOrSell.Core.Models;

namespace CountOrSell.Core.Services;

public interface IExportService
{
    Task<byte[]> ExportOwnedCardsAsCsvAsync(string userId);
    Task<byte[]> ExportOwnedCardsAsXmlAsync(string userId);
    Task<byte[]> ExportBoostersAsCsvAsync(string userId);
    Task<byte[]> ExportReserveListAsCsvAsync(string userId);
    Task<byte[]> ExportSlabbedCardsAsPdfAsync(string userId);

    // Collection exports
    Task<byte[]> ExportCollectionSummaryAsCsvAsync(string userId);
    Task<byte[]> ExportCollectionSummaryAsXmlAsync(string userId);
    Task<byte[]> ExportCollectionSummaryAsPdfAsync(string userId);
    Task<byte[]> ExportCollectionDetailedAsCsvAsync(string userId, CollectionFilter filter);
    Task<byte[]> ExportCollectionDetailedAsXmlAsync(string userId, CollectionFilter filter);
    Task<byte[]> ExportCollectionDetailedAsPdfAsync(string userId, CollectionFilter filter);
}

public class ExportService : IExportService
{
    private readonly CountOrSellDbContext _db;
    private readonly ICollectionService _collectionService;

    public ExportService(CountOrSellDbContext db, ICollectionService collectionService)
    {
        _db = db;
        _collectionService = collectionService;
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

    // ---- Collection Exports ----

    public async Task<byte[]> ExportCollectionSummaryAsCsvAsync(string userId)
    {
        var summary = await _collectionService.GetCollectionSummaryAsync(userId);
        var sb = new StringBuilder();

        sb.AppendLine("Section: Overall");
        sb.AppendLine("TotalCopies,TotalUniqueCards,TotalValue");
        sb.AppendLine($"{summary.TotalCopies},{summary.TotalUniqueCards},{summary.TotalValue:F2}");
        sb.AppendLine();

        sb.AppendLine("Section: By Rarity");
        sb.AppendLine("Rarity,Copies,Value");
        foreach (var (rarity, copies) in summary.ByRarity.OrderBy(k => k.Key))
        {
            summary.ValueByRarity.TryGetValue(rarity, out var val);
            sb.AppendLine($"\"{EscapeCsv(rarity)}\",{copies},{val:F2}");
        }
        sb.AppendLine();

        sb.AppendLine("Section: By Type");
        sb.AppendLine("Type,Copies");
        foreach (var (type, copies) in summary.ByType.OrderBy(k => k.Key))
            sb.AppendLine($"\"{EscapeCsv(type)}\",{copies}");
        sb.AppendLine();

        sb.AppendLine("Section: By Variant");
        sb.AppendLine("Variant,Copies");
        foreach (var (variant, copies) in summary.ByVariant.OrderBy(k => k.Key))
            sb.AppendLine($"\"{EscapeCsv(variant)}\",{copies}");
        sb.AppendLine();

        sb.AppendLine("Section: Reserve List");
        sb.AppendLine("Owned,Value");
        sb.AppendLine($"{summary.ReserveListOwned},{summary.ReserveListValue:F2}");
        sb.AppendLine();

        sb.AppendLine("Section: Boosters");
        sb.AppendLine("Owned,Total");
        sb.AppendLine($"{summary.BoostersOwned},{summary.BoostersTotal}");

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    public async Task<byte[]> ExportCollectionSummaryAsXmlAsync(string userId)
    {
        var summary = await _collectionService.GetCollectionSummaryAsync(userId);

        var doc = new XDocument(
            new XElement("CollectionSummary",
                new XElement("Overall",
                    new XElement("TotalCopies", summary.TotalCopies),
                    new XElement("TotalUniqueCards", summary.TotalUniqueCards),
                    new XElement("TotalValue", summary.TotalValue.ToString("F2"))
                ),
                new XElement("ByRarity",
                    summary.ByRarity.OrderBy(k => k.Key).Select(kvp =>
                    {
                        summary.ValueByRarity.TryGetValue(kvp.Key, out var val);
                        return new XElement("Entry",
                            new XAttribute("rarity", kvp.Key),
                            new XAttribute("copies", kvp.Value),
                            new XAttribute("value", val.ToString("F2")));
                    })
                ),
                new XElement("ByType",
                    summary.ByType.OrderBy(k => k.Key).Select(kvp =>
                        new XElement("Entry",
                            new XAttribute("type", kvp.Key),
                            new XAttribute("copies", kvp.Value)))
                ),
                new XElement("ByVariant",
                    summary.ByVariant.OrderBy(k => k.Key).Select(kvp =>
                        new XElement("Entry",
                            new XAttribute("variant", kvp.Key),
                            new XAttribute("copies", kvp.Value)))
                ),
                new XElement("ReserveList",
                    new XElement("Owned", summary.ReserveListOwned),
                    new XElement("Value", summary.ReserveListValue.ToString("F2"))
                ),
                new XElement("Boosters",
                    new XElement("Owned", summary.BoostersOwned),
                    new XElement("Total", summary.BoostersTotal)
                )
            )
        );

        return Encoding.UTF8.GetBytes(doc.ToString());
    }

    public async Task<byte[]> ExportCollectionSummaryAsPdfAsync(string userId)
    {
        var summary = await _collectionService.GetCollectionSummaryAsync(userId);

        QuestPDF.Settings.License = LicenseType.Community;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.5f, Unit.Centimetre);

                page.Header()
                    .Text("My Collection — Summary")
                    .Bold()
                    .FontSize(20);

                page.Content().Column(col =>
                {
                    col.Spacing(12);

                    // Overall stats
                    col.Item().Text($"{summary.TotalCopies} copies  •  {summary.TotalUniqueCards} unique cards  •  ${summary.TotalValue:F2} total value")
                        .FontSize(12);

                    // Three breakdown tables in a row
                    col.Item().Row(row =>
                    {
                        row.Spacing(8);

                        // By Rarity
                        row.RelativeItem().Column(inner =>
                        {
                            inner.Item().Text("By Rarity").Bold().FontSize(11);
                            inner.Item().Table(t =>
                            {
                                t.ColumnsDefinition(c => { c.RelativeColumn(2); c.RelativeColumn(1); c.RelativeColumn(2); });
                                t.Header(h =>
                                {
                                    static IContainer Hdr(IContainer c) => c.DefaultTextStyle(x => x.Bold()).Padding(3).Background(Colors.Grey.Lighten2);
                                    h.Cell().Element(Hdr).Text("Rarity");
                                    h.Cell().Element(Hdr).Text("Copies");
                                    h.Cell().Element(Hdr).Text("Value");
                                });
                                foreach (var (rarity, copies) in summary.ByRarity.OrderBy(k => k.Key))
                                {
                                    summary.ValueByRarity.TryGetValue(rarity, out var val);
                                    t.Cell().Padding(3).Text(rarity);
                                    t.Cell().Padding(3).Text(copies.ToString());
                                    t.Cell().Padding(3).Text($"${val:F2}");
                                }
                            });
                        });

                        // By Type
                        row.RelativeItem().Column(inner =>
                        {
                            inner.Item().Text("By Type").Bold().FontSize(11);
                            inner.Item().Table(t =>
                            {
                                t.ColumnsDefinition(c => { c.RelativeColumn(3); c.RelativeColumn(1); });
                                t.Header(h =>
                                {
                                    static IContainer Hdr(IContainer c) => c.DefaultTextStyle(x => x.Bold()).Padding(3).Background(Colors.Grey.Lighten2);
                                    h.Cell().Element(Hdr).Text("Type");
                                    h.Cell().Element(Hdr).Text("Copies");
                                });
                                foreach (var (type, copies) in summary.ByType.OrderBy(k => k.Key))
                                {
                                    t.Cell().Padding(3).Text(type);
                                    t.Cell().Padding(3).Text(copies.ToString());
                                }
                            });
                        });

                        // By Variant
                        row.RelativeItem().Column(inner =>
                        {
                            inner.Item().Text("By Variant").Bold().FontSize(11);
                            inner.Item().Table(t =>
                            {
                                t.ColumnsDefinition(c => { c.RelativeColumn(3); c.RelativeColumn(1); });
                                t.Header(h =>
                                {
                                    static IContainer Hdr(IContainer c) => c.DefaultTextStyle(x => x.Bold()).Padding(3).Background(Colors.Grey.Lighten2);
                                    h.Cell().Element(Hdr).Text("Variant");
                                    h.Cell().Element(Hdr).Text("Copies");
                                });
                                foreach (var (variant, copies) in summary.ByVariant.OrderBy(k => k.Key))
                                {
                                    t.Cell().Padding(3).Text(variant);
                                    t.Cell().Padding(3).Text(copies.ToString());
                                }
                            });
                        });
                    });

                    col.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                    col.Item().Text($"Reserve List: {summary.ReserveListOwned} cards owned  •  ${summary.ReserveListValue:F2} value")
                        .FontSize(11);

                    col.Item().Text($"Boosters: {summary.BoostersOwned}/{summary.BoostersTotal} owned")
                        .FontSize(11);
                });
            });
        }).GeneratePdf();
    }

    public async Task<byte[]> ExportCollectionDetailedAsCsvAsync(string userId, CollectionFilter filter)
    {
        var cards = await _collectionService.GetAllOwnedCardsAsync(userId, filter);

        var sb = new StringBuilder();
        sb.AppendLine("CardName,SetCode,SetName,CollectorNumber,Rarity,TypeLine,Variant,Quantity,PriceEach,LineValue,IsReserved");

        foreach (var card in cards)
        {
            var lineValue = (card.PriceUsd ?? 0) * card.Quantity;
            sb.AppendLine(
                $"\"{EscapeCsv(card.CardName)}\",{card.SetCode},\"{EscapeCsv(card.SetName)}\",{card.CollectorNumber}," +
                $"{card.Rarity},\"{EscapeCsv(card.TypeLine ?? "")}\",\"{EscapeCsv(card.Variant)}\"," +
                $"{card.Quantity},{card.PriceUsd?.ToString("F2") ?? ""},{lineValue:F2},{card.IsReserved}");
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    public async Task<byte[]> ExportCollectionDetailedAsXmlAsync(string userId, CollectionFilter filter)
    {
        var cards = await _collectionService.GetAllOwnedCardsAsync(userId, filter);

        var doc = new XDocument(
            new XElement("Collection",
                cards.Select(card => new XElement("Card",
                    new XElement("CardName", card.CardName),
                    new XElement("SetCode", card.SetCode),
                    new XElement("SetName", card.SetName),
                    new XElement("CollectorNumber", card.CollectorNumber),
                    new XElement("Rarity", card.Rarity),
                    new XElement("TypeLine", card.TypeLine ?? ""),
                    new XElement("Variant", card.Variant),
                    new XElement("Quantity", card.Quantity),
                    new XElement("PriceEach", card.PriceUsd?.ToString("F2") ?? ""),
                    new XElement("LineValue", ((card.PriceUsd ?? 0) * card.Quantity).ToString("F2")),
                    new XElement("IsReserved", card.IsReserved)
                ))
            )
        );

        return Encoding.UTF8.GetBytes(doc.ToString());
    }

    public async Task<byte[]> ExportCollectionDetailedAsPdfAsync(string userId, CollectionFilter filter)
    {
        var cards = await _collectionService.GetAllOwnedCardsAsync(userId, filter);
        var totalQty = cards.Sum(c => c.Quantity);
        var totalValue = cards.Sum(c => (c.PriceUsd ?? 0) * c.Quantity);

        QuestPDF.Settings.License = LicenseType.Community;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(1, Unit.Centimetre);

                page.Header()
                    .Text("Collection — Detailed")
                    .Bold()
                    .FontSize(18);

                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(4); // Card Name
                        columns.RelativeColumn(1); // Set
                        columns.RelativeColumn(1); // #
                        columns.RelativeColumn(1); // Rarity
                        columns.RelativeColumn(3); // Type
                        columns.RelativeColumn(2); // Variant
                        columns.RelativeColumn(1); // Qty
                        columns.RelativeColumn(2); // $/each
                        columns.RelativeColumn(2); // Total
                    });

                    table.Header(header =>
                    {
                        static IContainer HeaderCell(IContainer c) =>
                            c.DefaultTextStyle(x => x.Bold()).Padding(4).Background(Colors.Grey.Lighten2);

                        header.Cell().Element(HeaderCell).Text("Card Name");
                        header.Cell().Element(HeaderCell).Text("Set");
                        header.Cell().Element(HeaderCell).Text("#");
                        header.Cell().Element(HeaderCell).Text("Rarity");
                        header.Cell().Element(HeaderCell).Text("Type");
                        header.Cell().Element(HeaderCell).Text("Variant");
                        header.Cell().Element(HeaderCell).Text("Qty");
                        header.Cell().Element(HeaderCell).Text("$/each");
                        header.Cell().Element(HeaderCell).Text("Total");
                    });

                    foreach (var card in cards)
                    {
                        var lineValue = (card.PriceUsd ?? 0) * card.Quantity;
                        table.Cell().Padding(4).Text(card.CardName);
                        table.Cell().Padding(4).Text(card.SetCode.ToUpperInvariant());
                        table.Cell().Padding(4).Text(card.CollectorNumber);
                        table.Cell().Padding(4).Text(card.Rarity);
                        table.Cell().Padding(4).Text(card.TypeLine ?? "");
                        table.Cell().Padding(4).Text(card.Variant);
                        table.Cell().Padding(4).Text(card.Quantity.ToString());
                        table.Cell().Padding(4).Text(card.PriceUsd.HasValue ? $"${card.PriceUsd:F2}" : "");
                        table.Cell().Padding(4).Text($"${lineValue:F2}");
                    }

                    // Footer totals row
                    static IContainer FooterCell(IContainer c) =>
                        c.DefaultTextStyle(x => x.Bold()).Padding(4).Background(Colors.Grey.Lighten3);

                    table.Cell().ColumnSpan(6).Element(FooterCell).Text("Totals");
                    table.Cell().Element(FooterCell).Text(totalQty.ToString());
                    table.Cell().Element(FooterCell).Text("");
                    table.Cell().Element(FooterCell).Text($"${totalValue:F2}");
                });
            });
        }).GeneratePdf();
    }

    private static string EscapeCsv(string value)
    {
        return value.Replace("\"", "\"\"");
    }
}
