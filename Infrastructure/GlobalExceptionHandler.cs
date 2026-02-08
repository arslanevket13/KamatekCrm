using System.Windows;
using Microsoft.Extensions.Logging;
using Serilog;

namespace KamatekCrm.Infrastructure
{
    public static class GlobalExceptionHandler
    {
        private static Microsoft.Extensions.Logging.ILogger? _logger;

        public static void Initialize(Microsoft.Extensions.Logging.ILogger? logger = null)
        {
            _logger = logger;

            // Unhandled Exception Handler (non-UI thread)
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

            // Dispatcher Unhandled Exception (UI thread)
            System.Windows.Application.Current.DispatcherUnhandledException += OnDispatcherUnhandledException;

            // Task Unhandled Exception
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

            Log.Information("Global exception handlers registered");
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var exception = e.ExceptionObject as Exception;
            
            Log.Fatal(exception, "Unhandled exception occurred");
            _logger?.LogCritical(exception, "Unhandled exception in AppDomain");

            if (e.IsTerminating)
            {
                Log.Fatal("Application is terminating due to unhandled exception");
                
                MessageBox.Show(
                    $"Kritik bir hata oluştu ve uygulama kapatılacak:\n\n{exception?.Message}\n\nLütfen log dosyalarını kontrol edin.",
                    "Kritik Hata",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private static void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            Log.Error(e.Exception, "Unhandled exception in UI thread");
            _logger?.LogError(e.Exception, "Dispatcher unhandled exception");

            var result = MessageBox.Show(
                $"Beklenmeyen bir hata oluştu:\n\n{e.Exception.Message}\n\nUygulamayı kapatmak ister misiniz?",
                "Hata",
                MessageBoxButton.YesNo,
                MessageBoxImage.Error);

            if (result == MessageBoxResult.No)
            {
                // Hatayı handled olarak işaretle, uygulama çalışmaya devam etsin
                e.Handled = true;
            }
            else
            {
                System.Windows.Application.Current.Shutdown(1);
            }
        }

        private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            Log.Error(e.Exception, "Unobserved task exception");
            _logger?.LogError(e.Exception, "Unobserved task exception");

            // Exception'ı "observed" olarak işaretle (finalizer'da crash olmaması için)
            e.SetObserved();
        }
    }
}
