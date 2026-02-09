using KamatekCrm.Web.Components;
using Blazored.LocalStorage;
using KamatekCrm.Web.Services;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor.Services;
using Microsoft.AspNetCore.Authentication.Cookies; // EKLENDİ


// PostgreSQL Legacy Timestamp Behavior
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// 1. Portu 7000'e Sabitle
builder.WebHost.UseUrls("http://localhost:7000");

// 2. Temel Razor Bileşenleri
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// 3. MudBlazor Servisi
builder.Services.AddMudServices();

// 4. Kimlik Doğrulama ve Yetkilendirme (Hata Çözümü Burada)
builder.Services.AddCascadingAuthenticationState();

// HATANIN ÇÖZÜMÜ: Varsayılan şema olarak "Cookies" belirtildi
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login"; // Yetkisiz girişte yönlendirilecek sayfa
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
    });

builder.Services.AddAuthorizationCore();

// 5. Client Servisleri
builder.Services.AddBlazoredLocalStorage();
builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthenticationStateProvider>();
builder.Services.AddScoped<IClientAuthService, ClientAuthService>();
builder.Services.AddScoped<ITaskService, TaskService>();

// 6. API Bağlantısı (Port 5050)
builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri("http://localhost:5050")
});

var app = builder.Build();

// 7. Hata Yönetimi
app.UseDeveloperExceptionPage();
// app.UseHttpsRedirection(); // Localhost hatasını önlemek için kapalı

app.UseStaticFiles();

// Authentication ve Authorization Middleware (Sıralama Önemli)
app.UseAuthentication(); // Önce kimlik doğrula
app.UseAuthorization();  // Sonra yetki kontrolü yap

app.UseAntiforgery();

app.MapStaticAssets();

app.MapRazorComponents<KamatekCrm.Web.Components.App>()
    .AddInteractiveServerRenderMode();

Console.WriteLine("--> Web Uygulamasi 7000 Portunda (Auth Fix) ile Hazir.");

app.Run();