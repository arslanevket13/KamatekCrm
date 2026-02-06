using KamatekCrm.API.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// 1. KRITIK AYAR: API Portunu 5050'ye Sabitle (Cakismayi Onler)
// EXE olarak calistiginda 5000'e gitmesini engeller.
builder.WebHost.UseUrls("http://0.0.0.0:5050");

// DB Context
builder.Services.AddDbContext<ApiDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// 2. CORS AYARI (Tum erisimlere izin ver - Localhost sorunu icin)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", b =>
        b.AllowAnyOrigin()
         .AllowAnyMethod()
         .AllowAnyHeader());
});

// JWT Kimlik Dogrulama
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"] ?? "SecretKey"))
        };
    });

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => {
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement {
    {
        new OpenApiSecurityScheme
        { 
            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
        },
        new string[] { }
    }});
});

var app = builder.Build();

// Veritabani Baslatma (Otomatik Seed)
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
    try
    {
        // Veritabani yoksa olustur ve verileri ekle
        ApiDbSeeder.Seed(context);
        Console.WriteLine("Veritabani kontrolu basarili.");
    }
    catch (Exception ex)
    {
        Console.WriteLine("Veritabani hatasi: " + ex.Message);
    }
}

// Swagger (Dokumantasyon)
app.UseSwagger();
app.UseSwaggerUI();

// Middleware Siralamasi (CORS -> Auth -> Controllers)
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

Console.BackgroundColor = ConsoleColor.DarkGreen;
Console.ForegroundColor = ConsoleColor.White;
Console.WriteLine("==============================================");
Console.WriteLine(" API SERVER CALISTI (Port: 5050)              ");
Console.WriteLine("==============================================");
Console.ResetColor();

app.Run();
