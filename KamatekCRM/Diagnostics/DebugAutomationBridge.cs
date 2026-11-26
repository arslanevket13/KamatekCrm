#if DEBUG
using System;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using KamatekCrm.Helpers;
using KamatekCrm.Services;
using KamatekCrm.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace KamatekCrm.Diagnostics
{
    /// <summary>
    /// Yalnızca DEBUG modunda çalışan, sadece loopback (127.0.0.1:19876) dinleyen
    /// yerel otomasyon ve teşhis HTTP köprüsü.
    /// </summary>
    public sealed class DebugAutomationBridge : IDisposable
    {
        public const string DefaultPrefix = "http://127.0.0.1:19876/";

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never
        };

        private readonly Func<IServiceProvider?> _serviceProviderAccessor;
        private readonly DebugUiAutomationHelper _uiHelper = new();
        private readonly CancellationTokenSource _cts = new();
        private HttpListener? _listener;
        private bool _disposed;

        public DebugAutomationBridge(Func<IServiceProvider?> serviceProviderAccessor)
        {
            _serviceProviderAccessor = serviceProviderAccessor ?? throw new ArgumentNullException(nameof(serviceProviderAccessor));
        }

        public void Start()
        {
            if (_disposed || _listener != null)
            {
                return;
            }

            try
            {
                var listener = new HttpListener();
                listener.Prefixes.Add(DefaultPrefix);
                listener.Start();
                _listener = listener;

                Log.Information("DebugAutomationBridge aktif: {Prefix}", DefaultPrefix);
                ListenLoopAsync(listener, _cts.Token).SafeFireAndForget("DebugAutomationBridge.ListenLoopAsync");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "DebugAutomationBridge başlatılamadı ({Prefix}); uygulama normal çalışmaya devam ediyor.", DefaultPrefix);
                try
                {
                    _listener?.Close();
                }
                catch (Exception closeEx)
                {
                    Log.Debug(closeEx, "DebugAutomationBridge hatalı başlangıç temizliği sırasında ikincil hata.");
                }
                _listener = null;
            }
        }

        private async Task ListenLoopAsync(HttpListener listener, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && listener.IsListening)
            {
                HttpListenerContext? context = null;
                try
                {
                    context = await listener.GetContextAsync().ConfigureAwait(false);
                }
                catch (HttpListenerException) when (ct.IsCancellationRequested || !listener.IsListening)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "DebugAutomationBridge bağlantı kabulünde hata.");
                    continue;
                }

                if (context != null)
                {
                    HandleRequestAsync(context, ct).SafeFireAndForget("DebugAutomationBridge.HandleRequestAsync");
                }
            }
        }

        private async Task HandleRequestAsync(HttpListenerContext context, CancellationToken ct)
        {
            string method = context.Request.HttpMethod.ToUpperInvariant();
            string path = (context.Request.Url?.AbsolutePath ?? "/").TrimEnd('/').ToLowerInvariant();
            if (string.IsNullOrEmpty(path))
            {
                path = "/";
            }

            try
            {
                switch (method, path)
                {
                    case ("GET", "/health"):
                        await HandleHealthAsync(context.Response).ConfigureAwait(false);
                        break;

                    case ("GET", "/ui-tree"):
                        await HandleUiTreeAsync(context.Request, context.Response).ConfigureAwait(false);
                        break;

                    case ("POST", "/action"):
                        await HandleActionAsync(context.Request, context.Response).ConfigureAwait(false);
                        break;

                    case ("POST", "/navigate"):
                        await HandleNavigateAsync(context.Request, context.Response, ct).ConfigureAwait(false);
                        break;

                    case ("POST", "/screenshot"):
                        await HandleScreenshotAsync(context.Request, context.Response).ConfigureAwait(false);
                        break;

                    case ("GET", "/diagnostics"):
                        await HandleDiagnosticsAsync(context.Request, context.Response).ConfigureAwait(false);
                        break;

                    default:
                        await WriteJsonAsync(context.Response, 404, new { ok = false, error = $"Bilinmeyen uç nokta: {method} {path}" }).ConfigureAwait(false);
                        break;
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "DebugAutomationBridge istek işleme hatası: {Method} {Path}", method, path);
                await WriteJsonAsync(context.Response, 500, new { ok = false, error = ex.Message }).ConfigureAwait(false);
            }
        }

        private async Task HandleHealthAsync(HttpListenerResponse response)
        {
            var payload = await RunOnUiAsync(() =>
            {
                var window = DebugUiAutomationHelper.GetActiveTargetWindow();
                var (_, viewName, _) = InspectNavigationState();
                return new
                {
                    status = "ok",
                    pid = Environment.ProcessId,
                    activeWindow = DebugUiAutomationHelper.DescribeWindow(window),
                    activeView = viewName,
                    theme = GetCurrentThemeName()
                };
            }).ConfigureAwait(false);

            await WriteJsonAsync(response, 200, payload).ConfigureAwait(false);
        }

        private async Task HandleUiTreeAsync(HttpListenerRequest request, HttpListenerResponse response)
        {
            int maxNodes = 120;
            string? rawMax = request.QueryString["maxNodes"];
            if (!string.IsNullOrWhiteSpace(rawMax) && int.TryParse(rawMax, out int parsedMax))
            {
                maxNodes = parsedMax;
            }

            var nodes = await RunOnUiAsync(() => _uiHelper.InspectUiTree(maxNodes)).ConfigureAwait(false);
            await WriteJsonAsync(response, 200, nodes).ConfigureAwait(false);
        }

        private async Task HandleActionAsync(HttpListenerRequest request, HttpListenerResponse response)
        {
            var req = await ReadJsonBodyAsync<ActionRequestDto>(request).ConfigureAwait(false);
            if (req == null || string.IsNullOrWhiteSpace(req.Action))
            {
                await WriteJsonAsync(response, 400, new { ok = false, error = "Geçersiz istek gövdesi; 'action' zorunludur." }).ConfigureAwait(false);
                return;
            }

            var result = await RunOnUiAsync(() => _uiHelper.ExecuteAction(req.Target, req.Action, req.Value)).ConfigureAwait(false);
            await WriteJsonAsync(response, result.Ok ? 200 : 400, result).ConfigureAwait(false);
        }

        private async Task HandleNavigateAsync(HttpListenerRequest request, HttpListenerResponse response, CancellationToken ct)
        {
            var req = await ReadJsonBodyAsync<NavigateRequestDto>(request).ConfigureAwait(false) ?? new NavigateRequestDto(null, null);

            var navOutcome = await RunOnUiAsync(() =>
            {
                if (!string.IsNullOrWhiteSpace(req.Theme) &&
                    !string.Equals(req.Theme, "null", StringComparison.OrdinalIgnoreCase))
                {
                    ThemeService.ChangeTheme(req.Theme.Trim());
                }

                if (string.IsNullOrWhiteSpace(req.Route))
                {
                    return (Ok: true, Error: (string?)null);
                }

                return TryExecuteRoute(req.Route.Trim());
            }).ConfigureAwait(false);

            if (!navOutcome.Ok)
            {
                await WriteJsonAsync(response, 400, new { ok = false, error = navOutcome.Error }).ConfigureAwait(false);
                return;
            }

            await Task.Delay(150, ct).ConfigureAwait(false);

            var state = await RunOnUiAsync(() =>
            {
                var window = DebugUiAutomationHelper.GetActiveTargetWindow();
                var (_, viewName, vmName) = InspectNavigationState();
                return new
                {
                    ok = true,
                    route = req.Route,
                    activeView = viewName,
                    activeViewModel = vmName,
                    activeWindow = DebugUiAutomationHelper.DescribeWindow(window),
                    theme = GetCurrentThemeName()
                };
            }, DispatcherPriority.Background).ConfigureAwait(false);

            await WriteJsonAsync(response, 200, state).ConfigureAwait(false);
        }

        private (bool Ok, string? Error) TryExecuteRoute(string route)
        {
            var sp = _serviceProviderAccessor();
            var navService = sp?.GetService<NavigationService>()
                ?? Application.Current?.MainWindow?.DataContext as NavigationService;

            if (navService == null)
            {
                return (false, "NavigationService çözümlenemedi.");
            }

            string normalized = route.Trim().ToLowerInvariant();
            if (normalized == "login")
            {
                navService.NavigateToLogin();
                return (true, null);
            }

            if (normalized is "firstrun" or "firstrunwizard")
            {
                navService.NavigateToFirstRunWizard(1);
                return (true, null);
            }

            if (navService.CurrentView is not MainContentViewModel mainVm)
            {
                navService.NavigateToMainContent();
                mainVm = (MainContentViewModel)navService.CurrentView!;
            }

            switch (normalized)
            {
                case "dashboard":
                    mainVm.NavigateToDashboardCommand.Execute(null);
                    return (true, null);
                case "customers":
                    mainVm.NavigateToCustomersCommand.Execute(null);
                    return (true, null);
                case "interactions":
                case "customerinteractions":
                    mainVm.NavigateToCustomerInteractionsCommand.Execute(null);
                    return (true, null);
                case "manageragenda":
                case "agenda":
                    mainVm.NavigateToManagerAgendaCommand.Execute(null);
                    return (true, null);
                case "stock":
                case "products":
                case "inventory":
                    mainVm.NavigateToProductsCommand.Execute(null);
                    return (true, null);
                case "stockcount":
                    mainVm.NavigateToStockCountCommand.Execute(null);
                    return (true, null);
                case "reports":
                case "stockreports":
                    mainVm.NavigateToReportsCommand.Execute(null);
                    return (true, null);
                case "servicejobs":
                case "jobs":
                    mainVm.NavigateToServiceJobsCommand.Execute(null);
                    return (true, null);
                case "repairs":
                case "repairlist":
                    mainVm.NavigateToRepairListCommand.Execute(null);
                    return (true, null);
                case "fieldjobs":
                case "fieldjoblist":
                case "scheduler":
                    mainVm.NavigateToFieldJobListCommand.Execute(null);
                    return (true, null);
                case "routeplanning":
                    mainVm.NavigateToRoutePlanningCommand.Execute(null);
                    return (true, null);
                case "quotations":
                case "quotes":
                case "quotelist":
                case "projectquote":
                    mainVm.OpenProjectQuoteCommand.Execute(null);
                    return (true, null);
                case "quotationeditor":
                case "newquotation":
                    mainVm.OpenQuotationCommand.ExecuteAsync(null).SafeFireAndForget("DebugAutomationBridge.OpenQuotation");
                    return (true, null);
                case "directsales":
                case "pos":
                    mainVm.OpenDirectSalesCommand.Execute(null);
                    return (true, null);
                case "purchasing":
                case "purchaseorders":
                    mainVm.NavigateToPurchaseOrdersCommand.Execute(null);
                    return (true, null);
                case "suppliers":
                    mainVm.NavigateToSuppliersCommand.Execute(null);
                    return (true, null);
                case "finance":
                    mainVm.NavigateToFinanceCommand.Execute(null);
                    return (true, null);
                case "financialhealth":
                    mainVm.NavigateToFinancialHealthCommand.Execute(null);
                    return (true, null);
                case "analytics":
                    mainVm.NavigateToAnalyticsCommand.Execute(null);
                    return (true, null);
                case "users":
                    mainVm.NavigateToUsersCommand.Execute(null);
                    return (true, null);
                case "systemlogs":
                case "logs":
                    mainVm.NavigateToSystemLogsCommand.Execute(null);
                    return (true, null);
                case "settings":
                    mainVm.GoToSettingsCommand.Execute(null);
                    return (true, null);
                default:
                    return (false, $"Bilinmeyen rota: '{route}'.");
            }
        }

        private async Task HandleScreenshotAsync(HttpListenerRequest request, HttpListenerResponse response)
        {
            var req = await ReadJsonBodyAsync<ScreenshotRequestDto>(request).ConfigureAwait(false) ?? new ScreenshotRequestDto(null, false);

            string shotsDir = Path.Combine(Path.GetTempPath(), "kamatek_mcp_shots");
            Directory.CreateDirectory(shotsDir);

            string rawName = string.IsNullOrWhiteSpace(req.Filename)
                ? $"shot_{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}.png"
                : Path.GetFileName(req.Filename.Trim());

            if (!rawName.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                rawName += ".png";
            }

            string fullPath = Path.Combine(shotsDir, rawName);

            var capture = await RunOnUiAsync(() =>
            {
                var window = DebugUiAutomationHelper.GetActiveTargetWindow()
                    ?? throw new InvalidOperationException("Ekran görüntüsü alınacak aktif pencere bulunamadı.");

                window.UpdateLayout();

                double dipWidth = window.ActualWidth > 1 ? window.ActualWidth : Math.Max(window.Width, 800);
                double dipHeight = window.ActualHeight > 1 ? window.ActualHeight : Math.Max(window.Height, 600);
                var dpi = VisualTreeHelper.GetDpi(window);

                int pixelWidth = Math.Max(1, (int)Math.Round(dipWidth * dpi.DpiScaleX));
                int pixelHeight = Math.Max(1, (int)Math.Round(dipHeight * dpi.DpiScaleY));

                Visual targetVisual = (window.Content as Visual) ?? window;
                var drawingVisual = new DrawingVisual();
                using (var dc = drawingVisual.RenderOpen())
                {
                    var brush = new VisualBrush(targetVisual)
                    {
                        Stretch = Stretch.Fill
                    };
                    dc.DrawRectangle(brush, null, new Rect(0, 0, dipWidth, dipHeight));
                }

                var rtb = new RenderTargetBitmap(pixelWidth, pixelHeight, dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
                rtb.Render(drawingVisual);

                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(rtb));

                using var ms = new MemoryStream();
                encoder.Save(ms);
                return (Bytes: ms.ToArray(), Width: pixelWidth, Height: pixelHeight, WindowName: DebugUiAutomationHelper.DescribeWindow(window), Theme: GetCurrentThemeName());
            }, DispatcherPriority.Render).ConfigureAwait(false);

            await File.WriteAllBytesAsync(fullPath, capture.Bytes).ConfigureAwait(false);

            await WriteJsonAsync(response, 200, new
            {
                ok = true,
                filePath = fullPath,
                width = capture.Width,
                height = capture.Height,
                activeWindow = capture.WindowName,
                theme = capture.Theme,
                base64 = req.IncludeBase64 ? Convert.ToBase64String(capture.Bytes) : null
            }).ConfigureAwait(false);
        }

        private async Task HandleDiagnosticsAsync(HttpListenerRequest request, HttpListenerResponse response)
        {
            bool clear = string.Equals(request.QueryString["clear"], "true", StringComparison.OrdinalIgnoreCase);

            var uiState = await RunOnUiAsync(() =>
            {
                var window = DebugUiAutomationHelper.GetActiveTargetWindow();
                var (activeVm, viewName, vmName) = InspectNavigationState();
                var sp = _serviceProviderAccessor();
                bool globalBusy = sp?.GetService<LoadingViewModel>()?.IsBusy == true;
                bool vmBusy = ReadBooleanProperty(activeVm, "IsBusy") || ReadBooleanProperty(activeVm, "IsLoading");

                return new
                {
                    WindowName = DebugUiAutomationHelper.DescribeWindow(window),
                    ViewName = viewName,
                    ViewModelName = vmName,
                    IsBusy = globalBusy || vmBusy,
                    Theme = GetCurrentThemeName()
                };
            }).ConfigureAwait(false);

            var snapshot = DebugDiagnosticsCollector.Instance.GetSnapshotAndOptionallyClear(clear);

            await WriteJsonAsync(response, 200, new
            {
                status = "ok",
                activeWindow = uiState.WindowName,
                activeView = uiState.ViewName,
                activeViewModel = uiState.ViewModelName,
                isBusy = uiState.IsBusy,
                theme = uiState.Theme,
                diagnostics = snapshot
            }).ConfigureAwait(false);
        }

        private (object? ActiveViewModel, string ViewName, string ViewModelName) InspectNavigationState()
        {
            var sp = _serviceProviderAccessor();
            var nav = sp?.GetService<NavigationService>()
                ?? Application.Current?.MainWindow?.DataContext as NavigationService;

            object? current = nav?.CurrentView;
            if (current is MainContentViewModel mainVm && mainVm.CurrentView != null)
            {
                string vmType = mainVm.CurrentView.GetType().Name;
                return (mainVm.CurrentView, mainVm.CurrentViewName, vmType);
            }

            if (current != null)
            {
                string vmType = current.GetType().Name;
                return (current, vmType.Replace("ViewModel", string.Empty, StringComparison.Ordinal), vmType);
            }

            var activeWin = DebugUiAutomationHelper.GetActiveTargetWindow();
            object? winDc = activeWin?.DataContext;
            string fallbackType = winDc?.GetType().Name ?? "None";
            return (winDc, fallbackType, fallbackType);
        }

        private static bool ReadBooleanProperty(object? instance, string propertyName)
        {
            if (instance == null)
            {
                return false;
            }

            var prop = instance.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            return prop?.PropertyType == typeof(bool) && prop.GetValue(instance) is true;
        }

        private static string GetCurrentThemeName() =>
            string.IsNullOrWhiteSpace(ThemeService.CurrentThemeName) ? "PremiumLight" : ThemeService.CurrentThemeName;

        private static async Task<T> RunOnUiAsync<T>(Func<T> callback, DispatcherPriority priority = DispatcherPriority.Normal)
        {
            var dispatcher = Application.Current?.Dispatcher
                ?? throw new InvalidOperationException("WPF Application Dispatcher hazır değil.");

            if (dispatcher.CheckAccess())
            {
                return callback();
            }

            return await dispatcher.InvokeAsync(callback, priority);
        }

        private static async Task<T?> ReadJsonBodyAsync<T>(HttpListenerRequest request) where T : class
        {
            if (!request.HasEntityBody)
            {
                return null;
            }

            using var reader = new StreamReader(request.InputStream, request.ContentEncoding ?? Encoding.UTF8);
            string body = await reader.ReadToEndAsync().ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(body))
            {
                return null;
            }

            return JsonSerializer.Deserialize<T>(body, JsonOptions);
        }

        private static async Task WriteJsonAsync(HttpListenerResponse response, int statusCode, object payload)
        {
            try
            {
                byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions);
                response.StatusCode = statusCode;
                response.ContentType = "application/json; charset=utf-8";
                response.ContentLength64 = bytes.Length;
                await response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "DebugAutomationBridge HTTP yanıt yazımı sırasında ikincil hata.");
            }
            finally
            {
                try
                {
                    response.Close();
                }
                catch (Exception closeEx)
                {
                    Log.Debug(closeEx, "DebugAutomationBridge HTTP yanıt kapatma sırasında ikincil hata.");
                }
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            try
            {
                _cts.Cancel();
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "DebugAutomationBridge iptal sinyali sırasında ikincil hata.");
            }

            try
            {
                if (_listener != null)
                {
                    if (_listener.IsListening)
                    {
                        _listener.Stop();
                    }
                    _listener.Close();
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "DebugAutomationBridge kapatılırken ikincil hata.");
            }
            finally
            {
                _listener = null;
                _cts.Dispose();
            }
        }

        private sealed record ActionRequestDto(string? Target, string? Action, string? Value);
        private sealed record NavigateRequestDto(string? Route, string? Theme);
        private sealed record ScreenshotRequestDto(string? Filename, bool IncludeBase64);
    }
}
#endif
