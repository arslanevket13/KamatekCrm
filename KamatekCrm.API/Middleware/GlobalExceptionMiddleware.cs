using System.Net;
using System.Text.Json;
using Serilog;

namespace KamatekCrm.API.Middleware
{
    /// <summary>
    /// Global exception handler — tüm unhandled exception'ları yakalar,
    /// Serilog ile loglar ve istemciye standart JSON error response döner.
    /// Production'da stack trace gizlenir; Development'ta gösterilir.
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
            var (statusCode, errorType) = exception switch
            {
                ArgumentNullException => (HttpStatusCode.BadRequest, "Validation Error"),
                ArgumentException => (HttpStatusCode.BadRequest, "Validation Error"),
                KeyNotFoundException => (HttpStatusCode.NotFound, "Not Found"),
                UnauthorizedAccessException => (HttpStatusCode.Unauthorized, "Unauthorized"),
                InvalidOperationException => (HttpStatusCode.Conflict, "Conflict"),
                TimeoutException => (HttpStatusCode.RequestTimeout, "Timeout"),
                _ => (HttpStatusCode.InternalServerError, "Internal Server Error")
            };

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
                Error = new
                {
                    Type = errorType,
                    Message = _env.IsDevelopment() ? exception.Message : GetUserFriendlyMessage(statusCode),
                    CorrelationId = correlationId,
                    StackTrace = _env.IsDevelopment() ? exception.StackTrace : null
                }
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
