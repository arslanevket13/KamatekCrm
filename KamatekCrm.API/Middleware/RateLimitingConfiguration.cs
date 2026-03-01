using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Serilog;

namespace KamatekCrm.API.Middleware
{
    /// <summary>
    /// Rate Limiting konfigürasyonu.
    /// Strateji:
    ///   - Global: 100 istek / 10 saniye (sliding window)
    ///   - Auth endpoints: 10 istek / dakika (brute-force koruması)
    ///   - Dashboard: 30 istek / dakika (ağır sorgular)
    /// </summary>
    public static class RateLimitingConfiguration
    {
        public const string GlobalPolicy = "global";
        public const string AuthPolicy = "auth";
        public const string HeavyQueryPolicy = "heavy";

        public static IServiceCollection AddRateLimiting(this IServiceCollection services)
        {
            services.AddRateLimiter(options =>
            {
                // 429 Too Many Requests cevabı
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.OnRejected = async (context, cancellationToken) =>
                {
                    var clientIp = context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                    Log.Warning("Rate limit exceeded for {ClientIp} on {Path}",
                        clientIp, context.HttpContext.Request.Path);

                    context.HttpContext.Response.ContentType = "application/json";
                    await context.HttpContext.Response.WriteAsync(
                        "{\"success\":false,\"error\":{\"type\":\"RateLimit\",\"message\":\"Çok fazla istek gönderildi. Lütfen biraz bekleyin.\"}}",
                        cancellationToken);
                };

                // Global Policy: 100 istek / 10 saniye sliding window
                options.AddSlidingWindowLimiter(GlobalPolicy, opt =>
                {
                    opt.PermitLimit = 100;
                    opt.Window = TimeSpan.FromSeconds(10);
                    opt.SegmentsPerWindow = 2;
                    opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                    opt.QueueLimit = 5;
                });

                // Auth Policy: 10 istek / dakika (brute-force protection)
                options.AddFixedWindowLimiter(AuthPolicy, opt =>
                {
                    opt.PermitLimit = 10;
                    opt.Window = TimeSpan.FromMinutes(1);
                    opt.QueueLimit = 0; // Kuyruk yok — hemen reddet
                });

                // Heavy Query Policy: 30 istek / dakika (Dashboard gibi ağır sorgular)
                options.AddTokenBucketLimiter(HeavyQueryPolicy, opt =>
                {
                    opt.TokenLimit = 30;
                    opt.ReplenishmentPeriod = TimeSpan.FromSeconds(2);
                    opt.TokensPerPeriod = 1;
                    opt.QueueLimit = 2;
                });
            });

            return services;
        }
    }
}
