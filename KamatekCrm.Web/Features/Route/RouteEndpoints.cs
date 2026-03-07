using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using KamatekCrm.Web.Shared;
using Microsoft.AspNetCore.Antiforgery;

namespace KamatekCrm.Web.Features.Route;

public static class RouteEndpoints
{
    public static void Map(WebApplication app)
    {
        // GET /technician/route — Rota planı sayfası
        app.MapGet("/technician/route", async (
            HttpContext ctx,
            IHttpClientFactory httpClientFactory,
            IAntiforgery antiforgery,
            string? date = null) =>
        {
            var userName = ctx.User.FindFirst(ClaimTypes.Name)?.Value ?? "Teknisyen";
            var userIdStr = ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userId = int.TryParse(userIdStr, out var uid) ? uid : 0;
            var tokens = antiforgery.GetAndStoreTokens(ctx);
            var targetDate = string.IsNullOrEmpty(date) ? DateTime.Today : DateTime.Parse(date);

            var routePoints = new List<RoutePointDto>();
            try
            {
                var client = httpClientFactory.CreateClient("ApiClient");
                var response = await client.GetAsync($"api/location/route-plan/{userId}?date={targetDate:yyyy-MM-dd}");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var apiResponse = JsonSerializer.Deserialize<ApiRouteResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    routePoints = apiResponse?.Data ?? new List<RoutePointDto>();
                }
            }
            catch { /* API unavailable, show empty route */ }

            var html = HtmlTemplates.RoutePlanningPage(routePoints, targetDate, userName, userId, tokens.RequestToken);
            return Results.Content(html, "text/html; charset=utf-8");
        })
        .RequireAuthorization();

        // POST /technician/route/visit/{pointId} — HTMX ile noktayı ziyaret edildi işaretle
        app.MapPost("/technician/route/visit/{pointId}", async (
            HttpContext ctx,
            IHttpClientFactory httpClientFactory,
            IAntiforgery antiforgery,
            int pointId) =>
        {
            try { await antiforgery.ValidateRequestAsync(ctx); }
            catch { return Results.BadRequest("Token hatası"); }

            try
            {
                var client = httpClientFactory.CreateClient("ApiClient");
                var response = await client.PutAsync($"api/location/route-plan/point/{pointId}/visit", null);
                if (response.IsSuccessStatusCode)
                {
                    return Results.Content("""
                        <span class="badge bg-success"><i class="bi bi-check-circle me-1"></i>Ziyaret Edildi</span>
                    """, "text/html; charset=utf-8");
                }
                return Results.Content("""<span class="badge bg-warning">Hata</span>""", "text/html; charset=utf-8");
            }
            catch
            {
                return Results.Content("""<span class="badge bg-danger">Sunucu Hatası</span>""", "text/html; charset=utf-8");
            }
        })
        .RequireAuthorization();
    }
}

// API yanıt modelleri
public class ApiRouteResponse
{
    public bool Success { get; set; }
    public List<RoutePointDto>? Data { get; set; }
}

public class RoutePointDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int? ServiceJobId { get; set; }
    public int? CustomerId { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string Address { get; set; } = "";
    public int OrderIndex { get; set; }
    public DateTime? EstimatedArrival { get; set; }
    public DateTime? ActualArrival { get; set; }
    public bool IsVisited { get; set; }
    public DateTime Date { get; set; }
    public string? JobTitle { get; set; }
    public int? JobStatus { get; set; }
    public int? JobPriority { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
}
