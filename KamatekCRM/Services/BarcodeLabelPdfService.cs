using System;
using System.Globalization;
using System.IO;
using System.Text;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace KamatekCrm.Services;

public enum BarcodeLabelTemplate
{
    Thermal50x30,
    Thermal58x40,
    A4Grid24
}

public sealed record BarcodeLabelRequest(
    string ProductName,
    string Sku,
    string Barcode,
    decimal SalePrice,
    string Currency,
    int Quantity,
    BarcodeLabelTemplate Template,
    bool ShowPrice,
    bool ShowCompanyName,
    string CompanyName);

/// <summary>
/// QuestPDF + saf C# vektörel SVG ile barkod etiketi PDF çıktısı üretir.
/// Şablonlar: Termal 50x30 mm, Termal 58x40 mm, A4 24'lü Etiket (3x8).
/// </summary>
public static class BarcodeLabelPdfService
{
    private static readonly CultureInfo TrCulture = CultureInfo.GetCultureInfo("tr-TR");

    public static string BuildDefaultFileName(string? sku, string? productName, int quantity)
    {
        var safeSku = SanitizeSegment(sku, 20);
        var safeName = SanitizeSegment(productName, 40);
        var qty = Math.Clamp(quantity, 1, 100);

        var sb = new StringBuilder("Etiket");
        if (!string.IsNullOrEmpty(safeSku)) sb.Append('_').Append(safeSku);
        if (!string.IsNullOrEmpty(safeName)) sb.Append('_').Append(safeName);
        sb.Append('_').Append(qty.ToString(CultureInfo.InvariantCulture)).Append("Adet.pdf");
        return sb.ToString();
    }

    public static void GeneratePdf(BarcodeLabelRequest request, string filePath)
    {
        var qty = Math.Clamp(request.Quantity, 1, 100);
        var effectiveCode = !string.IsNullOrWhiteSpace(request.Barcode)
            ? request.Barcode.Trim()
            : request.Sku.Trim();
        var barcodeSvg = BarcodeSvgRenderer.RenderSvg(effectiveCode, barHeight: 44, quietZoneModules: 8);
        var priceText = FormatPrice(request.SalePrice, request.Currency);
        var truncatedName = Truncate(request.ProductName, request.Template == BarcodeLabelTemplate.Thermal50x30 ? 34 : 44);
        var companyName = Truncate(request.CompanyName, 32);

        Document.Create(container =>
        {
            if (request.Template == BarcodeLabelTemplate.A4Grid24)
            {
                const int cols = 3;
                const int rows = 8;
                const int perPage = cols * rows;
                var pageCount = (int)Math.Ceiling(qty / (double)perPage);

                for (var pageIndex = 0; pageIndex < pageCount; pageIndex++)
                {
                    var remaining = qty - (pageIndex * perPage);
                    var labelsOnPage = Math.Min(perPage, remaining);

                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.MarginVertical(8, Unit.Millimetre);
                        page.MarginHorizontal(6, Unit.Millimetre);
                        page.DefaultTextStyle(x => x.FontFamily("Segoe UI").FontColor(Colors.Black));

                        page.Content().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                            });

                            for (var i = 0; i < perPage; i++)
                            {
                                var hasLabel = i < labelsOnPage;
                                table.Cell()
                                    .Height(34.5f, Unit.Millimetre)
                                    .Padding(1.5f, Unit.Millimetre)
                                    .Border(hasLabel ? 0.5f : 0f)
                                    .BorderColor(Colors.Grey.Lighten2)
                                    .Padding(2.2f, Unit.Millimetre)
                                    .Element(cell =>
                                    {
                                        if (hasLabel)
                                        {
                                            RenderLabelContent(cell, request, truncatedName, companyName, effectiveCode, barcodeSvg, priceText, isCompact: false);
                                        }
                                    });
                            }
                        });
                    });
                }
            }
            else
            {
                var isCompact = request.Template == BarcodeLabelTemplate.Thermal50x30;
                var widthMm = isCompact ? 50f : 58f;
                var heightMm = isCompact ? 30f : 40f;

                for (var i = 0; i < qty; i++)
                {
                    container.Page(page =>
                    {
                        page.Size(widthMm, heightMm, Unit.Millimetre);
                        page.Margin(isCompact ? 1.8f : 2.5f, Unit.Millimetre);
                        page.DefaultTextStyle(x => x.FontFamily("Segoe UI").FontColor(Colors.Black));

                        page.Content().Element(content =>
                            RenderLabelContent(content, request, truncatedName, companyName, effectiveCode, barcodeSvg, priceText, isCompact));
                    });
                }
            }
        }).GeneratePdf(filePath);
    }

    private static void RenderLabelContent(
        IContainer container,
        BarcodeLabelRequest request,
        string productName,
        string companyName,
        string effectiveCode,
        string barcodeSvg,
        string priceText,
        bool isCompact)
    {
        container.Column(col =>
        {
            col.Spacing(isCompact ? 1.0f : 1.5f);

            if (request.ShowCompanyName && !string.IsNullOrWhiteSpace(companyName))
            {
                col.Item().AlignCenter().Text(companyName)
                    .FontSize(isCompact ? 6.5f : 7.5f)
                    .SemiBold()
                    .FontColor(Colors.Grey.Darken2);
            }

            col.Item().AlignCenter().Text(productName)
                .FontSize(isCompact ? 7.5f : 8.5f)
                .Bold()
                .ClampLines(1, "…");

            col.Item().Row(row =>
            {
                row.RelativeItem().AlignLeft().Text(string.IsNullOrWhiteSpace(request.Sku) ? string.Empty : $"SKU: {request.Sku}")
                    .FontSize(isCompact ? 6.5f : 7.5f)
                    .FontColor(Colors.Grey.Darken3);

                if (request.ShowPrice)
                {
                    row.AutoItem().AlignRight().Text(priceText)
                        .FontSize(isCompact ? 7.5f : 9f)
                        .Bold();
                }
            });

            col.Item()
                .Height(isCompact ? 11.5f : 15f, Unit.Millimetre)
                .AlignCenter()
                .Svg(barcodeSvg);

            col.Item().AlignCenter().Text(effectiveCode)
                .FontSize(isCompact ? 7f : 8f)
                .SemiBold()
                .LetterSpacing(0.08f);
        });
    }

    private static string FormatPrice(decimal price, string? currency)
    {
        var symbol = (currency ?? "TRY").ToUpperInvariant() switch
        {
            "USD" => "$",
            "EUR" => "€",
            _ => "₺"
        };
        return string.Format(TrCulture, "{0}{1:N2}", symbol, price);
    }

    private static string Truncate(string? value, int maxLen)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var trimmed = value.Trim();
        return trimmed.Length <= maxLen ? trimmed : trimmed[..(maxLen - 1)].TrimEnd() + "…";
    }

    private static string SanitizeSegment(string? raw, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(raw.Length);
        var lastUnderscore = false;

        foreach (var ch in raw.Trim())
        {
            if (char.IsWhiteSpace(ch) || ch is '-' or '/' or '\\' or '.' or ',' or ':')
            {
                if (!lastUnderscore && sb.Length > 0)
                {
                    sb.Append('_');
                    lastUnderscore = true;
                }
                continue;
            }
            if (Array.IndexOf(invalid, ch) >= 0 || (!char.IsLetterOrDigit(ch) && ch != '_'))
                continue;

            sb.Append(ch);
            lastUnderscore = false;
        }

        var result = sb.ToString().Trim('_');
        return result.Length > maxLength ? result[..maxLength].TrimEnd('_') : result;
    }
}
