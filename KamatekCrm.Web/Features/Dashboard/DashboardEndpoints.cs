using System.Security.Claims;
using KamatekCrm.Web.Shared;
using Microsoft.AspNetCore.Antiforgery;

namespace KamatekCrm.Web.Features.Dashboard;

/// <summary>
/// Korumalı Dashboard endpoint'i. Giriş yapmış kullanıcının
/// claim bilgilerini kullanarak kişiselleştirilmiş HTML döner.
/// </summary>
public static class DashboardEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/dashboard", (HttpContext ctx, IAntiforgery antiforgery) =>
        {
            var userName = ctx.User.FindFirst(ClaimTypes.Name)?.Value ?? "Kullanıcı";
            var role = ctx.User.FindFirst(ClaimTypes.Role)?.Value ?? "User";
            var tokens = antiforgery.GetAndStoreTokens(ctx);

            var html = HtmlTemplates.DashboardPage(userName, role, tokens.RequestToken);
            return Results.Content(html, "text/html; charset=utf-8");
        })
        .RequireAuthorization();
    }
}
