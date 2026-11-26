using System;

using System.Diagnostics;

using System.Net.Http;

using System.Threading.Tasks;

using System.Windows;

using Microsoft.Extensions.DependencyInjection;

using Microsoft.Extensions.Hosting;

using KamatekCrm.Services;
using KamatekCrm.Settings;

using KamatekCrm.Extensions;

using KamatekCrm.Helpers;

using KamatekCrm.ViewModels;

using Microsoft.Extensions.Configuration;

using KamatekCrm.Configuration;

using KamatekCrm.Diagnostics.Core;

using Serilog;

using Wpf.Ui.Appearance;

using Microsoft.EntityFrameworkCore;

using KamatekCrm.Infrastructure.Data;

using KamatekCrm.Infrastructure.Services;
using KamatekCrm.Shared.Services;
using KamatekCrm.Views;



namespace KamatekCrm

{

    /// <summary>

    /// WPF masaüstü uygulaması başlangıç noktası.

    /// API kullanılmadan, uygulama ve altyapı katmanları üzerinden PostgreSQL'e bağlanır.

    /// </summary>

    public partial class App : System.Windows.Application

    {

        private IHost? _host;

        private readonly System.Threading.CancellationTokenSource _appCts = new();

#if DEBUG
        private Diagnostics.DebugAutomationBridge? _debugAutomationBridge;
#endif



        public static IServiceProvider ServiceProvider { get; private set; } = null!;



        public static KamatekCrm.Shared.Models.User? CurrentUser { get; set; }



        public App()

        {

            // Global WPF exception handler'ları en erken aşamada bağla

            DispatcherUnhandledException += OnDispatcherUnhandledException;

            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        }



        protected override async void OnStartup(StartupEventArgs e)

        {

            // Velopack Bootstrap — Uygulama başlangıcının en ilk adımı (Installer/Hook yönetimi)

            try

            {

                Velopack.VelopackApp.Build().Run();

            }

            catch (Exception ex)

            {

                System.Diagnostics.Debug.WriteLine($"Velopack bootstrap: {ex.Message}");

            }



            // WPF binding formatları işletim sistemi dilinden bağımsız olarak Türkçe CRM

            // bağlamında para, sayı ve tarih üretmelidir.

            var applicationCulture = System.Globalization.CultureInfo.GetCultureInfo("tr-TR");

            System.Globalization.CultureInfo.DefaultThreadCurrentCulture = applicationCulture;

            System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = applicationCulture;

            FrameworkElement.LanguageProperty.OverrideMetadata(

                typeof(FrameworkElement),

                new FrameworkPropertyMetadata(

                    System.Windows.Markup.XmlLanguage.GetLanguage(applicationCulture.IetfLanguageTag)));



            // PostgreSQL Legacy Timestamp Behavior (Fix for Kind=Local error)

            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

            // Windows TSF / IME TextStore GetVisualInfo (0x80040206 - TS_E_NOLAYOUT) hatasını global düzeyde engelle
            AppContext.SetSwitch("Switch.System.Windows.Input.Stylus.DisableStylusAndTouchSupport", true);
            try
            {
                System.Windows.Input.InputMethod.IsInputMethodEnabledProperty.OverrideMetadata(
                    typeof(System.Windows.Controls.TextBox),
                    new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits));
                System.Windows.Input.InputMethod.IsInputMethodEnabledProperty.OverrideMetadata(
                    typeof(System.Windows.Controls.RichTextBox),
                    new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits));
                // Not: PasswordBox kendi statik kurucusunda IsInputMethodEnabledProperty=false metadata'sını zaten kaydeder.
            }
            catch (Exception catchEx) { Serilog.Log.Debug(catchEx, "İşlem sırasında beklenen/ikincil hata yakalandı: {Source}", "App.xaml.cs"); }



            // WPF bağlama hatalarını geliştirme sırasında görünür kıl (yalnızca DEBUG derlemeleri)

            // Tüm hatalar Debug çıktısına; "TwoWay or OneWayToSource" sınıfı ayrıca popup'a düşer.

#if DEBUG

            PresentationTraceSources.Refresh();

            PresentationTraceSources.DataBindingSource.Listeners.Add(new Diagnostics.BindingErrorTraceListener());

            PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Warning;

            Diagnostics.DebugDiagnosticsCollector.Instance.AttachWpfTraceListeners();

#endif



            // Logging'i ilk iş olarak yapılandır

            LoggingConfiguration.ConfigureLogging();

            // Oturum boyunca taşınacak korelasyon kimliği. UI işlemleri kendi kapsamlarını
            // açtığında bu değer geçici olarak ezilir (CorrelationContext.BeginScope).
            CorrelationContext.Set(CorrelationContext.NewId("APP"));

            

            try

            {

                Log.Information("=== KamatekCRM Desktop Starting (Direct Database Mode) ===");

                

                // ÖNCE base.OnStartup çağrılmalı - WPF-UI için önemli!

                base.OnStartup(e);



                // WPF-UI Theme - OnStartup'tan SONRA uygula

                ApplicationThemeManager.Apply(ApplicationTheme.Light);



                // Kendi tema ve stil yapılandırmamızı uygula

                // WPF-UI'ın enjekte ettiği ScrollBar/ScrollViewer stillerini ezer

                ThemeService.Initialize();



                // Host Builder'ı yapılandır — SADECE WPF DI, web server YOK

                _host = Host.CreateDefaultBuilder()

                    // dispose:false → logger'ı host değil, App.OnExit sonundaki Log.CloseAndFlush() kapatır.
                    // Aksi halde _host.Dispose() sonrası yazılan kapanış logları kaybolur.
                    .UseSerilog(dispose: false)

                    .ConfigureAppConfiguration((context, config) =>

                    {

                        var appSettingsFile = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");

                        Security.DataProtectionHelper.MigrateAppSettingsIfNecessary(appSettingsFile);

                        config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);



                        // Bellekteki konfigürasyonda şifreli bağlantı dizesini güvenli olarak çöz

                        var tempConfig = config.Build();

                        var rawConn = tempConfig.GetConnectionString("PostgreSQL")

                            ?? tempConfig["DatabaseSettings:ConnectionStrings:PostgreSQL"];

                        if (!string.IsNullOrEmpty(rawConn))

                        {

                            var decryptedConn = Security.DataProtectionHelper.UnprotectConnectionString(rawConn);

                            config.AddInMemoryCollection(new System.Collections.Generic.Dictionary<string, string?>

                            {

                                ["DatabaseSettings:ConnectionStrings:PostgreSQL"] = decryptedConn,

                                ["ConnectionStrings:PostgreSQL"] = decryptedConn

                            });

                        }

                    })

                    .ConfigureServices((context, services) =>

                    {

                        // JWT Token Storage Removed

                        

                        // WPF servisleri kaydet (DB, ViewModels, Navigation vs.)

                        services.AddApplicationServices(context.Configuration);

                        

                        // MainWindow'u DI container'a ekle

                        services.AddTransient<MainWindow>();



                        // Arka plan servisleri (IHostedService)

                        services.AddSingleton<NetworkDiscoveryService>();

                        services.AddHostedService(provider => provider.GetRequiredService<NetworkDiscoveryService>());



                        services.AddSingleton<ConnectionHeartbeatService>();

                        services.AddHostedService(provider => provider.GetRequiredService<ConnectionHeartbeatService>());

                    })

                    .Build();



                // Service Provider'ı global erişime aç

                ServiceProvider = _host.Services;



                // Exception handler'lar App() kurucusunda bir kez bağlanır.
                // Burada tekrar bağlanırsa her hata iki kez loglanır ve iki kez bildirim gösterilir.



                await _host.StartAsync();

#if DEBUG
                _debugAutomationBridge = new Diagnostics.DebugAutomationBridge(() => _host?.Services);
                _debugAutomationBridge.Start();
#endif

                // MainWindow açılış karar ağacı: ilk kurulum tamamlanmamışsa veya --first-run geçildiyse LoginView asla ilk ekran olmasın
                var mainWindow = _host.Services.GetRequiredService<MainWindow>();
                MainWindow = mainWindow;

                var navigationService = _host.Services.GetRequiredService<NavigationService>();
                var connectionProvider = _host.Services.GetRequiredService<IDatabaseConnectionProvider>();
                bool forceFirstRun = e.Args.Any(a => string.Equals(a, "--first-run", StringComparison.OrdinalIgnoreCase));

                if (!string.IsNullOrWhiteSpace(AppSettings.PostgreSqlConnectionString) &&
                    string.IsNullOrWhiteSpace(connectionProvider.GetConnectionString()))
                {
                    connectionProvider.UpdateConnectionString(AppSettings.PostgreSqlConnectionString);
                }

                bool hasConfiguredConnection = !string.IsNullOrWhiteSpace(connectionProvider.GetConnectionString());
                Log.Information("Yerel kurulum ayar dosyası: {SettingsPath} (ForceFirstRun={Force})",
                    AppSettings.LocalRuntimeSettingsFilePath, forceFirstRun);

                if (forceFirstRun || (!AppSettings.IsFirstRunCompleted && !hasConfiguredConnection))
                {
                    navigationService.NavigateToFirstRunWizard(startStep: 1);
                    Log.Information(
                        "Startup route: {Route} (IsFirstRunCompleted={Flag}, HasConn={HasConn}, UserCount={Count})",
                        "FirstRunWizardView(Step1)",
                        AppSettings.IsFirstRunCompleted,
                        hasConfiguredConnection,
                        0);
                    mainWindow.Show();
                    await CheckStartupUpdatesAsync(_appCts.Token);
                    return;
                }

                navigationService.NavigateToLogin();

                mainWindow.Show();
                Log.Information("Desktop application window opened; starting non-blocking database bootstrap.");

                var loginViewModel = navigationService.CurrentView as LoginViewModel;
                await InitializeDatabaseOnStartupAsync(loginViewModel, mainWindow, _appCts.Token);

                // Açılış sonrasında ana pencere ve login akışını bloklamadan güncelleme denetimi (5s gecikmeli, await edilebilir)
                await CheckStartupUpdatesAsync(_appCts.Token);
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Uygulama başlatılırken kritik hata");
                MessageBox.Show(
                    $"Uygulama başlatılırken hata oluştu:\n\n{ex.Message}\n\nDetay: {ex.InnerException?.Message}",
                    "Başlatma Hatası",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown();
            }
        }

        /// <summary>
        /// Açılışta veritabanı bağlantı ve şema kontrolünü UI'ı kilitlemeden, 5 saniyelik zaman aşımı ile yürütür.
        /// Bağlantı yapılandırılmamışsa veya sunucu erişilemezse uygulamayı kapatmaz; kullanıcıyı Ağ Ayarları'na yönlendirir.
        /// </summary>
        private async Task InitializeDatabaseOnStartupAsync(LoginViewModel? loginViewModel, Window mainWindow, CancellationToken appCancellationToken)
        {
            var connectionProvider = ServiceProvider!.GetRequiredService<IDatabaseConnectionProvider>();
            var navigationService = ServiceProvider!.GetRequiredService<NavigationService>();

            // 1) Yerel yönetilen PostgreSQL modu aktifse veritabanını sessizce ayağa kaldır
            if (AppSettings.IsManagedLocalPostgres)
            {
                loginViewModel?.SetStartupStatus("Yerel veritabanı motoru başlatılıyor...", isSearching: true, isFound: false, isNotFound: false);
                var managedPg = ServiceProvider!.GetRequiredService<IManagedPostgresService>();
                var progress = new Progress<string>(msg =>
                    loginViewModel?.SetStartupStatus(msg, isSearching: true, isFound: false, isNotFound: false));

                var pgResult = await managedPg.EnsureProvisionedAndRunningAsync(
                    AppSettings.AllowLanAccess,
                    progress,
                    appCancellationToken);

                if (pgResult.Success)
                {
                    connectionProvider.UpdateConnectionString(pgResult.ConnectionString);
                    connectionProvider.SetServerIp("127.0.0.1");
                }
                else
                {
                    Log.Warning("Yönetilen yerel PostgreSQL başlatılamadı: {Error}", pgResult.ErrorMessage);
                    loginViewModel?.SetStartupStatus(
                        pgResult.ErrorMessage ?? "Yerel veritabanı başlatılamadı.",
                        isSearching: false,
                        isFound: false,
                        isNotFound: true);
                    return;
                }
            }
            else if (!string.IsNullOrWhiteSpace(AppSettings.PostgreSqlConnectionString) &&
                     string.IsNullOrWhiteSpace(connectionProvider.GetConnectionString()))
            {
                connectionProvider.UpdateConnectionString(AppSettings.PostgreSqlConnectionString);
            }

            // 2) Kayıtlı geçerli bağlantı yoksa -> İlk Kurulum Sihirbazı (Adım 1)
            if (string.IsNullOrWhiteSpace(connectionProvider.GetConnectionString()))
            {
                Log.Information(
                    "Startup route: {Route} (IsFirstRunCompleted={Flag}, HasConn={HasConn}, UserCount={Count})",
                    "FirstRunWizardView(Step1)",
                    AppSettings.IsFirstRunCompleted,
                    false,
                    0);
                navigationService.NavigateToFirstRunWizard(startStep: 1);
                return;
            }

            loginViewModel?.SetStartupStatus("Sunucu aranıyor...", isSearching: true, isFound: false, isNotFound: false);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(appCancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(10));

            try
            {
                loginViewModel?.SetStartupStatus("Veritabanı kontrol ediliyor...", isSearching: true, isFound: false, isNotFound: false);

                var databaseInitializer = ServiceProvider!.GetRequiredService<IDatabaseInitializationService>();
                var initializationResult = await Task.Run(
                    () => databaseInitializer.InitializeAsync(timeoutCts.Token, seedDefaultAdminIfEmpty: false),
                    timeoutCts.Token);

                connectionProvider.SetConnectionState(true);
                var serverLabel = string.IsNullOrWhiteSpace(connectionProvider.CurrentServerIp)
                    ? "Yerel Sunucu"
                    : connectionProvider.CurrentServerIp;

                loginViewModel?.SetStartupStatus(
                    $"Veritabanı hazır ({serverLabel})",
                    isSearching: false,
                    isFound: true,
                    isNotFound: false);

                EventAggregator.Instance.Publish(new DatabaseConnectionRestoredEvent());

                // 3) Bağlantı tamam ama Users tablosu boşsa -> İlk Kurulum Sihirbazı (Adım 2)
                if (!initializationResult.HasAnyUsers)
                {
                    Log.Information(
                        "Startup route: {Route} (IsFirstRunCompleted={Flag}, HasConn={HasConn}, UserCount={Count})",
                        "FirstRunWizardView(Step2)",
                        AppSettings.IsFirstRunCompleted,
                        true,
                        initializationResult.UserCount);
                    navigationService.NavigateToFirstRunWizard(startStep: 2);
                    return;
                }

                // 4) AutoLoginEnabled aktif ve DPAPI oturumu geçerliyse -> doğrudan MainContentView
                if (AppSettings.TryReadAutoLoginSession(out int autoUserId, out string autoUsername))
                {
                    var authService = ServiceProvider!.GetRequiredService<IAuthService>();
                    if (await authService.TryAutoLoginAsync(autoUserId, autoUsername))
                    {
                        Log.Information(
                            "Startup route: {Route} (IsFirstRunCompleted={Flag}, HasConn={HasConn}, UserCount={Count})",
                            "MainContentView(AutoLogin)",
                            AppSettings.IsFirstRunCompleted,
                            true,
                            initializationResult.UserCount);
                        navigationService.NavigateToMainContent();
                        return;
                    }
                    AppSettings.ClearAutoLoginSession();
                }

                Log.Information(
                    "Startup route: {Route} (IsFirstRunCompleted={Flag}, HasConn={HasConn}, UserCount={Count})",
                    "LoginView",
                    AppSettings.IsFirstRunCompleted,
                    true,
                    initializationResult.UserCount);

                if (initializationResult.AdminCreated && !string.IsNullOrWhiteSpace(initializationResult.TemporaryAdminPassword))
                {
                    TemporaryPasswordDialog.ShowDialogSafe(
                        username: "admin",
                        temporaryPassword: initializationResult.TemporaryAdminPassword,
                        title: "Güvenli İlk Kurulum",
                        headerTitle: "İlk Yönetici Hesabı Oluşturuldu",
                        subtitle: "Bu geçici şifre yalnızca bir kez gösterilir. Giriş yaptıktan sonra değiştirmeniz istenecektir.",
                        owner: mainWindow);
                }
            }
            catch (OperationCanceledException) when (!appCancellationToken.IsCancellationRequested)
            {
                Log.Warning("Veritabanı başlangıç kontrolü zaman aşımına ulaştı.");
                connectionProvider.SetConnectionState(false);
                if (!AppSettings.IsFirstRunCompleted)
                {
                    Log.Information(
                        "Startup route: {Route} (IsFirstRunCompleted={Flag}, HasConn={HasConn}, UserCount={Count})",
                        "FirstRunWizardView(Step1-TimeoutFallback)",
                        AppSettings.IsFirstRunCompleted,
                        true,
                        0);
                    navigationService.NavigateToFirstRunWizard(startStep: 1);
                    return;
                }

                loginViewModel?.SetStartupStatus(
                    "Sunucu yanıt vermedi. Ağ Ayarları'ndan bağlantıyı yapılandırabilirsiniz.",
                    isSearching: false,
                    isFound: false,
                    isNotFound: true);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Veritabanı otomatik kurulumu sırasında hata oluştu. Lütfen bağlantı ayarlarını kontrol edin.");
                connectionProvider.SetConnectionState(false);
                if (!AppSettings.IsFirstRunCompleted)
                {
                    Log.Information(
                        "Startup route: {Route} (IsFirstRunCompleted={Flag}, HasConn={HasConn}, UserCount={Count})",
                        "FirstRunWizardView(Step1-ErrorFallback)",
                        AppSettings.IsFirstRunCompleted,
                        true,
                        0);
                    navigationService.NavigateToFirstRunWizard(startStep: 1);
                    return;
                }

                loginViewModel?.SetStartupStatus(
                    "Veritabanına bağlanılamadı. Lütfen Ağ Ayarları'nı kontrol edin.",
                    isSearching: false,
                    isFound: false,
                    isNotFound: true);
            }
        }



        /// <summary>

        /// Handles unhandled UI thread exceptions gracefully.

        /// Bu handler zaten Dispatcher (UI) thread üzerinde çalışır.

        /// </summary>

        private static bool IsTextStoreLayoutException(Exception? ex)

        {

            while (ex != null)

            {

                if (ex is System.Runtime.InteropServices.COMException comEx &&

                    (comEx.ErrorCode == unchecked((int)0x80040206) ||

                     comEx.HResult == unchecked((int)0x80040206) ||

                     comEx.StackTrace?.Contains("TextStore.GetVisualInfo") == true ||

                     comEx.Message.Contains("Kullanılabilir yerleşim yok") ||

                     comEx.Message.Contains("no layout available", StringComparison.OrdinalIgnoreCase)))

                {

                    return true;

                }



                if (ex.StackTrace?.Contains("TextStore.GetVisualInfo") == true ||

                    ex.Message.Contains("Kullanılabilir yerleşim yok"))

                {

                    return true;

                }



                ex = ex.InnerException;

            }

            return false;

        }



        private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs args)

        {

            var exception = args.Exception;



            // ── Ağ hataları ──

            if (exception is HttpRequestException httpEx)

            {

                Log.Error(httpEx, "HTTP Request Exception - Server unreachable");

                ShowErrorToast("Sunucuya bağlanılamıyor. İnternet bağlantınızı kontrol edin.");

                args.Handled = true;

                return;

            }



            // ── Timeout / iptal hataları ──

            if (exception is TaskCanceledException or OperationCanceledException)

            {

                Log.Warning(exception, "Task cancelled/timeout");

                ShowErrorToast("İşlem zaman aşımına uğradı. Lütfen tekrar deneyin.");

                args.Handled = true;

                return;

            }



            // ── WPF TSF (Text Services Framework) / TextStore layout yarış durumu ──

            // Windows IME, dokunmatik klavye, odak değişimi veya pencere geçişlerinde görsel ağaç hesaplanırken

            // TextStore.GetVisualInfo COMException 0x80040206 (TS_E_NOLAYOUT / "Kullanılabilir yerleşim yok") fırlatabilir.

            if (IsTextStoreLayoutException(exception))

            {

                Log.Warning(exception, "WPF TextStore TS_E_NOLAYOUT (0x80040206) yerleşim yarış durumu yakalandı ve güvenli şekilde bastırıldı.");

                args.Handled = true;

                return;

            }



            // ── Diğer tüm hatalar ──

            var correlationId = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

            Log.Error(exception, "Unhandled UI exception [CorrelationId: {CorrelationId}]: {Message}", correlationId, exception.Message);

            ShowErrorToast(FormatSafeGlobalErrorMessage(exception, "Beklenmeyen bir hata oluştu", correlationId));



            // Uygulamanın çökmesini engelle — hata loglandı ve kullanıcıya bildirildi

            args.Handled = true;

        }

        private static bool IsDatabaseException(Exception? ex)
        {
            var current = ex;
            while (current != null)
            {
                if (current is Npgsql.PostgresException
                    or Npgsql.NpgsqlException
                    or Microsoft.EntityFrameworkCore.DbUpdateException
                    || ToastService.ContainsRawDatabaseError(current.Message))
                {
                    return true;
                }
                current = current.InnerException;
            }
            return false;
        }

        private static string FormatSafeGlobalErrorMessage(Exception? ex, string prefix, string correlationId)
        {
            if (IsDatabaseException(ex))
            {
                return $"{prefix}: Veritabanı işlemi gerçekleştirilemedi. Lütfen tekrar deneyin. (Ref: {correlationId})";
            }
            return $"{prefix}. Lütfen tekrar deneyin. (Ref: {correlationId})";
        }



        /// <summary>

        /// Handles unhandled non-UI (AppDomain) exceptions.

        /// DİKKAT: Bu handler HERHANGİ bir thread'den tetiklenebilir.

        /// UI güncellemeleri Dispatcher üzerinden yapılmalıdır.

        /// </summary>

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs args)

        {

            var exception = args.ExceptionObject as Exception;
            var correlationId = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

            Log.Fatal(exception, "Unhandled domain exception [CorrelationId: {CorrelationId}] - IsTerminating: {IsTerminating}", correlationId, args.IsTerminating);

            // Kapanmadan önce redakte edilmiş küçük bir çökme izi bırak (dump değil).
            var crashArtifact = CrashArtifactWriter.Write(exception, "AppDomain.UnhandledException", args.IsTerminating);
            if (crashArtifact != null)
            {
                Log.Debug("Crash artifact written: {Path}", crashArtifact);
            }



            if (args.IsTerminating)

            {

                // Uygulama kapanıyor — Dispatcher üzerinden son bir bilgilendirme yap.

                // BeginInvoke kullanıyoruz çünkü thread zaten ölüyor olabilir.

                try

                {

                    Current?.Dispatcher?.Invoke(() =>

                    {

                        MessageBox.Show(

                            FormatSafeGlobalErrorMessage(exception, "Kritik bir hata oluştu ve uygulama kapanıyor", correlationId),

                            "Kritik Hata",

                            MessageBoxButton.OK,

                            MessageBoxImage.Error);

                    });

                }

                catch (Exception catchEx) { Serilog.Log.Debug(catchEx, "İşlem sırasında beklenen/ikincil hata yakalandı: {Source}", "App.xaml.cs"); }

            }

            else

            {

                ShowErrorToast(FormatSafeGlobalErrorMessage(exception, "Arka plan hatası", correlationId));

            }

        }



        /// <summary>

        /// Handles unobserved task exceptions (await edilmemiş Task'lardaki hatalar).

        /// DİKKAT: Bu handler Finalizer thread'inden tetiklenir — kesinlikle UI thread değildir.

        /// </summary>

        private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs args)

        {

            // Flatten ile iç içe AggregateException'ları düzleştir

            var flattenedException = args.Exception?.Flatten();

            var innerException = flattenedException?.InnerExceptions.Count > 0

                ? flattenedException.InnerExceptions[0]

                : args.Exception;



            var correlationId = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

            Log.Error(innerException, "Unobserved task exception [CorrelationId: {CorrelationId}]: {Message}", correlationId, innerException?.Message);



            // GC'nin process'i sonlandırmasını engelle

            args.SetObserved();



            // Kullanıcıya thread-safe bildirim gönder

            ShowErrorToast(FormatSafeGlobalErrorMessage(innerException, "Arka plan işleminde hata", correlationId));

        }



        /// <summary>

        /// Thread-safe hata bildirimi.

        /// Hangi thread'den çağrılırsa çağrılsın, UI güncellemesi her zaman

        /// Dispatcher (UI thread) üzerinden yapılır.

        /// </summary>

        private void ShowErrorToast(string message)

        {

            try

            {

                // Dispatcher erişilebilir mi kontrol et

                var dispatcher = Current?.Dispatcher;

                if (dispatcher == null || dispatcher.HasShutdownStarted)

                {

                    // Uygulama kapanıyorsa sadece logla

                    Log.Warning("ShowErrorToast called during shutdown: {Message}", message);

                    return;

                }



                // Her zaman UI thread üzerinden çalıştır (fire-and-forget, bloklama yok)

                dispatcher.BeginInvoke(new Action(() =>

                {

                    try

                    {

                        var toastService = ServiceProvider?.GetService(typeof(ToastService)) as ToastService;

                        if (toastService != null)

                        {

                            toastService.ShowError(message);

                        }

                        else

                        {

                            // ToastService henüz hazır değilse fallback MessageBox

                            MessageBox.Show(message, "Hata", MessageBoxButton.OK, MessageBoxImage.Warning);

                        }

                    }

                    catch (Exception ex)

                    {

                        // Toast gösterimi de başarısız olursa sadece logla — sonsuz döngü önleme

                        Log.Warning(ex, "Failed to show error toast: {OriginalMessage}", message);

                    }

                }));

            }

            catch (Exception ex)

            {

                // Dispatcher erişimi bile başarısız olursa (uygulama çöküyorsa)

                Log.Warning(ex, "ShowErrorToast completely failed: {Message}", message);

            }

        }



        /// <summary>

        /// Saves JWT token after successful login

        /// </summary>

        public static async Task SaveTokenAsync(string token)

        {

            await Task.CompletedTask;

        }



        /// <summary>

        /// Clears JWT token on logout

        /// </summary>

        public static async Task ClearTokenAsync()

        {

            await Task.CompletedTask;

        }



        /// <summary>

        /// Ana pencere açıldıktan sonra UI mesaj döngüsünü bloklamadan 5s gecikmeli güncelleme taraması yapar.

        /// </summary>

        private async Task CheckStartupUpdatesAsync(System.Threading.CancellationToken cancellationToken)

        {

            try

            {

                await Task.Delay(5000, cancellationToken);



                var updateService = _host?.Services.GetService<Services.Update.IUpdateService>();

                if (updateService != null && updateService.GetSettings().CheckForUpdatesOnStartup)

                {

                    var update = await updateService.CheckForUpdatesAsync(isAutoCheck: true, ct: cancellationToken);

                    if (update != null && !cancellationToken.IsCancellationRequested)

                    {

                        var updateDialog = new Views.UpdateNotificationWindow(updateService)

                        {

                            Owner = MainWindow

                        };

                        updateDialog.ShowDialog();

                    }

                }

            }

            catch (OperationCanceledException)

            {

                Log.Information("Startup update check cancelled.");

            }

            catch (Exception updateEx)

            {

                Log.Warning(updateEx, "Background startup update check failed silently.");

            }

        }



        protected override async void OnExit(ExitEventArgs e)

        {

            try

            {

                Log.Information("Application shutting down...");



                // Aktif arka plan güncelleme taramasını iptal et

#if DEBUG
                try
                {
                    _debugAutomationBridge?.Dispose();
                    _debugAutomationBridge = null;
                }
                catch (Exception bridgeEx)
                {
                    Serilog.Log.Debug(bridgeEx, "DebugAutomationBridge kapatılırken ikincil hata: {Source}", "App.xaml.cs");
                }
#endif

                try

                {

                    _appCts.Cancel();

                    _appCts.Dispose();

                }

                catch (Exception catchEx) { Serilog.Log.Debug(catchEx, "İşlem sırasında beklenen/ikincil hata yakalandı: {Source}", "App.xaml.cs"); }

                

                // Uygulama kapanırken otomatik yedek al (DI üzerinden)

                if (_host != null)

                {

                    try

                    {

                        var updateService = _host.Services.GetService<Services.Update.IUpdateService>();

                        if (updateService != null && updateService.IsUpdateDownloaded && updateService.GetSettings().InstallOnClose)

                        {

                            Log.Information("Applying downloaded update on application exit...");

                            updateService.ApplyUpdateAndRestart();

                        }

                    }

                    catch (Exception updateEx)

                    {

                        Log.Warning(updateEx, "Failed to apply update on application exit");

                    }



                    try

                    {

                        using var backupScope = _host.Services.CreateScope();

                        var backupService = backupScope.ServiceProvider.GetRequiredService<IBackupService>();

                        backupService.BackupDatabase();

                    }

                    catch (Exception backupEx)

                    {

                        Log.Warning(backupEx, "Yedekleme sırasında hata oluştu (kritik değil)");

                    }

                    

                    var managedPostgres = _host.Services.GetService<IManagedPostgresService>();

                    await _host.StopAsync();

                    _host.Dispose();

                    if (managedPostgres is not null)
                    {
                        await managedPostgres.StopIfSinglePcModeAsync();
                    }

                }



                // Alt süreçlerin ve arka plan araçlarının (pg_dump, pg_restore vb.) güvenli şekilde sonlandırılması

                try

                {

                    ProcessManager.StopServices();

                }

                catch (Exception procEx)

                {

                    Log.Warning(procEx, "Alt süreçler sonlandırılırken hata oluştu");

                }



                Log.Information("=== KamatekCRM Desktop Stopped ===");

            }

            catch (Exception ex)

            {

                Log.Error(ex, "Exit cleanup failed");

                Debug.WriteLine($"Exit cleanup failed: {ex.Message}");

            }

            finally

            {

                try { ProcessManager.StopServices(); } catch (Exception catchEx) { Serilog.Log.Debug(catchEx, "İşlem sırasında beklenen/ikincil hata yakalandı: {Source}", "App.xaml.cs"); }

                Log.CloseAndFlush();

                base.OnExit(e);

            }

        }

    }

}



