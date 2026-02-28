using System.Net.Http.Json;
using System.Security.Claims;
using KamatekCrm.Web.Services;
using KamatekCrm.Web.Shared;
using Microsoft.AspNetCore.Antiforgery;

namespace KamatekCrm.Web.Features.Customers;

public static class CustomersEndpoints
{
    public static void Map(WebApplication app)
    {
        // GET /customers - Müşteri listesi
        app.MapGet("/customers", async (
            HttpContext ctx,
            IHttpClientFactory httpClientFactory,
            IAntiforgery antiforgery,
            string? search = null,
            int page = 1) =>
        {
            var userName = ctx.User.FindFirst(ClaimTypes.Name)?.Value ?? "Kullanıcı";
            var tokens = antiforgery.GetAndStoreTokens(ctx);

            try
            {
                var client = httpClientFactory.CreateClient("ApiClient");
                var url = $"api/customers?page={page}&pageSize=20";
                if (!string.IsNullOrEmpty(search)) url += $"&search={Uri.EscapeDataString(search)}";
                
                var response = await client.GetAsync(url);
                var customers = response.IsSuccessStatusCode 
                    ? await response.Content.ReadFromJsonAsync<List<CustomerListItem>>() 
                    : new List<CustomerListItem>();

                var totalStr = response.Headers.FirstOrDefault(h => h.Key == "X-Total-Count").Value?.FirstOrDefault();
                var total = int.TryParse(totalStr, out var t) ? t : 0;

                var html = HtmlTemplates.CustomersPage(customers ?? [], total, page, userName, tokens.RequestToken, search);
                return Results.Content(html, "text/html; charset=utf-8");
            }
            catch
            {
                var html = HtmlTemplates.CustomersPage([], 0, 1, userName, tokens.RequestToken, search);
                return Results.Content(html, "text/html; charset=utf-8");
            }
        })
        .RequireAuthorization();

        // GET /customers/new - Yeni müşteri formu
        app.MapGet("/customers/new", (
            HttpContext ctx,
            IAntiforgery antiforgery) =>
        {
            var tokens = antiforgery.GetAndStoreTokens(ctx);
            var html = HtmlTemplates.CustomerForm(null, tokens.RequestToken);
            return Results.Content(html, "text/html; charset=utf-8");
        })
        .RequireAuthorization();

        // GET /customers/{id} - Müşteri detayı
        app.MapGet("/customers/{id}", async (
            int id,
            HttpContext ctx,
            IHttpClientFactory httpClientFactory,
            IAntiforgery antiforgery) =>
        {
            var tokens = antiforgery.GetAndStoreTokens(ctx);
            try
            {
                var client = httpClientFactory.CreateClient("ApiClient");
                var customer = await client.GetFromJsonAsync<CustomerListItem>($"api/customers/{id}");
                var html = HtmlTemplates.CustomerForm(customer, tokens.RequestToken);
                return Results.Content(html, "text/html; charset=utf-8");
            }
            catch
            {
                return Results.Redirect("/customers");
            }
        })
        .RequireAuthorization();

        // POST /customers - Yeni müşteri oluştur
        app.MapPost("/customers", async (
            HttpContext ctx,
            IHttpClientFactory httpClientFactory,
            IAntiforgery antiforgery) =>
        {
            try { await antiforgery.ValidateRequestAsync(ctx); }
            catch { return Results.BadRequest("Token hatası"); }

            var form = await ctx.Request.ReadFormAsync();
            var customer = new
            {
                FullName = form["FullName"].ToString(),
                PhoneNumber = form["PhoneNumber"].ToString(),
                Email = form["Email"].ToString(),
                Address = form["Address"].ToString(),
                City = form["City"].ToString(),
                District = form["District"].ToString()
            };

            try
            {
                var client = httpClientFactory.CreateClient("ApiClient");
                var response = await client.PostAsJsonAsync("api/customers", customer);
                if (response.IsSuccessStatusCode)
                {
                    ctx.Response.Headers["HX-Redirect"] = "/customers";
                    return Results.Ok();
                }
                return Results.BadRequest("Müşteri oluşturulamadı");
            }
            catch
            {
                return Results.BadRequest("Sunucu hatası");
            }
        })
        .RequireAuthorization();

        // PUT /customers/{id} - Müşteri güncelle
        app.MapPut("/customers/{id}", async (
            int id,
            HttpContext ctx,
            IHttpClientFactory httpClientFactory,
            IAntiforgery antiforgery) =>
        {
            try { await antiforgery.ValidateRequestAsync(ctx); }
            catch { return Results.BadRequest("Token hatası"); }

            var form = await ctx.Request.ReadFormAsync();
            var customer = new
            {
                Id = id,
                FullName = form["FullName"].ToString(),
                PhoneNumber = form["PhoneNumber"].ToString(),
                Email = form["Email"].ToString(),
                Address = form["Address"].ToString(),
                City = form["City"].ToString(),
                District = form["District"].ToString()
            };

            try
            {
                var client = httpClientFactory.CreateClient("ApiClient");
                var response = await client.PutAsJsonAsync($"api/customers/{id}", customer);
                if (response.IsSuccessStatusCode)
                {
                    ctx.Response.Headers["HX-Redirect"] = "/customers";
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

        // DELETE /customers/{id}
        app.MapDelete("/customers/{id}", async (
            int id,
            HttpContext ctx,
            IHttpClientFactory httpClientFactory) =>
        {
            try
            {
                var client = httpClientFactory.CreateClient("ApiClient");
                var response = await client.DeleteAsync($"api/customers/{id}");
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
