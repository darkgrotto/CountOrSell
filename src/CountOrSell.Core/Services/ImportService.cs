using System.Text;
using Microsoft.EntityFrameworkCore;
using CountOrSell.Core.Data;
using CountOrSell.Core.Entities;
using CountOrSell.Core.Models;

namespace CountOrSell.Core.Services;

public interface IImportService
{
    Task<ImportResult> ImportCollectionAsync(string userId, Stream fileStream, string filename);
}

public class ImportService : IImportService
{
    private readonly CountOrSellDbContext _db;

    public ImportService(CountOrSellDbContext db)
    {
        _db = db;
    }

    // -------------------------------------------------------------------------
    // Parsed intermediate row (common across all formats)
    // -------------------------------------------------------------------------

    private record ParsedRow(
        string? ScryfallId,
        string? SetCode,        // lowercase set code, if known
        string? SetName,        // full set name, if set code not available
        string? CardName,
        string? CollectorNumber,
        string Variant,
        int Quantity);

    // -------------------------------------------------------------------------
    // Public entry point
    // -------------------------------------------------------------------------

    public async Task<ImportResult> ImportCollectionAsync(string userId, Stream fileStream, string filename)
    {
        string content;
        using (var reader = new StreamReader(fileStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
            content = await reader.ReadToEndAsync();

        var ext = Path.GetExtension(filename).ToLowerInvariant();

        if (ext == ".xml")
            return await ImportXmlAsync(userId, content);

        // CSV / text path
        var rawLines = content.Split('\n');
        var lines = rawLines.Select(l => l.TrimEnd('\r')).Where(l => l.Length > 0).ToArray();

        if (lines.Length < 2)
            return new ImportResult { Error = "File is empty or has no data rows" };

        var headers = ParseCsvLine(lines[0]);
        var headerMap = BuildHeaderMap(headers);
        var format = DetectFormat(headerMap);
        var dataLines = lines.Skip(1).ToArray();

        List<ParsedRow> rows = format switch
        {
            "CountOrSell" => ParseCountOrSellRows(dataLines, headerMap),
            "Moxfield" => ParseMoxfieldRows(dataLines, headerMap),
            "TCGPlayer" => ParseTCGPlayerRows(dataLines, headerMap),
            "DragonShield" => ParseDragonShieldRows(dataLines, headerMap),
            _ => ParseGenericRows(dataLines, headerMap)
        };

        return await MatchAndImportAsync(userId, rows, format);
    }

    // -------------------------------------------------------------------------
    // Format detection
    // -------------------------------------------------------------------------

    private static Dictionary<string, int> BuildHeaderMap(string[] headers)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < headers.Length; i++)
            map.TryAdd(headers[i].Trim(), i);
        return map;
    }

    private static string DetectFormat(Dictionary<string, int> h)
    {
        if (h.ContainsKey("ScryfallCardId") || h.ContainsKey("Scryfall Card Id"))
            return "CountOrSell";
        if (h.ContainsKey("Tradelist Count") || (h.ContainsKey("Edition") && h.ContainsKey("Foil")))
            return "Moxfield";
        if (h.ContainsKey("Simple Name") || h.ContainsKey("Card Number") && h.ContainsKey("Set"))
            return "TCGPlayer";
        if (h.ContainsKey("Folder Name") || h.ContainsKey("Set Code") && h.ContainsKey("Card Name"))
            return "DragonShield";
        return "Generic";
    }

    // -------------------------------------------------------------------------
    // Format-specific parsers
    // -------------------------------------------------------------------------

    private List<ParsedRow> ParseCountOrSellRows(string[] lines, Dictionary<string, int> h)
    {
        var rows = new List<ParsedRow>();
        int? iName = Idx(h, "CardName"),
             iSet  = Idx(h, "SetCode"),
             iNum  = Idx(h, "CollectorNumber"),
             iId   = Idx(h, "ScryfallCardId", "Scryfall Card Id"),
             iVar  = Idx(h, "Variant"),
             iQty  = Idx(h, "Quantity");

        foreach (var line in lines)
        {
            var f = ParseCsvLine(line);
            if (f.Length == 0) continue;
            rows.Add(new ParsedRow(
                ScryfallId: Get(f, iId),
                SetCode: Get(f, iSet)?.ToLowerInvariant(),
                SetName: null,
                CardName: Get(f, iName),
                CollectorNumber: Get(f, iNum),
                Variant: Get(f, iVar) ?? "Regular",
                Quantity: int.TryParse(Get(f, iQty), out var q) ? q : 1));
        }
        return rows;
    }

    private List<ParsedRow> ParseMoxfieldRows(string[] lines, Dictionary<string, int> h)
    {
        var rows = new List<ParsedRow>();
        int? iCount = Idx(h, "Count"),
             iName  = Idx(h, "Name"),
             iEd    = Idx(h, "Edition"),
             iFoil  = Idx(h, "Foil"),
             iNum   = Idx(h, "Collector Number");

        foreach (var line in lines)
        {
            var f = ParseCsvLine(line);
            if (f.Length == 0) continue;
            var foil = Get(f, iFoil);
            rows.Add(new ParsedRow(
                ScryfallId: null,
                SetCode: Get(f, iEd)?.ToLowerInvariant(),
                SetName: null,
                CardName: Get(f, iName),
                CollectorNumber: Get(f, iNum),
                Variant: MapMoxfieldFoil(foil),
                Quantity: int.TryParse(Get(f, iCount), out var q) ? q : 1));
        }
        return rows;
    }

    private List<ParsedRow> ParseTCGPlayerRows(string[] lines, Dictionary<string, int> h)
    {
        var rows = new List<ParsedRow>();
        int? iQty      = Idx(h, "Quantity"),
             iName     = Idx(h, "Simple Name", "Name"),
             iSet      = Idx(h, "Set"),
             iNum      = Idx(h, "Card Number"),
             iPrinting = Idx(h, "Printing");

        foreach (var line in lines)
        {
            var f = ParseCsvLine(line);
            if (f.Length == 0) continue;
            rows.Add(new ParsedRow(
                ScryfallId: null,
                SetCode: null,
                SetName: Get(f, iSet),           // TCGPlayer gives set name, not code
                CardName: Get(f, iName),
                CollectorNumber: Get(f, iNum),
                Variant: MapPrinting(Get(f, iPrinting)),
                Quantity: int.TryParse(Get(f, iQty), out var q) ? q : 1));
        }
        return rows;
    }

    private List<ParsedRow> ParseDragonShieldRows(string[] lines, Dictionary<string, int> h)
    {
        var rows = new List<ParsedRow>();
        int? iQty      = Idx(h, "Quantity"),
             iName     = Idx(h, "Card Name"),
             iSetCode  = Idx(h, "Set Code"),
             iNum      = Idx(h, "Card Number"),
             iPrinting = Idx(h, "Printing");

        foreach (var line in lines)
        {
            var f = ParseCsvLine(line);
            if (f.Length == 0) continue;
            rows.Add(new ParsedRow(
                ScryfallId: null,
                SetCode: Get(f, iSetCode)?.ToLowerInvariant(),
                SetName: null,
                CardName: Get(f, iName),
                CollectorNumber: Get(f, iNum),
                Variant: MapPrinting(Get(f, iPrinting)),
                Quantity: int.TryParse(Get(f, iQty), out var q) ? q : 1));
        }
        return rows;
    }

    private List<ParsedRow> ParseGenericRows(string[] lines, Dictionary<string, int> h)
    {
        // Best-effort: look for common column names
        int? iQty  = Idx(h, "Quantity", "Count", "Qty", "Amount"),
             iName = Idx(h, "Name", "Card Name", "CardName"),
             iSet  = Idx(h, "Set", "SetCode", "Set Code", "Edition"),
             iNum  = Idx(h, "Collector Number", "Card Number", "CollectorNumber", "Number"),
             iVar  = Idx(h, "Variant", "Foil", "Printing", "Finish");

        var rows = new List<ParsedRow>();
        foreach (var line in lines)
        {
            var f = ParseCsvLine(line);
            if (f.Length == 0) continue;
            rows.Add(new ParsedRow(
                ScryfallId: null,
                SetCode: Get(f, iSet)?.ToLowerInvariant(),
                SetName: null,
                CardName: Get(f, iName),
                CollectorNumber: Get(f, iNum),
                Variant: MapPrinting(Get(f, iVar)),
                Quantity: int.TryParse(Get(f, iQty), out var q) ? q : 1));
        }
        return rows;
    }

    // -------------------------------------------------------------------------
    // XML import (CountOrSell XML format)
    // -------------------------------------------------------------------------

    private async Task<ImportResult> ImportXmlAsync(string userId, string content)
    {
        System.Xml.Linq.XDocument doc;
        try { doc = System.Xml.Linq.XDocument.Parse(content); }
        catch { return new ImportResult { Error = "Invalid XML file" }; }

        var rows = doc.Root?
            .Elements("Card")
            .Select(el => new ParsedRow(
                ScryfallId: (string?)el.Element("ScryfallId"),
                SetCode: ((string?)el.Element("SetCode"))?.ToLowerInvariant(),
                SetName: null,
                CardName: (string?)el.Element("Name"),
                CollectorNumber: (string?)el.Element("CollectorNumber"),
                Variant: (string?)el.Element("Variant") ?? "Regular",
                Quantity: int.TryParse((string?)el.Element("Quantity"), out var q) ? q : 1))
            .ToList() ?? new List<ParsedRow>();

        return await MatchAndImportAsync(userId, rows, "CountOrSell XML");
    }

    // -------------------------------------------------------------------------
    // Card matching and DB upsert
    // -------------------------------------------------------------------------

    private async Task<ImportResult> MatchAndImportAsync(string userId, List<ParsedRow> rows, string format)
    {
        if (rows.Count == 0)
            return new ImportResult { DetectedFormat = format, Error = "No data rows found" };

        // Build set-name → set-code map for TCGPlayer-style imports
        var needsSetNameLookup = rows.Any(r => r.SetName != null && r.SetCode == null);
        Dictionary<string, string> setNameToCode = new(StringComparer.OrdinalIgnoreCase);
        if (needsSetNameLookup)
        {
            var sets = await _db.CachedSets.Select(s => new { s.Code, s.Name }).ToListAsync();
            foreach (var s in sets)
                setNameToCode.TryAdd(s.Name, s.Code.ToLowerInvariant());
        }

        // Resolve all set codes we need, then batch-load cards by set
        var setCodes = rows
            .Select(r => r.SetCode ?? (r.SetName != null && setNameToCode.TryGetValue(r.SetName, out var c) ? c : null))
            .Where(c => c != null)
            .Distinct()
            .Cast<string>()
            .ToList();

        // Load cards keyed by (setCode, collectorNumber) and (setCode, name)
        var cardsBySet = await _db.CachedCards
            .Where(c => setCodes.Contains(c.SetCode))
            .ToListAsync();

        var bySetAndNumber = cardsBySet
            .Where(c => !string.IsNullOrEmpty(c.CollectorNumber))
            .GroupBy(c => (c.SetCode, c.CollectorNumber))
            .ToDictionary(g => g.Key, g => g.First());

        var bySetAndName = cardsBySet
            .GroupBy(c => (c.SetCode, c.Name))
            .ToDictionary(g => g.Key, g => g.First());

        // For direct Scryfall ID lookups, collect all needed IDs and batch-load
        var scryfallIds = rows
            .Where(r => !string.IsNullOrEmpty(r.ScryfallId))
            .Select(r => r.ScryfallId!)
            .Distinct()
            .ToList();

        var byId = scryfallIds.Count > 0
            ? (await _db.CachedCards.Where(c => scryfallIds.Contains(c.Id)).ToListAsync())
              .ToDictionary(c => c.Id)
            : new Dictionary<string, CachedCard>();

        // Load existing ownership records for this user (for upsert)
        var existingOwnerships = await _db.CardOwnerships
            .Where(o => o.UserId == userId)
            .ToListAsync();
        var ownershipMap = existingOwnerships
            .ToDictionary(o => (o.ScryfallCardId, o.Variant));

        int imported = 0;
        var unmatched = new List<string>();

        foreach (var row in rows)
        {
            if (row.Quantity <= 0) continue;

            // --- Find the card ---
            CachedCard? card = null;

            if (!string.IsNullOrEmpty(row.ScryfallId))
                byId.TryGetValue(row.ScryfallId, out card);

            if (card == null)
            {
                var resolvedCode = row.SetCode
                    ?? (row.SetName != null && setNameToCode.TryGetValue(row.SetName, out var c2) ? c2 : null);

                if (resolvedCode != null && !string.IsNullOrEmpty(row.CollectorNumber))
                    bySetAndNumber.TryGetValue((resolvedCode, row.CollectorNumber), out card);

                if (card == null && resolvedCode != null && !string.IsNullOrEmpty(row.CardName))
                    bySetAndName.TryGetValue((resolvedCode, row.CardName), out card);
            }

            if (card == null)
            {
                var label = row.CardName ?? row.ScryfallId ?? "Unknown";
                if (!string.IsNullOrEmpty(row.SetCode))
                    label += $" [{row.SetCode.ToUpperInvariant()}]";
                else if (!string.IsNullOrEmpty(row.SetName))
                    label += $" [{row.SetName}]";
                unmatched.Add(label);
                continue;
            }

            // --- Upsert ownership ---
            var key = (card.Id, row.Variant);
            if (ownershipMap.TryGetValue(key, out var existing))
            {
                existing.Quantity = row.Quantity;
                existing.Owned = row.Quantity > 0;
            }
            else
            {
                var newOwnership = new CardOwnership
                {
                    UserId = userId,
                    ScryfallCardId = card.Id,
                    CardName = card.Name,
                    SetCode = card.SetCode,
                    CollectorNumber = card.CollectorNumber,
                    Variant = row.Variant,
                    Quantity = row.Quantity,
                    Owned = true
                };
                _db.CardOwnerships.Add(newOwnership);
                ownershipMap[key] = newOwnership;
            }

            imported++;

            // Save in batches of 500 to avoid huge change trackers
            if (imported % 500 == 0)
                await _db.SaveChangesAsync();
        }

        await _db.SaveChangesAsync();

        return new ImportResult
        {
            Imported = imported,
            Unmatched = unmatched.Count,
            UnmatchedCards = unmatched.Take(50).ToList(),   // cap list at 50
            DetectedFormat = format
        };
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static string MapMoxfieldFoil(string? foil) =>
        foil?.Trim() switch
        {
            "*F*" or "foil" or "yes" or "true" => "Foil",
            "*E*" or "etched" => "Etched Foil",
            _ => "Regular"
        };

    private static string MapPrinting(string? printing) =>
        printing?.Trim().ToLowerInvariant() switch
        {
            "foil" => "Foil",
            "etched" or "etched foil" => "Etched Foil",
            "galaxy foil" => "Galaxy Foil",
            "gilded foil" => "Gilded Foil",
            "surge foil" => "Surge Foil",
            "fracture foil" => "Fracture Foil",
            "textured foil" => "Textured Foil",
            "serialized" => "Serialized",
            _ => "Regular"
        };

    private static int? Idx(Dictionary<string, int> h, params string[] names)
    {
        foreach (var name in names)
            if (h.TryGetValue(name, out var i)) return i;
        return null;
    }

    private static string? Get(string[] fields, int? idx) =>
        idx.HasValue && idx.Value < fields.Length ? fields[idx.Value].Trim() : null;

    private static string[] ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var sb = new StringBuilder();
        bool inQuotes = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (inQuotes)
            {
                if (c == '"' && i + 1 < line.Length && line[i + 1] == '"')
                { sb.Append('"'); i++; }
                else if (c == '"') inQuotes = false;
                else sb.Append(c);
            }
            else
            {
                if (c == '"') inQuotes = true;
                else if (c == ',') { fields.Add(sb.ToString()); sb.Clear(); }
                else sb.Append(c);
            }
        }
        fields.Add(sb.ToString());
        return fields.ToArray();
    }
}
