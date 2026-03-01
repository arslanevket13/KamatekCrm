using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.ComponentModel.DataAnnotations;
using Serilog;

namespace KamatekCrm.API.Middleware
{
    /// <summary>
    /// Model validation filter — [ApiController] otomatik validasyonu genişletir.
    /// DataAnnotation hatalarını standart ApiResponse formatında döner.
    /// </summary>
    public class ValidationFilter : IActionFilter
    {
        public void OnActionExecuting(ActionExecutingContext context)
        {
            if (!context.ModelState.IsValid)
            {
                var errors = context.ModelState
                    .Where(x => x.Value?.Errors.Count > 0)
                    .Select(x => new
                    {
                        Field = x.Key,
                        Errors = x.Value!.Errors.Select(e => 
                            !string.IsNullOrEmpty(e.ErrorMessage) 
                                ? e.ErrorMessage 
                                : e.Exception?.Message ?? "Geçersiz değer"
                        ).ToList()
                    })
                    .ToList();

                Log.Warning("Validation failed: {@Errors}", errors);

                context.Result = new BadRequestObjectResult(new
                {
                    Success = false,
                    Error = new
                    {
                        Type = "Validation Error",
                        Message = "Gönderilen veriler geçersiz.",
                        Details = errors
                    }
                });
            }
        }

        public void OnActionExecuted(ActionExecutedContext context) { }
    }

    /// <summary>
    /// Request/Response loglama filter'ı — 
    /// her API çağrısının süresini ölçer ve loglar.
    /// </summary>
    public class RequestTimingFilter : IActionFilter
    {
        private const string StopwatchKey = "RequestStopwatch";

        public void OnActionExecuting(ActionExecutingContext context)
        {
            context.HttpContext.Items[StopwatchKey] = System.Diagnostics.Stopwatch.StartNew();
        }

        public void OnActionExecuted(ActionExecutedContext context)
        {
            if (context.HttpContext.Items.TryGetValue(StopwatchKey, out var sw) 
                && sw is System.Diagnostics.Stopwatch stopwatch)
            {
                stopwatch.Stop();
                var elapsed = stopwatch.ElapsedMilliseconds;

                // Yavaş istekleri özel olarak logla
                if (elapsed > 1000)
                {
                    Log.Warning("Slow API call: {Method} {Path} took {ElapsedMs}ms",
                        context.HttpContext.Request.Method,
                        context.HttpContext.Request.Path,
                        elapsed);
                }
                else
                {
                    Log.Debug("API call: {Method} {Path} completed in {ElapsedMs}ms",
                        context.HttpContext.Request.Method,
                        context.HttpContext.Request.Path,
                        elapsed);
                }

                // Response header'a ekle
                context.HttpContext.Response.Headers.Append("X-Response-Time-Ms", elapsed.ToString());
            }
        }
    }
}
