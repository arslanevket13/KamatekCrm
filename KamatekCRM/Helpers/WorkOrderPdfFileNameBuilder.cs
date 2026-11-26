using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using KamatekCrm.ApplicationCore.DTOs.ServiceJobs;

namespace KamatekCrm.Helpers;

/// <summary>
/// İş emri PDF dosyaları için temizlenmiş, açıklayıcı varsayılan dosya adı üretir:
/// IsEmri_{JobCode}_{CustomerName}_{City}_{TitleOrType}_{yyyyMMdd}.pdf
/// </summary>
public static class WorkOrderPdfFileNameBuilder
{
    private const int MaxFileNameLength = 120;
    private const string PdfExtension = ".pdf";

    public static string Build(
        ServiceJobDocumentDto job,
        string? titleOrType = null,
        string? jobCode = null,
        string? city = null,
        DateTime? dateUtc = null)
    {
        var resolvedCustomer = !string.IsNullOrWhiteSpace(job.CustomerName)
            ? job.CustomerName
            : job.CustomerCompanyName;
        var resolvedCity = !string.IsNullOrWhiteSpace(city)
            ? city
            : ExtractCityFromAddress(job.CustomerAddress);
        var resolvedTitle = !string.IsNullOrWhiteSpace(titleOrType)
            ? titleOrType
            : job.WorkOrderType.ToString();

        return Build(job.Id, jobCode, resolvedCustomer, resolvedCity, resolvedTitle, dateUtc);
    }

    public static string Build(
        int jobId,
        string? jobCode,
        string? customerName,
        string? city,
        string? titleOrType,
        DateTime? dateUtc = null)
    {
        var dateSegment = (dateUtc ?? DateTime.UtcNow).ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var codeSegment = SanitizeSegment(string.IsNullOrWhiteSpace(jobCode)
            ? jobId.ToString("D6", CultureInfo.InvariantCulture)
            : jobCode);
        if (string.IsNullOrEmpty(codeSegment))
        {
            codeSegment = jobId.ToString("D6", CultureInfo.InvariantCulture);
        }

        var middleSegments = new List<string>();
        AddIfNotEmpty(middleSegments, SanitizeSegment(customerName, maxLength: 36));
        AddIfNotEmpty(middleSegments, SanitizeSegment(city, maxLength: 24));
        AddIfNotEmpty(middleSegments, SanitizeSegment(titleOrType, maxLength: 32));

        var prefix = $"IsEmri_{codeSegment}";
        var suffix = $"_{dateSegment}{PdfExtension}";
        var maxMiddleLength = MaxFileNameLength - prefix.Length - suffix.Length;

        var middle = middleSegments.Count > 0 ? "_" + string.Join("_", middleSegments) : string.Empty;
        if (middle.Length > maxMiddleLength && maxMiddleLength > 0)
        {
            middle = middle[..maxMiddleLength].TrimEnd('_');
        }
        else if (maxMiddleLength <= 0)
        {
            middle = string.Empty;
        }

        return $"{prefix}{middle}{suffix}";
    }

    private static void AddIfNotEmpty(List<string> list, string value)
    {
        if (!string.IsNullOrEmpty(value))
            list.Add(value);
    }

    private static string ExtractCityFromAddress(string? address)
    {
        if (string.IsNullOrWhiteSpace(address)) return string.Empty;

        var trimmed = address.Trim();
        var slashParts = trimmed.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (slashParts.Length >= 2)
        {
            var last = slashParts[^1];
            if (last.Length is >= 2 and <= 24) return last;
        }

        var commaParts = trimmed.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (commaParts.Length >= 2)
        {
            var last = commaParts[^1];
            if (last.Length is >= 2 and <= 24) return last;
        }

        return trimmed.Length <= 24 ? trimmed : string.Empty;
    }

    private static string SanitizeSegment(string? raw, int maxLength = 40)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

        var invalidChars = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(raw.Length);
        var lastWasUnderscore = false;

        foreach (var ch in raw.Trim())
        {
            if (char.IsWhiteSpace(ch) || ch is '-' or '/' or '\\' or '.' or ',' or ':' or ';')
            {
                if (!lastWasUnderscore && sb.Length > 0)
                {
                    sb.Append('_');
                    lastWasUnderscore = true;
                }
                continue;
            }

            if (Array.IndexOf(invalidChars, ch) >= 0 || !char.IsLetterOrDigit(ch) && ch != '_')
            {
                continue;
            }

            if (ch == '_')
            {
                if (!lastWasUnderscore && sb.Length > 0)
                {
                    sb.Append('_');
                    lastWasUnderscore = true;
                }
                continue;
            }

            sb.Append(ch);
            lastWasUnderscore = false;
        }

        var result = sb.ToString().Trim('_');
        if (result.Length > maxLength)
        {
            result = result[..maxLength].TrimEnd('_');
        }
        return result;
    }
}
