using System.Net.Http.Json;
using System.Security.Claims;
using KamatekCrm.Shared.DTOs;
using KamatekCrm.Web.Shared;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace KamatekCrm.Web.Features.Auth;

/// <summary>
/// Kimlik doğrulama endpoint'leri: Login (GET/POST) ve Logout (POST).
/// HTMX form submission ile çalışır, JSON yerine HTML döner.
/// </summary>
public static class AuthEndpoints
{
    public static void Map(WebApplication app)
    {
        // ─── GET /login ───
        app.MapGet("/login", (HttpContext ctx, IAntiforgery antiforgery) =>
        {
            // Zaten giriş yapmışsa dashboard'a yönlendir
            if (ctx.User.Identity?.IsAuthenticated == true)
                return Results.Redirect("/dashboard");

            var tokens = antiforgery.GetAndStoreTokens(ctx);
            var html = HtmlTemplates.LoginPage(antiforgeryToken: tokens.RequestToken);
            return Results.Content(html, "text/html; charset=utf-8");
        })
        .AllowAnonymous();

        // ─── POST /login ───
        app.MapPost("/login", async (
            HttpContext ctx,
            IHttpClientFactory httpClientFactory,
            IAntiforgery antiforgery,
            ILogger<Program> logger) =>
        {
            // Antiforgery doğrulama
            try
            {
                await antiforgery.ValidateRequestAsync(ctx);
            }
            catch (AntiforgeryValidationException)
            {
                logger.LogWarning("Antiforgery token doğrulama başarısız: {IP}",
                    ctx.Connection.RemoteIpAddress);
                return Results.Content(
                    ErrorSnippet("Güvenlik doğrulaması başarısız. Sayfayı yenileyip tekrar deneyin."),
                    "text/html; charset=utf-8",
                    statusCode: 400);
            }

            // Form verilerini oku - güvenli şekilde
            var form = await ctx.Request.ReadFormAsync();
            if (!form.TryGetValue("username", out var usernameForm) ||
                !form.TryGetValue("password", out var passwordForm))
            {
                return Results.Content(
                    ErrorSnippet("Kullanıcı adı ve şifre gereklidir."),
                    "text/html; charset=utf-8",
                    statusCode: 400);
            }

            var username = usernameForm.ToString().Trim();
            var password = passwordForm.ToString();

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                return Results.Content(
                    ErrorSnippet("Kullanıcı adı ve şifre gereklidir."),
                    "text/html; charset=utf-8",
                    statusCode: 400);
            }

            // Input validation - uzunluk kontrolü
            if (username.Length < 3 || username.Length > 50)
            {
                return Results.Content(
                    ErrorSnippet("Kullanıcı adı 3-50 karakter arasında olmalıdır."),
                    "text/html; charset=utf-8",
                    statusCode: 400);
            }

            // WPF API'ye login isteği
            try
            {
                var client = httpClientFactory.CreateClient("ApiClient");
                var apiResponse = await client.PostAsJsonAsync("api/auth/login", new LoginRequestDto
                {
                    Username = username,
                    Password = password
                });

                if (!apiResponse.IsSuccessStatusCode)
                {
                    logger.LogWarning("Login başarısız: {Username} - API Status: {Status}",
                        username, apiResponse.StatusCode);
                    ctx.Response.TriggerToast("Kullanıcı adı veya şifre hatalı.", ToastType.Error);
                    return Results.Content(
                        ErrorSnippet("Kullanıcı adı veya şifre hatalı."),
                        "text/html; charset=utf-8",
                        statusCode: 401);
                }

                var loginResult = await apiResponse.Content.ReadFromJsonAsync<LoginResponseDto>();
                if (loginResult is null)
                {
                    return Results.Content(
                        ErrorSnippet("Sunucudan geçersiz yanıt alındı."),
                        "text/html; charset=utf-8",
                        statusCode: 500);
                }

                // Cookie Authentication — Claims oluştur
                var claims = new List<Claim>
                {
                    new(ClaimTypes.Name, loginResult.FullName ?? username),
                    new(ClaimTypes.NameIdentifier, loginResult.UserId.ToString()),
                    new(ClaimTypes.Role, loginResult.Role ?? "User"),
                    new("Username", loginResult.Username ?? username)
                };

                var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var principal = new ClaimsPrincipal(identity);

                await ctx.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    principal,
                    new AuthenticationProperties
                    {
                        IsPersistent = true,
                        ExpiresUtc = DateTimeOffset.UtcNow.AddDays(30)
                    });

                logger.LogInformation("Login başarılı: {Username} ({FullName})",
                    username, loginResult.FullName);

                // HTMX: HX-Redirect başlığı ile yönlendir (sayfa yeniden yüklenir)
                ctx.Response.Headers["HX-Redirect"] = "/dashboard";
                return Results.Ok();
            }
            catch (HttpRequestException ex)
            {
                logger.LogError(ex, "API bağlantı hatası (Port 5050)");
                ctx.Response.TriggerToast("Sunucuya bağlanılamadı!", ToastType.Error);
                return Results.Content(
                    ErrorSnippet("Sunucuya bağlanılamadı. API Host (Port 5050) çalışıyor mu kontrol edin."),
                    "text/html; charset=utf-8",
                    statusCode: 503);
            }
        })
        .AllowAnonymous();

        // ─── POST /logout ───
        app.MapPost("/logout", async (HttpContext ctx) =>
        {
            await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            if (ctx.Request.Headers.ContainsKey("HX-Request"))
            {
                ctx.Response.Headers["HX-Redirect"] = "/login";
                return Results.Ok();
            }

            return Results.Redirect("/login");
        })
        .RequireAuthorization();
    }

    /// <summary>
    /// HTMX tarafından #error-container içine swap edilen hata snippet'i.
    /// </summary>
    private static string ErrorSnippet(string message) =>
        $"""<div class="alert alert-danger d-flex align-items-center" role="alert"><i class="bi bi-exclamation-triangle-fill me-2"></i>{message}</div>""";
}
