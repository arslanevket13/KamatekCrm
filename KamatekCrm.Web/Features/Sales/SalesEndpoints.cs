using System.Net.Http.Json;
using System.Security.Claims;
using KamatekCrm.Web.Services;
using KamatekCrm.Web.Shared;
using Microsoft.AspNetCore.Antiforgery;

namespace KamatekCrm.Web.Features.Sales;

public static class SalesEndpoints
{
    public static void Map(WebApplication app)
    {
        // GET /sales - POS/Satış ekranı
        app.MapGet("/sales", async (
            HttpContext ctx,
            IHttpClientFactory httpClientFactory,
            IAntiforgery antiforgery) =>
        {
            var userName = ctx.User.FindFirst(ClaimTypes.Name)?.Value ?? "Kullanıcı";
            var tokens = antiforgery.GetAndStoreTokens(ctx);

            try
            {
                var client = httpClientFactory.CreateClient("ApiClient");
                var productsResponse = await client.GetAsync("api/products?pageSize=100");
                var products = productsResponse.IsSuccessStatusCode 
                    ? await productsResponse.Content.ReadFromJsonAsync<List<ProductListItem>>() 
                    : new List<ProductListItem>();

                var html = HtmlTemplates.SalesPage(products ?? [], userName, tokens.RequestToken);
                return Results.Content(html, "text/html; charset=utf-8");
            }
            catch
            {
                var html = HtmlTemplates.SalesPage([], userName, tokens.RequestToken);
                return Results.Content(html, "text/html; charset=utf-8");
            }
        })
        .RequireAuthorization();

        // POST /sales - Satış oluştur
        app.MapPost("/sales", async (
            HttpContext ctx,
            IHttpClientFactory httpClientFactory,
            IAntiforgery antiforgery) =>
        {
            try { await antiforgery.ValidateRequestAsync(ctx); }
            catch { return Results.BadRequest("Token hatası"); }

            var form = await ctx.Request.ReadFormAsync();
            var itemsJson = form["Items"].ToString();
            
            try
            {
                var items = System.Text.Json.JsonSerializer.Deserialize<List<dynamic>>(itemsJson);
                var sale = new
                {
                    Items = items,
                    TotalAmount = decimal.Parse(form["TotalAmount"].ToString()),
                    PaymentMethod = form["PaymentMethod"].ToString(),
                    CustomerId = string.IsNullOrEmpty(form["CustomerId"].ToString()) ? (int?)null : int.Parse(form["CustomerId"].ToString()!)
                };

                var client = httpClientFactory.CreateClient("ApiClient");
                var response = await client.PostAsJsonAsync("api/sales", sale);
                
                if (response.IsSuccessStatusCode)
                {
                    ctx.Response.Headers["HX-Redirect"] = "/sales?success=true";
                    return Results.Ok();
                }
                return Results.BadRequest("Satış kaydedilemedi");
            }
            catch
            {
                return Results.BadRequest("Sunucu hatası");
            }
        })
        .RequireAuthorization();

        // GET /sales/history - Satış geçmişi
        app.MapGet("/sales/history", async (
            HttpContext ctx,
            IHttpClientFactory httpClientFactory,
            IAntiforgery antiforgery,
            int page = 1) =>
        {
            var userName = ctx.User.FindFirst(ClaimTypes.Name)?.Value ?? "Kullanıcı";
            var tokens = antiforgery.GetAndStoreTokens(ctx);

            try
            {
                var client = httpClientFactory.CreateClient("ApiClient");
                var url = $"api/sales?page={page}&pageSize=20";
                var response = await client.GetAsync(url);
                
                var html = HtmlTemplates.SalesHistoryPage([], 0, page, userName, tokens.RequestToken);
                return Results.Content(html, "text/html; charset=utf-8");
            }
            catch
            {
                var html = HtmlTemplates.SalesHistoryPage([], 0, page, userName, tokens.RequestToken);
                return Results.Content(html, "text/html; charset=utf-8");
            }
        })
        .RequireAuthorization();
    }
}
