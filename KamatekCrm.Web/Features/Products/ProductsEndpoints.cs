using System.Net.Http.Json;
using System.Security.Claims;
using KamatekCrm.Web.Services;
using KamatekCrm.Web.Shared;
using Microsoft.AspNetCore.Antiforgery;

namespace KamatekCrm.Web.Features.Products;

public static class ProductsEndpoints
{
    public static void Map(WebApplication app)
    {
        // GET /products - Ürün/Stok listesi
        app.MapGet("/products", async (
            HttpContext ctx,
            IHttpClientFactory httpClientFactory,
            IAntiforgery antiforgery,
            string? search = null,
            string? category = null,
            int page = 1) =>
        {
            var userName = ctx.User.FindFirst(ClaimTypes.Name)?.Value ?? "Kullanıcı";
            var tokens = antiforgery.GetAndStoreTokens(ctx);

            try
            {
                var client = httpClientFactory.CreateClient("ApiClient");
                var url = $"api/products?page={page}&pageSize=20";
                if (!string.IsNullOrEmpty(search)) url += $"&search={Uri.EscapeDataString(search)}";
                
                var response = await client.GetAsync(url);
                var products = response.IsSuccessStatusCode 
                    ? await response.Content.ReadFromJsonAsync<List<ProductListItem>>() 
                    : new List<ProductListItem>();

                var totalStr = response.Headers.FirstOrDefault(h => h.Key == "X-Total-Count").Value?.FirstOrDefault();
                var total = int.TryParse(totalStr, out var t) ? t : 0;

                var html = HtmlTemplates.ProductsPage(products ?? [], total, page, userName, tokens.RequestToken, search);
                return Results.Content(html, "text/html; charset=utf-8");
            }
            catch
            {
                var html = HtmlTemplates.ProductsPage([], 0, 1, userName, tokens.RequestToken, search);
                return Results.Content(html, "text/html; charset=utf-8");
            }
        })
        .RequireAuthorization();

        // GET /products/new - Yeni ürün formu
        app.MapGet("/products/new", (
            HttpContext ctx,
            IAntiforgery antiforgery) =>
        {
            var tokens = antiforgery.GetAndStoreTokens(ctx);
            var html = HtmlTemplates.ProductForm(null, tokens.RequestToken);
            return Results.Content(html, "text/html; charset=utf-8");
        })
        .RequireAuthorization();

        // GET /products/{id} - Ürün detayı
        app.MapGet("/products/{id}", async (
            int id,
            HttpContext ctx,
            IHttpClientFactory httpClientFactory,
            IAntiforgery antiforgery) =>
        {
            var tokens = antiforgery.GetAndStoreTokens(ctx);
            try
            {
                var client = httpClientFactory.CreateClient("ApiClient");
                var product = await client.GetFromJsonAsync<ProductListItem>($"api/products/{id}");
                var html = HtmlTemplates.ProductForm(product, tokens.RequestToken);
                return Results.Content(html, "text/html; charset=utf-8");
            }
            catch
            {
                return Results.Redirect("/products");
            }
        })
        .RequireAuthorization();

        // POST /products - Yeni ürün oluştur
        app.MapPost("/products", async (
            HttpContext ctx,
            IHttpClientFactory httpClientFactory,
            IAntiforgery antiforgery) =>
        {
            try { await antiforgery.ValidateRequestAsync(ctx); }
            catch { return Results.BadRequest("Token hatası"); }

            var form = await ctx.Request.ReadFormAsync();
            var product = new
            {
                ProductName = form["ProductName"].ToString(),
                SKU = form["SKU"].ToString(),
                Barcode = form["Barcode"].ToString(),
                SalePrice = decimal.Parse(form["SalePrice"].ToString()),
                PurchasePrice = decimal.Parse(form["PurchasePrice"].ToString()),
                TotalStockQuantity = int.Parse(form["TotalStockQuantity"].ToString()),
                MinStockLevel = int.Parse(form["MinStockLevel"].ToString())
            };

            try
            {
                var client = httpClientFactory.CreateClient("ApiClient");
                var response = await client.PostAsJsonAsync("api/products", product);
                if (response.IsSuccessStatusCode)
                {
                    ctx.Response.Headers["HX-Redirect"] = "/products";
                    return Results.Ok();
                }
                return Results.BadRequest("Ürün oluşturulamadı");
            }
            catch
            {
                return Results.BadRequest("Sunucu hatası");
            }
        })
        .RequireAuthorization();

        // PUT /products/{id} - Ürün güncelle
        app.MapPut("/products/{id}", async (
            int id,
            HttpContext ctx,
            IHttpClientFactory httpClientFactory,
            IAntiforgery antiforgery) =>
        {
            try { await antiforgery.ValidateRequestAsync(ctx); }
            catch { return Results.BadRequest("Token hatası"); }

            var form = await ctx.Request.ReadFormAsync();
            var product = new
            {
                Id = id,
                ProductName = form["ProductName"].ToString(),
                SKU = form["SKU"].ToString(),
                Barcode = form["Barcode"].ToString(),
                SalePrice = decimal.Parse(form["SalePrice"].ToString()),
                PurchasePrice = decimal.Parse(form["PurchasePrice"].ToString()),
                TotalStockQuantity = int.Parse(form["TotalStockQuantity"].ToString()),
                MinStockLevel = int.Parse(form["MinStockLevel"].ToString())
            };

            try
            {
                var client = httpClientFactory.CreateClient("ApiClient");
                var response = await client.PutAsJsonAsync($"api/products/{id}", product);
                if (response.IsSuccessStatusCode)
                {
                    ctx.Response.Headers["HX-Redirect"] = "/products";
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

        // DELETE /products/{id}
        app.MapDelete("/products/{id}", async (
            int id,
            HttpContext ctx,
            IHttpClientFactory httpClientFactory) =>
        {
            try
            {
                var client = httpClientFactory.CreateClient("ApiClient");
                var response = await client.DeleteAsync($"api/products/{id}");
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
