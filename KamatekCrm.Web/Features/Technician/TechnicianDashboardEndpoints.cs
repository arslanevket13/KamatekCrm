using System.Net.Http.Json;
using System.Security.Claims;
using KamatekCrm.Web.Services;
using KamatekCrm.Web.Shared;
using Microsoft.AspNetCore.Antiforgery;

namespace KamatekCrm.Web.Features.Technician;

public static class TechnicianDashboardEndpoints
{
    public static void Map(WebApplication app)
    {
        // GET /technician - Yönlendirme
        app.MapGet("/technician", () => Results.Redirect("/technician/dashboard"));

        // GET /technician/dashboard - Teknisyen ana paneli
        app.MapGet("/technician/dashboard", async (
            HttpContext ctx,
            IHttpClientFactory httpClientFactory,
            IAntiforgery antiforgery) =>
        {
            var userName = ctx.User.FindFirst(ClaimTypes.Name)?.Value ?? "Teknisyen";
            var userId = ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var role = ctx.User.FindFirst(ClaimTypes.Role)?.Value ?? "Technician";
            var tokens = antiforgery.GetAndStoreTokens(ctx);

            try
            {
                var client = httpClientFactory.CreateClient("ApiClient");
                
                // Bugünkü işler
                var todayResponse = await client.GetAsync("api/tasks/today");
                var todayJobs = todayResponse.IsSuccessStatusCode 
                    ? await todayResponse.Content.ReadFromJsonAsync<List<JobListItem>>() 
                    : new List<JobListItem>();

                // Bekleyen işler
                var pendingResponse = await client.GetAsync("api/tasks?status=Bekliyor&pageSize=10");
                var pendingJobs = pendingResponse.IsSuccessStatusCode 
                    ? await pendingResponse.Content.ReadFromJsonAsync<List<JobListItem>>() 
                    : new List<JobListItem>();

                // İstatistikler
                var statsResponse = await client.GetAsync("api/dashboard/stats");
                var stats = statsResponse.IsSuccessStatusCode 
                    ? await statsResponse.Content.ReadFromJsonAsync<DashboardStats>() 
                    : new DashboardStats();

                var html = HtmlTemplates.TechnicianDashboard(userName, role, todayJobs, pendingJobs, stats, tokens.RequestToken);
                return Results.Content(html, "text/html; charset=utf-8");
            }
            catch
            {
                var html = HtmlTemplates.TechnicianDashboard(userName, role, new List<JobListItem>(), new List<JobListItem>(), new DashboardStats(), tokens.RequestToken);
                return Results.Content(html, "text/html; charset=utf-8");
            }
        })
        .RequireAuthorization();

        // GET /technician/schedule - Günlük program
        app.MapGet("/technician/schedule", async (
            HttpContext ctx,
            IHttpClientFactory httpClientFactory,
            IAntiforgery antiforgery,
            string? date = null) =>
        {
            var userName = ctx.User.FindFirst(ClaimTypes.Name)?.Value ?? "Teknisyen";
            var tokens = antiforgery.GetAndStoreTokens(ctx);
            var targetDate = string.IsNullOrEmpty(date) ? DateTime.Today : DateTime.Parse(date);

            try
            {
                var client = httpClientFactory.CreateClient("ApiClient");
                var response = await client.GetAsync($"api/tasks/schedule?date={targetDate:yyyy-MM-dd}");
                var jobs = response.IsSuccessStatusCode 
                    ? await response.Content.ReadFromJsonAsync<List<JobListItem>>() 
                    : new List<JobListItem>();

                var html = HtmlTemplates.SchedulePage(jobs, targetDate, userName, tokens.RequestToken);
                return Results.Content(html, "text/html; charset=utf-8");
            }
            catch
            {
                var html = HtmlTemplates.SchedulePage(new List<JobListItem>(), targetDate, userName, tokens.RequestToken);
                return Results.Content(html, "text/html; charset=utf-8");
            }
        })
        .RequireAuthorization();

        // GET /technician/profile - Teknisyen profil
        app.MapGet("/technician/profile", (
            HttpContext ctx,
            IAntiforgery antiforgery) =>
        {
            var userName = ctx.User.FindFirst(ClaimTypes.Name)?.Value ?? "Teknisyen";
            var role = ctx.User.FindFirst(ClaimTypes.Role)?.Value ?? "Technician";
            var username = ctx.User.FindFirst("Username")?.Value ?? userName;
            var tokens = antiforgery.GetAndStoreTokens(ctx);

            var html = HtmlTemplates.TechnicianProfile(userName, role, username, tokens.RequestToken);
            return Results.Content(html, "text/html; charset=utf-8");
        })
        .RequireAuthorization();
    }
}
