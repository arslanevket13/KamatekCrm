using KamatekCrm.ApplicationCore.Common;
using KamatekCrm.Shared.Models;

namespace KamatekCrm.ApplicationCore.Services;

public static class ProjectQuoteLifecyclePolicy
{
    public static Result ValidateTransition(
        QuoteStatus current,
        QuoteStatus target,
        DateTime? validUntil,
        DateTime utcNow,
        string? reason = null)
    {
        if (current == target) return Result.Success();

        var allowed = current switch
        {
            QuoteStatus.Draft => target == QuoteStatus.Sent,
            QuoteStatus.Revised => target == QuoteStatus.Sent,
            QuoteStatus.Sent => target is QuoteStatus.Approved or QuoteStatus.Rejected or QuoteStatus.Expired,
            _ => false
        };
        if (!allowed)
            return Result.Failure($"Teklif durumu '{Display(current)}' iken '{Display(target)}' durumuna geçirilemez.");

        if (target == QuoteStatus.Approved && validUntil.HasValue && validUntil.Value < utcNow)
            return Result.Failure("Geçerlilik süresi dolmuş teklif onaylanamaz; önce yeni revizyon oluşturulmalıdır.");
        if (target == QuoteStatus.Rejected && string.IsNullOrWhiteSpace(reason))
            return Result.Failure("Reddedilen teklif için neden girilmelidir.");
        if (target == QuoteStatus.Expired && (!validUntil.HasValue || validUntil.Value >= utcNow))
            return Result.Failure("Yalnızca geçerlilik tarihi geçmiş teklifler süresi doldu olarak işaretlenebilir.");

        return Result.Success();
    }

    public static string Display(QuoteStatus status) => status switch
    {
        QuoteStatus.Draft => "Taslak",
        QuoteStatus.Sent => "Gönderildi",
        QuoteStatus.Approved => "Onaylandı",
        QuoteStatus.Rejected => "Reddedildi",
        QuoteStatus.Expired => "Süresi Doldu",
        QuoteStatus.Revised => "Revize",
        _ => status.ToString()
    };
}
