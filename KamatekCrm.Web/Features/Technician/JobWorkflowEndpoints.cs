using System.Net.Http.Json;
using System.Security.Claims;
using KamatekCrm.Web.Services;
using KamatekCrm.Web.Shared;
using Microsoft.AspNetCore.Antiforgery;
using KamatekCrm.Shared.Enums;

namespace KamatekCrm.Web.Features.Technician;

/// <summary>
/// Teknisyen iş akışı endpoint'leri:
/// - İş detay görüntüleme
/// - İşe başla / tamamla
/// - İşe not ekle
/// - Fotoğraf yükleme
/// </summary>
public static class JobWorkflowEndpoints
{
    public static void Map(WebApplication app)
    {
        // GET /technician/job/{id} — İş detay kartı
        app.MapGet("/technician/job/{id}", async (
            int id,
            HttpContext ctx,
            IHttpClientFactory httpClientFactory,
            IAntiforgery antiforgery) =>
        {
            var userName = ctx.User.FindFirst(ClaimTypes.Name)?.Value ?? "Teknisyen";
            var tokens = antiforgery.GetAndStoreTokens(ctx);

            try
            {
                var client = httpClientFactory.CreateClient("ApiClient");
                var job = await client.GetFromJsonAsync<JobListItem>($"api/servicejobs/{id}");

                if (job == null)
                    return Results.Redirect("/technician/dashboard");

                var html = JobDetailPage(job, userName, tokens.RequestToken);
                return Results.Content(html, "text/html; charset=utf-8");
            }
            catch
            {
                return Results.Redirect("/technician/dashboard");
            }
        })
        .RequireAuthorization();

        // POST /technician/job/{id}/start — İşe başla
        app.MapPost("/technician/job/{id}/start", async (
            int id,
            HttpContext ctx,
            IHttpClientFactory httpClientFactory,
            IAntiforgery antiforgery) =>
        {
            try { await antiforgery.ValidateRequestAsync(ctx); }
            catch { return Results.BadRequest("Token hatası"); }

            try
            {
                var client = httpClientFactory.CreateClient("ApiClient");
                var response = await client.PatchAsJsonAsync($"api/servicejobs/{id}/status", new { Status = JobStatus.InProgress });

                if (response.IsSuccessStatusCode)
                {
                    ctx.Response.Headers["HX-Redirect"] = $"/technician/job/{id}";
                    return Results.Ok();
                }
                return Results.BadRequest("İş başlatılamadı");
            }
            catch { return Results.BadRequest("Sunucu hatası"); }
        })
        .RequireAuthorization();

        // POST /technician/job/{id}/complete — İşi tamamla
        app.MapPost("/technician/job/{id}/complete", async (
            int id,
            HttpContext ctx,
            IHttpClientFactory httpClientFactory,
            IAntiforgery antiforgery) =>
        {
            try { await antiforgery.ValidateRequestAsync(ctx); }
            catch { return Results.BadRequest("Token hatası"); }

            var form = await ctx.Request.ReadFormAsync();
            var notes = form["CompletionNotes"].ToString();

            try
            {
                var client = httpClientFactory.CreateClient("ApiClient");
                var response = await client.PatchAsJsonAsync($"api/servicejobs/{id}/status", new { Status = JobStatus.Completed });

                if (response.IsSuccessStatusCode)
                {
                    ctx.Response.Headers["HX-Redirect"] = "/technician/dashboard";
                    return Results.Ok();
                }
                return Results.BadRequest("İş tamamlanamadı");
            }
            catch { return Results.BadRequest("Sunucu hatası"); }
        })
        .RequireAuthorization();

        // POST /technician/job/{id}/note — İşe not ekle (HTMX partial)
        app.MapPost("/technician/job/{id}/note", async (
            int id,
            HttpContext ctx,
            IHttpClientFactory httpClientFactory,
            IAntiforgery antiforgery) =>
        {
            try { await antiforgery.ValidateRequestAsync(ctx); }
            catch { return Results.BadRequest("Token hatası"); }

            var form = await ctx.Request.ReadFormAsync();
            var note = form["Note"].ToString();
            var userName = ctx.User.FindFirst(ClaimTypes.Name)?.Value ?? "Teknisyen";
            var now = DateTime.Now;

            // Return the new note card via HTMX
            var noteHtml = $"""
            <div class="card mb-2">
                <div class="card-body">
                    <div class="d-flex justify-content-between align-items-start">
                        <div>
                            <small class="text-muted"><i class="bi bi-person me-1"></i>{userName} — {now:dd.MM.yyyy HH:mm}</small>
                            <p class="mb-0 mt-1">{note}</p>
                        </div>
                    </div>
                </div>
            </div>
            """;

            return Results.Content(noteHtml, "text/html; charset=utf-8");
        })
        .RequireAuthorization();
    }

    // ─── İŞ DETAY SAYFASI ───
    private static string JobDetailPage(JobListItem job, string userName, string? token)
    {
        var statusColor = job.Status switch
        {
            "Tamamlandı" => "success",
            "Devam Ediyor" => "primary",
            "Bekliyor" => "warning",
            "İptal" => "danger",
            _ => "secondary"
        };

        var canStart = job.Status == "Bekliyor";
        var canComplete = job.Status == "Devam Ediyor";

        var actionButtons = "";
        if (canStart)
            actionButtons = $"""
            <form hx-post="/technician/job/{job.Id}/start" hx-swap="none" class="d-inline">
                <button type="submit" class="btn btn-primary"><i class="bi bi-play-fill me-1"></i>İşe Başla</button>
            </form>
            """;
        else if (canComplete)
            actionButtons = $"""
            <button type="button" class="btn btn-success" data-bs-toggle="collapse" data-bs-target="#completeForm">
                <i class="bi bi-check-circle me-1"></i>İşi Tamamla
            </button>
            """;

        var completeForm = canComplete ? $"""
        <div class="collapse mt-3" id="completeForm">
            <div class="content-card" style="border-color:var(--accent-green)">
                <h6><i class="bi bi-check-circle text-success me-2"></i>İşi Tamamla</h6>
                <form hx-post="/technician/job/{job.Id}/complete" hx-swap="none">
                    <div class="mb-3">
                        <label class="form-label">Tamamlama Notu</label>
                        <textarea class="form-control" name="CompletionNotes" rows="3" placeholder="Yapılan işleri açıklayın..."></textarea>
                    </div>
                    <button type="submit" class="btn btn-success"><i class="bi bi-check-lg me-1"></i>Tamamla ve Kapat</button>
                </form>
            </div>
        </div>
        """ : "";

        var content = $$"""
        <div class="page-header">
            <div class="d-flex justify-content-between align-items-start flex-wrap gap-2">
                <div>
                    <a href="/technician/dashboard" class="text-muted text-decoration-none" style="font-size:0.85rem">
                        <i class="bi bi-arrow-left me-1"></i>Teknisyen Panel
                    </a>
                    <h2 class="mt-1"><i class="bi bi-clipboard-check me-2"></i>{{job.Title}}</h2>
                </div>
                <span class="badge bg-{{statusColor}}" style="font-size:0.9rem;padding:8px 16px">{{job.Status}}</span>
            </div>
        </div>

        <!-- İş Bilgileri -->
        <div class="content-card">
            <div class="row g-3">
                <div class="col-md-6">
                    <div class="p-3 rounded" style="background:var(--bg-hover)">
                        <small class="text-muted d-block mb-1"><i class="bi bi-person me-1"></i>Müşteri</small>
                        <strong>{{job.CustomerName ?? "Belirtilmemiş"}}</strong>
                    </div>
                </div>
                <div class="col-md-6">
                    <div class="p-3 rounded" style="background:var(--bg-hover)">
                        <small class="text-muted d-block mb-1"><i class="bi bi-calendar me-1"></i>Planlanan Tarih</small>
                        <strong>{{job.ScheduledDate?.ToString("dd MMMM yyyy, dddd") ?? "Belirtilmemiş"}}</strong>
                    </div>
                </div>
            </div>
            {{(job.Description != null ? $"<div class=\"mt-3\"><small class=\"text-muted\">Açıklama</small><p class=\"mt-1\">{job.Description}</p></div>" : "")}}
        </div>

        <!-- Aksiyon Butonları -->
        <div class="content-card">
            <h5 class="card-section-title"><i class="bi bi-lightning-fill text-warning me-2"></i>İşlemler</h5>
            <div class="d-flex flex-wrap gap-2 mt-2">
                {{actionButtons}}
                <a href="/jobs/{{job.Id}}" class="btn btn-outline-secondary"><i class="bi bi-pencil me-1"></i>Düzenle</a>
            </div>
            {{completeForm}}
        </div>

        <!-- Not Ekleme -->
        <div class="content-card">
            <h5 class="card-section-title"><i class="bi bi-chat-dots text-info me-2"></i>Notlar</h5>
            <form hx-post="/technician/job/{{job.Id}}/note" hx-target="#notes-list" hx-swap="afterbegin" class="mb-3">
                <div class="d-flex gap-2">
                    <input type="text" class="form-control" name="Note" placeholder="Not ekle..." required>
                    <button type="submit" class="btn btn-primary"><i class="bi bi-send"></i></button>
                </div>
            </form>
            <div id="notes-list"></div>
        </div>
        """;

        return HtmlTemplates.Layout("İş Detay", content, userName, token);
    }
}
