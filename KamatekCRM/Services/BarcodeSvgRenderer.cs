using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using KamatekCrm.Shared.Helpers;

namespace KamatekCrm.Services;

/// <summary>
/// Harici yerel DLL bağımlılığı olmadan saf C# ile taranabilir vektörel
/// EAN-13 ve Code 128B SVG barkod üreteci (QuestPDF .Svg() ile uyumlu).
/// </summary>
public static class BarcodeSvgRenderer
{
    // EAN-13 L, G, R kod tabloları (her biri 7 modül)
    private static readonly string[] EanL =
    [
        "0001101", "0011001", "0010011", "0111101", "0100011",
        "0110001", "0101111", "0111011", "0110111", "0001011"
    ];

    private static readonly string[] EanG =
    [
        "0100111", "0110011", "0011011", "0100001", "0011101",
        "0111001", "0000101", "0010001", "0001001", "0010111"
    ];

    private static readonly string[] EanR =
    [
        "1110010", "1100110", "1101100", "1000010", "1011100",
        "1001110", "1010000", "1000100", "1001000", "1110100"
    ];

    // İlk basamağa göre sol 6 hanenin L/G parite deseni
    private static readonly string[] EanParity =
    [
        "LLLLLL", "LLGLGG", "LLGGLG", "LLGGGL", "LGLLGG",
        "LGGLLG", "LGGGLL", "LGLGLG", "LGLGGL", "LGGLGL"
    ];

    // Code 128 0..106 desen tablosu (11 modül; Stop=106 13 modül)
    private static readonly string[] Code128Patterns =
    [
        "11011001100", "11001101100", "11001100110", "10010011000", "10010001100",
        "10001001100", "10011001000", "10011000100", "10001100100", "11001001000",
        "11001000100", "11000100100", "10110011100", "10011011100", "10011001110",
        "10111001100", "10011101100", "10011100110", "11001110010", "11001011100",
        "11001001110", "11011100100", "11001110100", "11101101110", "11101001100",
        "11100101100", "11100100110", "11101100100", "11100110100", "11100110010",
        "11011011000", "11011000110", "11000110110", "10100011000", "10001011000",
        "10001000110", "10110001000", "10001101000", "10001100010", "11010001000",
        "11000101000", "11000100010", "10110111000", "10110001110", "10001101110",
        "10111011000", "10111000110", "10001110110", "11101110110", "11010001110",
        "11000101110", "11011101000", "11011100010", "11011101110", "11101011000",
        "11101000110", "11100010110", "11101101000", "11101100010", "11100011010",
        "11101111010", "11001000010", "11110001010", "10100110000", "10100001100",
        "10010110000", "10010000110", "10000101100", "10000100110", "10110010000",
        "10110000100", "10011010000", "10011000010", "10000110100", "10000110010",
        "11000010010", "11001010000", "11110111010", "11000010100", "10001111010",
        "10100111100", "10010111100", "10010011110", "10111100100", "10011110100",
        "10011110010", "11110100100", "11110010100", "11110010010", "11011011110",
        "11011110110", "11110110110", "10101111000", "10100011110", "10001011110",
        "10111101000", "10111100010", "11110101000", "11110100010", "10111011110",
        "10111101110", "11101011110", "11110101110", "11010000100", "11010010000",
        "11010011100", "1100011101011"
    ];

    /// <summary>
    /// Verilen değeri geçerli EAN-13 ise EAN-13 olarak, değilse Code 128B olarak vektörel SVG'ye çevirir.
    /// </summary>
    public static string RenderSvg(string value, int barHeight = 40, int quietZoneModules = 8)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(trimmed))
            trimmed = "0000000000000";

        var bits = ProductCodeHelper.IsValidEan13(trimmed)
            ? EncodeEan13(trimmed)
            : EncodeCode128B(trimmed);

        var totalModules = bits.Length + (quietZoneModules * 2);
        var sb = new StringBuilder(bits.Length * 18);
        sb.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 ");
        sb.Append(totalModules.ToString(CultureInfo.InvariantCulture));
        sb.Append(' ');
        sb.Append(barHeight.ToString(CultureInfo.InvariantCulture));
        sb.Append("\" preserveAspectRatio=\"none\">");
        sb.Append("<rect width=\"100%\" height=\"100%\" fill=\"#FFFFFF\"/>");

        var i = 0;
        while (i < bits.Length)
        {
            if (bits[i] != '1')
            {
                i++;
                continue;
            }

            var start = i;
            while (i < bits.Length && bits[i] == '1') i++;
            var width = i - start;
            var x = quietZoneModules + start;
            sb.Append("<rect x=\"");
            sb.Append(x.ToString(CultureInfo.InvariantCulture));
            sb.Append("\" y=\"0\" width=\"");
            sb.Append(width.ToString(CultureInfo.InvariantCulture));
            sb.Append("\" height=\"");
            sb.Append(barHeight.ToString(CultureInfo.InvariantCulture));
            sb.Append("\" fill=\"#000000\"/>");
        }

        sb.Append("</svg>");
        return sb.ToString();
    }

    private static string EncodeEan13(string digits)
    {
        var first = digits[0] - '0';
        var parity = EanParity[first];
        var sb = new StringBuilder(95);
        sb.Append("101"); // Sol koruma

        for (var i = 0; i < 6; i++)
        {
            var d = digits[i + 1] - '0';
            sb.Append(parity[i] == 'L' ? EanL[d] : EanG[d]);
        }

        sb.Append("01010"); // Orta koruma

        for (var i = 0; i < 6; i++)
        {
            var d = digits[i + 7] - '0';
            sb.Append(EanR[d]);
        }

        sb.Append("101"); // Sağ koruma
        return sb.ToString();
    }

    private static string EncodeCode128B(string text)
    {
        // ASCII 32..126 dışındaki karakterleri güvenli ASCII karşılığa indirge
        var codes = new List<int>(text.Length + 3) { 104 }; // Start Code B
        var checksum = 104;
        var position = 1;

        foreach (var ch in text)
        {
            var ascii = ch switch
            {
                'Ç' => 'C', 'ç' => 'c',
                'Ğ' => 'G', 'ğ' => 'g',
                'İ' => 'I', 'ı' => 'i',
                'Ö' => 'O', 'ö' => 'o',
                'Ş' => 'S', 'ş' => 's',
                'Ü' => 'U', 'ü' => 'u',
                _ => ch
            };

            if (ascii < 32 || ascii > 126) continue;
            var code = ascii - 32;
            codes.Add(code);
            checksum += code * position;
            position++;
        }

        if (codes.Count == 1)
        {
            codes.Add('0' - 32);
            checksum += ('0' - 32) * position;
        }

        codes.Add(checksum % 103);
        codes.Add(106); // Stop

        var sb = new StringBuilder(codes.Count * 11 + 2);
        foreach (var code in codes)
        {
            sb.Append(Code128Patterns[code]);
        }
        return sb.ToString();
    }
}
