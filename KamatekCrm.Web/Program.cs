using KamatekCrm.Web.Components;
using KamatekCrm.Web.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Blazored.LocalStorage;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. KRITIK AYAR: Portu 5200'e Zorla (EXE icin sart)
builder.WebHost.UseUrls("http://localhost:5200");

// 2. Statik Dosya Yolu (wwwroot hatasini onler)
var binPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
var wwwRootPath = Path.Combine(binPath!, "wwwroot");
if (Directory.Exists(wwwRootPath))
{
    builder.Environment.WebRootPath = wwwRootPath;
}

// Servisler
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();
builder.Services.AddBlazoredLocalStorage();
builder.Services.AddScoped<AuthenticationStateProvider, ApiAuthenticationStateProvider>();
builder.Services.AddAuthorizationCore();

// API Baglantisi (5050 Portu)
builder.Services.AddHttpClient("API", client =>
{
    client.BaseAddress = new Uri("http://localhost:5050");
});
builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("API"));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine("WEB SERVER CALISTI! Adres: http://localhost:5200");
Console.ResetColor();

app.Run();
