using System.Net.Http.Headers;
using System.Security.Claims;
using KamatekCrm.Web.Features.Auth;
using KamatekCrm.Web.Features.Dashboard;
using KamatekCrm.Web.Features.Customers;
using KamatekCrm.Web.Features.Products;
using KamatekCrm.Web.Features.Jobs;
using KamatekCrm.Web.Features.Sales;
using KamatekCrm.Web.Features.Technician;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Serilog;

// ─── SERILOG EARLY INIT ───
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // ─── 1. SERILOG ───
    builder.Host.UseSerilog((ctx, lc) => lc
        .ReadFrom.Configuration(ctx.Configuration)
        .WriteTo.Console()
        .Enrich.FromLogContext());

    // ─── 2. ORTAM & PORT ───
    builder.WebHost.UseUrls("http://localhost:7000");

    // ─── 3. IIS / REVERSE PROXY ───
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();
    });

    // ─── 4. COOKIE AUTHENTICATION ───
    builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
        .AddCookie(options =>
        {
            options.LoginPath = "/login";
            options.LogoutPath = "/logout";
            options.AccessDeniedPath = "/login";
            options.ExpireTimeSpan = TimeSpan.FromDays(30);
            options.SlidingExpiration = true;
            options.Cookie.Name = "KamatekCrm.Auth";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;

            // HTMX-aware: If HTMX request gets 302, return 401 so HTMX can handle it
            options.Events = new CookieAuthenticationEvents
            {
                OnRedirectToLogin = context =>
                {
                    if (context.Request.Headers.ContainsKey("HX-Request"))
                    {
                        context.Response.StatusCode = 401;
                        context.Response.Headers["HX-Redirect"] = "/login";
                        return Task.CompletedTask;
                    }
                    context.Response.Redirect(context.RedirectUri);
                    return Task.CompletedTask;
                }
            };
        });

    builder.Services.AddAuthorization();

    // ─── 5. ANTIFORGERY ───
    builder.Services.AddAntiforgery(options =>
    {
        options.HeaderName = "X-XSRF-TOKEN";
        options.Cookie.Name = "KamatekCrm.Antiforgery";
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    });

    // ─── 6. HTTP CLIENT → WPF API HOST ───
    builder.Services.AddHttpClient("ApiClient", client =>
    {
        client.BaseAddress = new Uri("http://localhost:5050/");
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    });

    // ═══════════════════════════════════════════
    //  BUILD
    // ═══════════════════════════════════════════
    var app = builder.Build();

    // ─── 7. MIDDLEWARE PIPELINE ───
    app.UseForwardedHeaders();
    app.UseSerilogRequestLogging();

    // Global Exception Handler (HTMX-aware)
    app.Use(async (context, next) =>
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "İşlenmeyen hata: {Path}", context.Request.Path);

            var isHtmx = context.Request.Headers.ContainsKey("HX-Request");
            context.Response.StatusCode = 500;
            context.Response.ContentType = "text/html; charset=utf-8";

            if (isHtmx)
            {
                await context.Response.WriteAsync(
                    """<div class="alert alert-danger" role="alert"><i class="bi bi-exclamation-triangle-fill me-2"></i>Sunucu hatası oluştu. Lütfen tekrar deneyin.</div>""");
            }
            else
            {
                await context.Response.WriteAsync(
                    KamatekCrm.Web.Shared.HtmlTemplates.ErrorPage(
                        "Sunucu Hatası",
                        "Beklenmeyen bir hata oluştu. Lütfen daha sonra tekrar deneyin."));
            }
        }
    });

    app.UseStaticFiles();
    app.UseAuthentication();
    app.UseAuthorization();

    // ─── 8. ROOT REDIRECT ───
    app.MapGet("/", (HttpContext ctx) =>
    {
        if (ctx.User.Identity?.IsAuthenticated == true)
            return Results.Redirect("/dashboard");
        return Results.Redirect("/login");
    }).ExcludeFromDescription();

    // ─── 9. FEATURE ENDPOINTS ───
    AuthEndpoints.Map(app);
    DashboardEndpoints.Map(app);
    CustomersEndpoints.Map(app);
    ProductsEndpoints.Map(app);
    JobsEndpoints.Map(app);
    SalesEndpoints.Map(app);
    TechnicianDashboardEndpoints.Map(app);

    // ─── 10. STARTUP ───
    Log.Information("──► KamatekCRM Web | Port 7000 | Minimal API + HTMX | CSP Strict");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Uygulama başlatılamadı!");
}
finally
{
    Log.CloseAndFlush();
}