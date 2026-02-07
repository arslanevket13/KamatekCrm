using Blazored.LocalStorage;
using KamatekCrm.Web.Components;
using KamatekCrm.Web.Services;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

try 
{
    // [CRITICAL] FORCE BIND TO PORT 7000
    builder.WebHost.UseUrls("http://0.0.0.0:7000");

    // [CRITICAL] FORCE DEVELOPMENT ENVIRONMENT FOR STATIC ASSETS
    builder.Environment.EnvironmentName = "Development";

    // Add services to the container.
    builder.Services.AddRazorComponents()
        .AddInteractiveServerComponents();

    builder.Services.AddMudServices();
    builder.Services.AddBlazoredLocalStorage();

    // [CRITICAL] HTTP CLIENT POINTING TO API 5050
    builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri("http://localhost:5050/") });

    // [CRITICAL] AUTH STATE PROVIDER
    builder.Services.AddScoped<AuthenticationStateProvider, ApiAuthenticationStateProvider>();
    builder.Services.AddScoped<ApiAuthenticationStateProvider>(sp => (ApiAuthenticationStateProvider)sp.GetRequiredService<AuthenticationStateProvider>());
    builder.Services.AddScoped<IAuthService, AuthService>();

    var app = builder.Build();

    // Configure the HTTP request pipeline.
    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error", createScopeForErrors: true);
    }

    app.UseStaticFiles();
    app.UseAntiforgery();

    app.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode();

    Console.WriteLine("=================================");
    Console.WriteLine("   KAMATEK CRM WEB READY (7000)  ");
    Console.WriteLine("=================================");

    app.Run();
}
catch (Exception ex)
{
    Console.BackgroundColor = ConsoleColor.Red;
    Console.ForegroundColor = ConsoleColor.White;
    Console.WriteLine("------------------------------------------------");
    Console.WriteLine("CRITICAL STARTUP ERROR:");
    Console.WriteLine(ex.ToString());
    Console.WriteLine("------------------------------------------------");
    Console.ResetColor();
    Console.WriteLine("Press ENTER to exit...");
    Console.ReadLine(); // Keeping console open
}
