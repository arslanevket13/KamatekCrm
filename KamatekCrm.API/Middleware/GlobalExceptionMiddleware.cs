using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Serilog;

namespace KamatekCrm.API.Middleware
{
    /// <summary>
    /// Global exception handler — tüm unhandled exception'ları yakalar,
    /// Serilog ile loglar ve istemciye standart JSON error response döner.
    /// DbUpdateException ve PostgresException'ları yakalayarak Türkçe mesajlar üretir.
    /// </summary>
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IHostEnvironment _env;

        public GlobalExceptionMiddleware(RequestDelegate next, IHostEnvironment env)
        {
            _next = next;
            _env = env;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(context, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            var statusCode = HttpStatusCode.InternalServerError;
            var errorType = "Internal Server Error";
            var uiMessage = "Beklenmeyen bir hata oluştu. Lütfen tekrar deneyin.";

            // PostgreSQL / EF Core DB Error Parsing
            if (exception is DbUpdateException dbEx && dbEx.InnerException is PostgresException pgEx)
            {
                statusCode = HttpStatusCode.Conflict;
                errorType = "Database Constraint Violation";
                
                uiMessage = pgEx.SqlState switch
                {
                    "23505" => "Bu kayıt zaten mevcut. Lütfen benzersiz bir değer girin. (Unique Constraint)",
                    "23503" => "Bu işlem yapılamaz çünkü ilişkili kayıtlar mevcut. (Foreign Key Violation)",
                    "23502" => "Zorunlu bir alan boş bırakılmış. (Not Null Violation)",
                    _ => "Veritabanı kayıt işlemi sırasında kural ihlali oluştu."
                };
            }
            else if (exception is PostgresException rawPgEx)
            {
                statusCode = HttpStatusCode.ServiceUnavailable;
                errorType = "Database Connection Error";
                uiMessage = "Veritabanı sunucusuna erişilemiyor. Lütfen sistem yöneticisi ile iletişime geçin.";
            }
            else
            {
                (statusCode, errorType) = exception switch
                {
                    ArgumentNullException => (HttpStatusCode.BadRequest, "Validation Error"),
                    ArgumentException => (HttpStatusCode.BadRequest, "Validation Error"),
                    KeyNotFoundException => (HttpStatusCode.NotFound, "Not Found"),
                    UnauthorizedAccessException => (HttpStatusCode.Unauthorized, "Unauthorized"),
                    InvalidOperationException => (HttpStatusCode.Conflict, "Conflict"),
                    TimeoutException => (HttpStatusCode.RequestTimeout, "Timeout"),
                    _ => (HttpStatusCode.InternalServerError, "Internal Server Error")
                };
                
                uiMessage = GetUserFriendlyMessage(statusCode);
            }

            // Log with correlation
            var correlationId = context.TraceIdentifier;
            Log.Error(exception,
                "Unhandled exception [{ErrorType}] CorrelationId={CorrelationId} Path={Path} Method={Method}",
                errorType, correlationId, context.Request.Path, context.Request.Method);

            context.Response.StatusCode = (int)statusCode;
            context.Response.ContentType = "application/json";

            var response = new
            {
                Success = false,
                Message = uiMessage, // Now using our mapped Turkish message
                Errors = new List<string> { _env.IsDevelopment() ? exception.Message : uiMessage }
            };

            var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });

            await context.Response.WriteAsync(json);
        }

        private static string GetUserFriendlyMessage(HttpStatusCode statusCode) => statusCode switch
        {
            HttpStatusCode.BadRequest => "Geçersiz istek. Lütfen gönderilen verileri kontrol edin.",
            HttpStatusCode.NotFound => "İstenen kayıt bulunamadı.",
            HttpStatusCode.Unauthorized => "Bu işlem için yetkiniz bulunmamaktadır.",
            HttpStatusCode.Conflict => "İşlem çakışması. Lütfen tekrar deneyin.",
            HttpStatusCode.RequestTimeout => "İşlem zaman aşımına uğradı.",
            _ => "Beklenmeyen bir hata oluştu. Lütfen tekrar deneyin."
        };
    }

    /// <summary>
    /// Extension method for registering the middleware
    /// </summary>
    public static class GlobalExceptionMiddlewareExtensions
    {
        public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder app)
        {
            return app.UseMiddleware<GlobalExceptionMiddleware>();
        }
    }
}
