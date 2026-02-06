using KamatekCrm.Web.Components;
using KamatekCrm.Web.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Blazored.LocalStorage;
using MudBlazor.Services;
using Microsoft.Extensions.FileProviders;

try
{
    var builder = WebApplication.CreateBuilder(args);

    // 1. PORT 7001 (Port çakışması önlendi)
    builder.WebHost.UseUrls("http://0.0.0.0:7001");

    // 2. DOSYA YOLU AYARI - FIX
    // StaticWebAssets (Development) mekanizmasini bozmamak icin
    // WebRootPath ayarlamasini sadece Production moduna kisitliyoruz.
    // Development modunda otomatik olarak Manifest kullanilir.
    
    var binPath = AppContext.BaseDirectory;
    var wwwRootPath = Path.Combine(binPath, "wwwroot");

    if (!builder.Environment.IsDevelopment())
    {
        // Production/Staging: Fiziksel wwwroot klasorunu kullan
        if (Directory.Exists(wwwRootPath))
        {
             builder.Environment.WebRootPath = wwwRootPath;
             builder.Environment.ContentRootPath = binPath;
        }
    }

    // Servisler
    builder.Services.AddRazorComponents()
        .AddInteractiveServerComponents();

    builder.Services.AddMudServices();
    builder.Services.AddBlazoredLocalStorage();
    
    // AUTH SERVICE FIX: Hem Interface hem Concrete olarak erisilebilir olmali
    builder.Services.AddScoped<ApiAuthenticationStateProvider>();
    builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<ApiAuthenticationStateProvider>());
    builder.Services.AddAuthorizationCore();
    
    // AUTHENTICATION EKLENDI (HTTP 500 Cozumu)
    builder.Services.AddAuthentication("Cookies")
        .AddCookie("Cookies");

    // API Bağlantısı (5050 - Burası sabit kalmalı)
    builder.Services.AddHttpClient("API", client =>
    {
        client.BaseAddress = new Uri("http://localhost:5050");
    });
    builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("API"));

    var app = builder.Build();

    // HSTS ve HTTPS Yonlendirmesi KAPALI kalmali
    app.UseStaticFiles();
    app.UseAntiforgery();
    
    app.UseAuthentication(); // EKLENDI
    app.UseAuthorization();  // EKLENDI

    app.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode();

    // EKRAN CIKTISINI DUZELTTIK:
    Console.BackgroundColor = ConsoleColor.Blue;
    Console.ForegroundColor = ConsoleColor.White;
    Console.WriteLine("================================================");
    Console.WriteLine(" WEB SERVER AKTIF - GUVENLIK KILIDI KALDIRILDI  ");
    Console.WriteLine(" URL: http://localhost:7001                     ");
    Console.WriteLine("================================================");
    Console.ResetColor();

    app.Run();
}
catch (Exception ex)
{
    Console.BackgroundColor = ConsoleColor.Red;
    Console.ForegroundColor = ConsoleColor.White;
    Console.WriteLine("================================================");
    Console.WriteLine(" KRITIK HATA - WEB SUNUCU BAŞLATILAMADI!");
    Console.WriteLine("================================================");
    Console.ResetColor();
    Console.WriteLine($"\nHATA DETAYI:\n{ex.Message}");
    Console.WriteLine($"\nSTACK TRACE:\n{ex.StackTrace}");
    if (ex.InnerException != null)
    {
        Console.WriteLine($"\nIÇ HATA:\n{ex.InnerException.Message}");
    }
    Console.WriteLine("\n\nDevam etmek için bir tuşa basın...");
    Console.ReadLine();
}