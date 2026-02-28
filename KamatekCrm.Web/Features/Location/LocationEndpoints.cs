using System.Net.Http.Json;
using System.Security.Claims;
using KamatekCrm.Web.Services;
using Microsoft.AspNetCore.Antiforgery;

namespace KamatekCrm.Web.Features.Location;

public static class LocationEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/location/update", async (HttpContext ctx, IHttpClientFactory httpClientFactory, IAntiforgery antiforgery) =>
        {
            try { await antiforgery.ValidateRequestAsync(ctx); }
            catch { return Results.BadRequest("Token hatası"); }
            var form = await ctx.Request.ReadFormAsync();
            var userId = ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var location = new { UserId = userId, Latitude = double.TryParse(form["Latitude"].ToString(), out var lat) ? lat : 0, Longitude = double.TryParse(form["Longitude"].ToString(), out var lng) ? lng : 0 };
            try
            {
                var client = httpClientFactory.CreateClient("ApiClient");
                var response = await client.PostAsJsonAsync("api/location", location);
                return Results.Ok(new { success = response.IsSuccessStatusCode });
            }
            catch { return Results.BadRequest(new { success = false }); }
        }).RequireAuthorization();
    }
}
