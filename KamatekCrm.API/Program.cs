using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Serilog;
using KamatekCrm.Data;
using KamatekCrm.API.Services;
using KamatekCrm.API.Middleware;
using KamatekCrm.API.Hubs;

// Serilog yapılandırması
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .WriteTo.File("logs/api-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

try
{
    Log.Information("Starting KamatekCRM API...");

    var builder = WebApplication.CreateBuilder(args);

    // Serilog'u kullan
    builder.Host.UseSerilog();

    // DbContext - PostgreSQL
    var connectionString = builder.Configuration.GetConnectionString("PostgreSQL") 
        ?? "Host=localhost;Port=5432;Database=kamatekcrm;Username=postgres;Password=123456";
    
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseNpgsql(connectionString, npgsqlOptions =>
        {
            npgsqlOptions.MigrationsAssembly("KamatekCrm.API");
        }));

    // MediatR - CQRS için
    builder.Services.AddMediatR(cfg => 
        cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

    // JWT Authentication
    var jwtKey = builder.Configuration["Jwt:Key"];
    var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "KamatekCRM";
    var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "KamatekCRM-Users";
    
    // JWT Key validasyonu
    if (string.IsNullOrWhiteSpace(jwtKey))
    {
        throw new InvalidOperationException("JWT Key is not configured. Please set 'Jwt:Key' in appsettings.json with at least 32 characters.");
    }
    
    if (jwtKey.Length < 32)
    {
        throw new InvalidOperationException($"JWT Key must be at least 32 characters long. Current length: {jwtKey.Length}");
    }

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtIssuer,
                ValidAudience = jwtAudience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
            };
        });

    builder.Services.AddAuthorization();

    // CORS - Web ve Mobile erişimi için (Güvenli yapılandırma)
    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() 
        ?? new[] { "http://localhost:3000", "http://localhost:7000" };
    
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowedOrigins", policy =>
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials();
        });
    });

    // Controllers + Action Filters
    builder.Services.AddControllers(options =>
    {
        options.Filters.Add<KamatekCrm.API.Middleware.ValidationFilter>();
        options.Filters.Add<KamatekCrm.API.Middleware.RequestTimingFilter>();
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });

    // Rate Limiting
    builder.Services.AddRateLimiting();

    // Services
    builder.Services.AddScoped<IPhotoStorageService, PhotoStorageService>();
    builder.Services.AddScoped<ISalesDomainService, SalesDomainService>();

    // Caching
    builder.Services.AddMemoryCache();
    builder.Services.AddSingleton<ICacheService, CacheService>();

    // Excel Export
    builder.Services.AddScoped<IExcelService, ExcelService>();

    // PDF Report Engine
    builder.Services.AddScoped<IPdfReportService, PdfReportService>();

    // SignalR Real-time
    builder.Services.AddSignalR();
    builder.Services.AddScoped<INotificationService, NotificationService>();

    // Network Discovery Service - UDP Broadcast
    var discoveryEnabled = builder.Configuration.GetValue<bool>("NetworkDiscovery:Enabled", true);
    if (discoveryEnabled)
    {
        builder.Services.AddHostedService<NetworkDiscoveryService>();
    }

    // Swagger/OpenAPI
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new()
        {
            Title = "KamatekCRM API",
            Version = "v1",
            Description = "KamatekCRM Web API - Görev yönetimi ve teknik servis işlemleri"
        });

        // JWT için Swagger ayarı
        c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\"",
            Name = "Authorization",
            In = Microsoft.OpenApi.Models.ParameterLocation.Header,
            Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
            Scheme = "Bearer"
        });

        c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
        {
            {
                new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                {
                    Reference = new Microsoft.OpenApi.Models.OpenApiReference
                    {
                        Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        });
    });

    var app = builder.Build();

    // --- EF Core Auto-Migration Injection ---
    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;
        try
        {
            var context = services.GetRequiredService<AppDbContext>();
            
            var databaseCreator = Microsoft.EntityFrameworkCore.Infrastructure.AccessorExtensions.GetService<Microsoft.EntityFrameworkCore.Storage.IRelationalDatabaseCreator>(context.Database);
            if (databaseCreator != null && !databaseCreator.Exists())
            {
                databaseCreator.Create();
            }

            // This command checks if DB exists, creates it if not, and applies all pending migrations.
            context.Database.Migrate();

            // Seed data
            DbInitializer.Initialize(context);
            Log.Information("Database migrations and seeding applied successfully at startup.");
        }
        catch (Exception ex)
        {
            var logger = services.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "An error occurred while migrating or seeding the database during application startup.");
            Log.Error(ex, "An error occurred while migrating the database.");
        }
    }

    // Middleware pipeline
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "KamatekCRM API v1");
            c.RoutePrefix = "swagger";
        });
    }

    // Global Exception Handler — En dış katman, her şeyi yakalar
    app.UseGlobalExceptionHandler();

    app.UseSerilogRequestLogging();
    app.UseCors("AllowedOrigins");
    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();
    app.MapHub<NotificationHub>("/hubs/notifications");

    // Health check endpoint
    app.MapGet("/health", () => Results.Ok(new { 
        Status = "Healthy", 
        Timestamp = DateTime.UtcNow,
        Version = "1.0.0"
    }));

    Log.Information("API running on: {Urls}", string.Join(", ", app.Urls));
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "API terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

