using System.Text.Json;

namespace KamatekCrm.Web.Shared;

/// <summary>
/// HTMX için yardımcı extension method'lar.
/// HX-Trigger header ile sunucu tarafından toast bildirimi tetikleme.
///
/// ⚠️ KRITIK NOKTALAR (Yaygın Hatalar):
/// 1. HX-Trigger JSON'da event adı KEY, veri VALUE olmalı: {"showToast": {...}}
/// 2. HTMX, event adını document.body üzerinde dispatch eder
/// 3. JSON.stringify DEĞİL, doğrudan obje geçilir (HTMX zaten JSON parse eder)
/// 4. System.Text.Json ile safe serialization kullanılır (XSS önlemi)
/// </summary>
public static class HtmxExtensions
{
    /// <summary>
    /// Response'a HX-Trigger header ekleyerek client-side toast notification tetikler.
    /// HTMX bu header'ı alınca "showToast" event'ini document.body üzerinde ateşler.
    /// </summary>
    public static void TriggerToast(this HttpResponse response, string message, ToastType type = ToastType.Info)
    {
        // HTMX HX-Trigger format: {"eventName": {data}}
        // HTMX otomatik olarak JSON parse eder ve evt.detail içine koyar
        var payload = new
        {
            showToast = new
            {
                message,
                type = type.ToString().ToLowerInvariant()
            }
        };

        // System.Text.Json ile güvenli serialization (XSS-safe)
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        response.Headers["HX-Trigger"] = json;
    }

    /// <summary>
    /// Mevcut HX-Trigger header'ı varsa, toast event'ini ekler (birden fazla event desteği).
    /// HTMX birden fazla event'i aynı HX-Trigger altında destekler.
    /// </summary>
    public static void TriggerToastAfterSwap(this HttpResponse response, string message, ToastType type = ToastType.Info)
    {
        var payload = new
        {
            showToast = new
            {
                message,
                type = type.ToString().ToLowerInvariant()
            }
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        // HX-Trigger-After-Swap: swap tamamlandıktan SONRA tetiklenir
        response.Headers["HX-Trigger-After-Swap"] = json;
    }
}

/// <summary>
/// Toast bildirim tipleri. Bootstrap alert renkleri ile eşleşir.
/// </summary>
public enum ToastType
{
    Success,
    Error,
    Warning,
    Info
}
