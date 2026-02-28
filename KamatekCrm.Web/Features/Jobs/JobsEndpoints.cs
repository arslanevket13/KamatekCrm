using System.Net.Http.Json;
using System.Security.Claims;
using KamatekCrm.Web.Services;
using KamatekCrm.Web.Shared;
using Microsoft.AspNetCore.Antiforgery;

namespace KamatekCrm.Web.Features.Jobs;

public static class JobsEndpoints
{
    public static void Map(WebApplication app)
    {
        // GET /jobs - İş emirleri listesi
        app.MapGet("/jobs", async (
            HttpContext ctx,
            IHttpClientFactory httpClientFactory,
            IAntiforgery antiforgery,
            string? status = null,
            string? search = null,
            int page = 1) =>
        {
            var userName = ctx.User.FindFirst(ClaimTypes.Name)?.Value ?? "Kullanıcı";
            var tokens = antiforgery.GetAndStoreTokens(ctx);

            try
            {
                var client = httpClientFactory.CreateClient("ApiClient");
                var url = $"api/tasks?page={page}&pageSize=20";
                if (!string.IsNullOrEmpty(search)) url += $"&search={Uri.EscapeDataString(search)}";
                if (!string.IsNullOrEmpty(status)) url += $"&status={status}";
                
                var response = await client.GetAsync(url);
                var jobs = response.IsSuccessStatusCode 
                    ? await response.Content.ReadFromJsonAsync<List<JobListItem>>() 
                    : new List<JobListItem>();

                var totalStr = response.Headers.FirstOrDefault(h => h.Key == "X-Total-Count").Value?.FirstOrDefault();
                var total = int.TryParse(totalStr, out var t) ? t : 0;

                var html = HtmlTemplates.JobsPage(jobs ?? [], total, page, userName, tokens.RequestToken, status, search);
                return Results.Content(html, "text/html; charset=utf-8");
            }
            catch
            {
                var html = HtmlTemplates.JobsPage([], 0, 1, userName, tokens.RequestToken, status, search);
                return Results.Content(html, "text/html; charset=utf-8");
            }
        })
        .RequireAuthorization();

        // GET /jobs/new - Yeni iş emri formu
        app.MapGet("/jobs/new", (
            HttpContext ctx,
            IAntiforgery antiforgery) =>
        {
            var tokens = antiforgery.GetAndStoreTokens(ctx);
            var html = HtmlTemplates.JobForm(null, tokens.RequestToken);
            return Results.Content(html, "text/html; charset=utf-8");
        })
        .RequireAuthorization();

        // GET /jobs/{id} - İş emri detayı
        app.MapGet("/jobs/{id}", async (
            int id,
            HttpContext ctx,
            IHttpClientFactory httpClientFactory,
            IAntiforgery antiforgery) =>
        {
            var tokens = antiforgery.GetAndStoreTokens(ctx);
            try
            {
                var client = httpClientFactory.CreateClient("ApiClient");
                var job = await client.GetFromJsonAsync<JobListItem>($"api/tasks/{id}");
                var html = HtmlTemplates.JobForm(job, tokens.RequestToken);
                return Results.Content(html, "text/html; charset=utf-8");
            }
            catch
            {
                return Results.Redirect("/jobs");
            }
        })
        .RequireAuthorization();

        // POST /jobs - Yeni iş emri oluştur
        app.MapPost("/jobs", async (
            HttpContext ctx,
            IHttpClientFactory httpClientFactory,
            IAntiforgery antiforgery) =>
        {
            try { await antiforgery.ValidateRequestAsync(ctx); }
            catch { return Results.BadRequest("Token hatası"); }

            var form = await ctx.Request.ReadFormAsync();
            var job = new
            {
                Title = form["Title"].ToString(),
                Description = form["Description"].ToString(),
                Status = form["Status"].ToString(),
                Priority = form["Priority"].ToString(),
                ScheduledDate = string.IsNullOrEmpty(form["ScheduledDate"].ToString()) ? (DateTime?)null : DateTime.Parse(form["ScheduledDate"].ToString()!),
                CustomerId = string.IsNullOrEmpty(form["CustomerId"].ToString()) ? (int?)null : int.Parse(form["CustomerId"].ToString()!)
            };

            try
            {
                var client = httpClientFactory.CreateClient("ApiClient");
                var response = await client.PostAsJsonAsync("api/tasks", job);
                if (response.IsSuccessStatusCode)
                {
                    ctx.Response.Headers["HX-Redirect"] = "/jobs";
                    return Results.Ok();
                }
                return Results.BadRequest("İş emri oluşturulamadı");
            }
            catch
            {
                return Results.BadRequest("Sunucu hatası");
            }
        })
        .RequireAuthorization();

        // PUT /jobs/{id} - İş emri güncelle
        app.MapPut("/jobs/{id}", async (
            int id,
            HttpContext ctx,
            IHttpClientFactory httpClientFactory,
            IAntiforgery antiforgery) =>
        {
            try { await antiforgery.ValidateRequestAsync(ctx); }
            catch { return Results.BadRequest("Token hatası"); }

            var form = await ctx.Request.ReadFormAsync();
            var job = new
            {
                Id = id,
                Title = form["Title"].ToString(),
                Description = form["Description"].ToString(),
                Status = form["Status"].ToString(),
                Priority = form["Priority"].ToString(),
                ScheduledDate = string.IsNullOrEmpty(form["ScheduledDate"].ToString()) ? (DateTime?)null : DateTime.Parse(form["ScheduledDate"].ToString()!)
            };

            try
            {
                var client = httpClientFactory.CreateClient("ApiClient");
                var response = await client.PutAsJsonAsync($"api/tasks/{id}", job);
                if (response.IsSuccessStatusCode)
                {
                    ctx.Response.Headers["HX-Redirect"] = "/jobs";
                    return Results.Ok();
                }
                return Results.BadRequest("Güncelleme başarısız");
            }
            catch
            {
                return Results.BadRequest("Sunucu hatası");
            }
        })
        .RequireAuthorization();

        // PATCH /jobs/{id}/status - İş emri durumu güncelle
        app.MapPatch("/jobs/{id}/status", async (
            int id,
            HttpContext ctx,
            IHttpClientFactory httpClientFactory,
            IAntiforgery antiforgery) =>
        {
            try { await antiforgery.ValidateRequestAsync(ctx); }
            catch { return Results.BadRequest("Token hatası"); }

            var form = await ctx.Request.ReadFormAsync();
            var status = form["Status"].ToString();

            try
            {
                var client = httpClientFactory.CreateClient("ApiClient");
                var response = await client.PatchAsJsonAsync($"api/tasks/{id}/status", new { Status = status });
                return Results.Ok(new { success = response.IsSuccessStatusCode });
            }
            catch
            {
                return Results.BadRequest(new { success = false, message = "Sunucu hatası" });
            }
        })
        .RequireAuthorization();

        // DELETE /jobs/{id}
        app.MapDelete("/jobs/{id}", async (
            int id,
            HttpContext ctx,
            IHttpClientFactory httpClientFactory) =>
        {
            try
            {
                var client = httpClientFactory.CreateClient("ApiClient");
                var response = await client.DeleteAsync($"api/tasks/{id}");
                return Results.Ok(new { success = response.IsSuccessStatusCode });
            }
            catch
            {
                return Results.BadRequest(new { success = false, message = "Sunucu hatası" });
            }
        })
        .RequireAuthorization();
    }
}
