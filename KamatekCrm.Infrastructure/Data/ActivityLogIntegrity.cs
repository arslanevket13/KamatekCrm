using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using KamatekCrm.Shared.Models;

namespace KamatekCrm.Infrastructure.Data;

/// <summary>
/// Denetim kayıtları için sürümlenmiş ve kültürden bağımsız bütünlük mührü üretir.
/// </summary>
public static class ActivityLogIntegrity
{
    public const int CurrentVersion = 1;

    public static void Seal(ActivityLog log)
    {
        ArgumentNullException.ThrowIfNull(log);
        log.IntegrityVersion = CurrentVersion;
        log.IntegrityHash = ComputeHash(log, CurrentVersion);
    }

    public static bool Verify(ActivityLog log)
    {
        ArgumentNullException.ThrowIfNull(log);

        if (log.IntegrityVersion != CurrentVersion || string.IsNullOrWhiteSpace(log.IntegrityHash))
        {
            return false;
        }

        var expectedBytes = Convert.FromHexString(ComputeHash(log, log.IntegrityVersion));
        byte[] actualBytes;
        try
        {
            actualBytes = Convert.FromHexString(log.IntegrityHash);
        }
        catch (FormatException)
        {
            return false;
        }

        return expectedBytes.Length == actualBytes.Length &&
               CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }

    public static string ComputeHash(ActivityLog log, int version = CurrentVersion)
    {
        ArgumentNullException.ThrowIfNull(log);
        if (version != CurrentVersion)
        {
            throw new NotSupportedException($"Desteklenmeyen denetim bütünlük sürümü: {version}");
        }

        var canonical = new StringBuilder(512);
        Append(canonical, version.ToString(CultureInfo.InvariantCulture));
        Append(canonical, log.UserId?.ToString(CultureInfo.InvariantCulture));
        Append(canonical, log.Username);
        Append(canonical, log.ActionType);
        Append(canonical, log.Action);
        Append(canonical, log.EntityName);
        Append(canonical, log.RecordId);
        Append(canonical, log.Description);
        Append(canonical, log.AdditionalData);
        Append(canonical, log.Timestamp.ToUniversalTime().Ticks.ToString(CultureInfo.InvariantCulture));
        Append(canonical, log.ReferenceId);
        Append(canonical, log.DurationMs.ToString(CultureInfo.InvariantCulture));
        Append(canonical, log.IpAddress);
        Append(canonical, log.UserAgent);

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())));
    }

    private static void Append(StringBuilder builder, string? value)
    {
        if (value is null)
        {
            builder.Append("-1:");
            return;
        }

        builder.Append(value.Length.ToString(CultureInfo.InvariantCulture))
            .Append(':')
            .Append(value);
    }
}
