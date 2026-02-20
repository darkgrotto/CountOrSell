using System.Reflection;
using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using MtgHelper.Core.Models;

namespace MtgHelper.Core.Services;

public interface ILabelService
{
    byte[] GenerateLabel(MtgSet set, string boxType, byte[]? iconSvg = null);
}

public class LabelService : ILabelService
{
    private const string KeyruneFontFamily = "Keyrune";
    private static bool _fontRegistered = false;
    private static readonly object _fontLock = new();

    public LabelService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        RegisterKeyruneFont();
    }

    private static void RegisterKeyruneFont()
    {
        if (_fontRegistered) return;

        lock (_fontLock)
        {
            if (_fontRegistered) return;

            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var resourceName = "MtgHelper.Core.Resources.keyrune.ttf";

                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream != null)
                {
                    FontManager.RegisterFont(stream);
                    _fontRegistered = true;
                }
            }
            catch
            {
                // Font registration failed - fall back to text display
            }
        }
    }

    public byte[] GenerateLabel(MtgSet set, string boxType, byte[]? iconSvg = null)
    {
        const float widthPt = 270f;
        const float heightPt = 198f;

        var year = string.IsNullOrEmpty(set.ReleasedAt) ? "????"
            : set.ReleasedAt.Length >= 4 ? set.ReleasedAt[..4] : "????";

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(widthPt, heightPt, Unit.Point);
                page.Margin(8, Unit.Point);

                page.Content()
                    .Border(1)
                    .BorderColor(Colors.Black)
                    .Padding(8)
                    .Column(column =>
                    {
                        column.Item().Row(row =>
                        {
                            row.RelativeItem(1).AlignLeft().Text(text =>
                            {
                                text.Span("\ue684").FontFamily(KeyruneFontFamily).FontSize(29);
                            });
                            row.RelativeItem(1).AlignCenter().Text(text =>
                            {
                                text.Span(year).FontSize(20);
                            });
                            row.RelativeItem(1).AlignRight().Text(text =>
                            {
                                text.Span($"{set.CardCount} cards").FontSize(20);
                            });
                        });

                        var keyruneSymbol = KeyruneMapper.GetSymbol(set.Code);

                        column.Item()
                            .PaddingVertical(5)
                            .AlignCenter()
                            .AlignMiddle()
                            .Column(center =>
                            {
                                center.Item()
                                    .AlignCenter()
                                    .Text(text =>
                                    {
                                        text.AlignCenter();
                                        if (keyruneSymbol.HasValue && _fontRegistered)
                                        {
                                            text.Span(keyruneSymbol.Value.ToString())
                                                .FontFamily(KeyruneFontFamily)
                                                .FontSize(120);
                                        }
                                        else
                                        {
                                            text.Span(set.Code.ToUpperInvariant())
                                                .FontSize(56)
                                                .Bold();
                                        }
                                    });

                                center.Item()
                                    .AlignCenter()
                                    .Text(text =>
                                    {
                                        text.AlignCenter();
                                        var name = set.Name.Length > 25 ? set.Name[..22] + "..." : set.Name;
                                        text.Span(name).FontSize(18).Bold();
                                    });
                            });

                        column.Item().Extend();

                        column.Item().Row(row =>
                        {
                            row.RelativeItem(1).AlignLeft().Text(text =>
                            {
                                text.Span(set.Code.ToUpperInvariant()).FontSize(20).Bold();
                            });
                            row.RelativeItem(1).AlignRight().Text(text =>
                            {
                                text.Span(boxType).FontSize(20).Italic();
                            });
                        });
                    });
            });
        });

        return document.GeneratePdf();
    }
}
