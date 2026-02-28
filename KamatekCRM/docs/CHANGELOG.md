## v11.5 — Glassmorphism UI & Port Stability (2026-02-28)
- **UI/UX: Glassmorphism**: Standardized premium Glassmorphism effect across `DashboardView` and `MainContentView` using semi-transparent surfaces and blurred backgrounds.
- **Theme Standardization**: Added `ThemeTextPrimary`, `ThemeCardBackground`, and corresponding Dark variants to `CustomTheme.xaml` for consistent dashboard rendering.
- **API: Port Enforcement**: Updated `KamatekCrm.API/appsettings.json` to explicitly listen on Port 5050 via Kestrel configuration, ensuring WPF-API connectivity.

## v11.4 — Architecture Strengthening & UI Polish (2026-02-26)
- **Dumb Client Enforcement**: Refactored `App.xaml.cs` to strictly operate as a client, removing all legacy server-side logic and ensuring 100% adherence to the Hybrid .NET 9 architecture.
- **Fluent UI Enhancements**: Added `PulseAnimation` and `ProgressRing` styles to `CustomTheme.xaml` for better asynchronous feedback.
- **PostgreSQL Stability**: Enforced `Npgsql.EnableLegacyTimestampBehavior` in WPF to match Blazor Server's UTC strictness.
- **Cleanup**: Purged redundant scratch files and updated root `.gitignore`.

## v11.3 — Infrastructure Update & Customer Management (2026-02-24)
- **Git Migration**: Moved repository root to solution level (`C:\Antigravity Proje`) to track WPF, API, and Web projects simultaneously.
- **Customer Management**: Added `CustomerAddViewModel`, `QuickCustomerAddViewModel` and corresponding Windows for rich CRM functionality.
- **Quick-Add Actions**: Implemented `QuickNewProductForPurchaseViewModel` for streamlined procurement workflows.
- **PostgreSQL Migrations**:
  - `RemoveWalkInCustomerSeed`: Cleaned up initial seed data.
  - `AddCustomerLoyaltyAndPosReceiptFields`: Added loyalty tracking and physical receipt metadata.
  - `AddCustomerSegmentAndActivities`: Implemented granular customer segmentation and CRM activity logging.
  - `AddServiceJobSlaAndTechnicianFields`: Enhanced SLA tracking for field service operations.

## v11.2 — ERP Modules Phase 3: WPF API Services (2026-02-21)
- **PosApiService**: HttpClient-based POS transaction processing and product search.
- **PurchaseApiService**: HttpClient-based purchase invoice processing.
- **ProductApiService**: HttpClient-based product listing and multipart image upload.
- **DI**: Registered `IPosApiService`, `IPurchaseApiService`, `IProductApiService` in WPF DI container.

### POS API
- **Service**: `PosService` — Atomic transaction processing (stock deduction, split payments, cash transaction recording).
- **Controller**: `POST /api/pos/transaction`, `GET /api/pos/products/search?q=`.
### Purchasing API
- **Service**: `PurchaseService` — Invoice processing with Moving Average Cost (MAC/WAC), supplier balance update.
- **Controller**: `POST /api/purchase/invoice`.
### Product Images API
- **Service**: `ProductImageService` — WebP compression (< 200KB), auto-delete old images.
- **Controller**: `POST /api/product/{id}/image`, `GET /api/product?q=&page=&pageSize=`.

### POS (Point of Sale)
- **Entity**: Enriched `PosTransaction` with split payments (Cash/Card), financial breakdown, cashier audit trail.
- **Entity**: Enriched `PosTransactionLine` with row-level discounts, per-line VAT, product name snapshots.
- **DTOs**: `PosTransactionDto`, `PosTransactionLineDto`, `PosTransactionResultDto`.
### Purchasing
- **Entity**: Enriched `PurchaseInvoice` with accounts payable, OCR integration, payment status tracking.
- **Entity**: Enriched `PurchaseInvoiceLine` with moving average cost audit trail, per-line VAT.
- **DTOs**: `PurchaseInvoiceDto`, `PurchaseInvoiceLineDto`, `PurchaseInvoiceResultDto`.
### Product Images
- **DTOs**: `ProductListDto` (lightweight for POS search), `ProductImageUploadResultDto`.
### Infrastructure
- **Enums**: `PosTransactionStatus`, `PurchaseInvoicePaymentStatus`.
- **DbContext**: Decimal precision (18,2/18,4), unique indexes, Barcode index, Walk-in Customer seed.
- **Migration**: `ErpModulesPhase1` scaffolded (auto-applies on API startup).

- **Database**: Applied EF Core migration `AddErpMajorUpdateComponents` to sync PostgreSQL schema with new ERP entities and properties (`AverageCost`, `ImagePath`, etc.).
- **Login UI**: Implemented modern Glassmorphism (Frosted Glass) effect using semi-transparent surfaces and blur.
- **Fluent Design**: Applied Windows 11 style rounded corners and bottom-accented inputs.
- **Vector Icons**: Replaced emojis with minimalist `<Path>` vector icons for User and Security (Lock).
- **UX**: Added dynamic loading state with spinning animation to the Login button.

## v10.1 — ERP Update Verification & Missing Components Recovery (2026-02-20)
- **Recovered**: Missing `ImagePath` and `AverageCost` properties in `Product` entity.
- **Restored**: Missing `PosTransaction` and `PosTransactionLine` entities for POS operations.
- **Restored**: Missing `PurchaseInvoice` and `PurchaseInvoiceLine` entities for Purchasing/Procurement.
- **Fix**: Re-registered `PosTransactions` and `PurchaseInvoices` DbSets in `AppDbContext` and configured relationships.

## v10.0 — Critical Architectural Refactoring (2026-02-19)
- **WPF Decoupled**: Removed embedded Kestrel web server, JWT, EF Migrate, SLA from `App.xaml.cs`
- **API is The Brain**: SLA `BackgroundService`, DbSeeder, default admin → all moved to API `Program.cs`
- **ProcessManager**: Now launches both `KamatekCrm.API.exe` (port 5050) and `KamatekCrm.Web.exe` (port 7000)
- **HttpClient**: WPF registers `HttpClient` for API communication at `http://localhost:5050`
- **Cleanup**: Removed `AddControllers/AddSwaggerGen` from WPF `ServiceCollectionExtensions`
- **Fix**: Fixed broken `KamatekCrm.Shared` project reference path in API `.csproj`

## 2026-02-19 (v9.0 - Core Business Modules: POS, Purchasing, Product Images)

### 🏪 Professional POS (Perakende Satış)
- **Rewritten** `DirectSalesViewModel.cs` — barcode scanning, row-level discounts (% and flat), per-item KDV, split payments, F8/F9 quick-pay shortcuts
- **Enhanced** `SalesDomainService.cs` — persists SubTotal, DiscountTotal, TaxTotal, Status on SalesOrder; per-item DiscountPercent, DiscountAmount, TaxRate, LineTotal on SalesOrderItem

### 📦 Hybrid Purchasing (Satın Alma)
- **NEW** `PurchasingDomainService.cs` — stock increase, Moving Average Cost (WAC) recalculation, StockTransaction recording, CashTransaction (expense/borç)
- **Refactored** `PurchaseOrderViewModel.cs` — delegates stock/WAC logic to domain service via `CompletePurchaseOrder`

### 🖼️ Product Image Management
- **NEW** `ProductImageService.cs` — WebP compression (≤200KB, 800px max), local file storage in `uploads/products/`
- **Updated** `AddProductViewModel.cs` — BrowseImageCommand, RemoveImageCommand, SelectedImagePreview, integrated into SaveProduct

### 🗃️ Schema Changes
- **Product**: `ImagePath` column
- **SalesOrder**: `SubTotal`, `DiscountTotal`, `TaxTotal`, `Notes`, `Status` (SalesOrderStatus enum)
- **SalesOrderItem**: `DiscountPercent`, `DiscountAmount`, `TaxRate`, `LineTotal`
- **CashTransaction**: `PaymentMethod` (PaymentMethod enum)
- **PurchaseOrder**: `InvoiceNumber`, `TotalAmount`, `Notes`
- **NEW** `SalesOrderPayment` entity — split-payment tracking (PaymentMethod, Amount, Reference)
- **NEW** `SalesOrderStatus`, `DiscountType` enums

### 🔧 DI Registrations
- `IProductImageService` → `ProductImageService` (Singleton)
- `IPurchasingDomainService` → `PurchasingDomainService` (Scoped)

## 2026-02-18 (v8.8 - Critical Bug Fix - Multiple View Crashes)
- **Critical Bug Fix**: 4 View'de daha null reference exception çözüldü.
  - **Sorun**: Aşağıdaki View'lerde XAML'da `<vm:ViewModel/>` şeklinde parametresiz constructor çağrılıyordu:
    - `SystemLogsView.xaml`
    - `FieldJobListView.xaml`
    - `ProjectQuoteEditorWindow.xaml`
    - `ProjectQuoteWindow.xaml`
  - **Çözüm**: `<UserControl.DataContext>` ve `<Window.DataContext>` blokları XAML'dan kaldırıldı.
  - **Renk Güncellemeleri**: Hardcoded renkler tema renkleriyle değiştirildi:
    - `#757575` → `{DynamicResource ThemeTextSecondary}`
    - `#616161` → `{DynamicResource ThemeTextSecondary}`
    - `#F5F5F5` → `{DynamicResource ThemeBackground}`
    - `{StaticResource BackgroundColor}` → `{DynamicResource ThemeBackground}`
- **Dosyalar**: `SystemLogsView.xaml`, `FieldJobListView.xaml`, `ProjectQuoteEditorWindow.xaml`, `ProjectQuoteWindow.xaml`

## 2026-02-18 (v8.7 - UI/UX Readability & Color Consistency)
- **Text Readability Improvements**: Tüm yazılarda okunabilirlik artırıldı.
  - `TextTrimming="CharacterEllipsis"` özelliği eklendi (DashboardView, UsersView, vb.)
  - `TextWrapping="Wrap"` ile uzun metinlerin taşması önlendi
  - Font boyutları standartlaştırıldı (HeaderSize=22, BodySize=14)
- **Color Consistency**: Hardcoded renkler tema renkleriyle değiştirildi.
  - DarkTheme.xaml: Legacy renk uyumluluğu eklendi (TextPrimary, PrimaryHue, vb.)
  - LightTheme.xaml: Legacy renk uyumluluğu eklendi
  - DashboardView.xaml: #3B82F6, #10B981 gibi renkler → {DynamicResource ThemePrimary}, {DynamicResource ThemeSuccess}
  - LoginView.xaml: #424242, #616161 gibi renkler → {DynamicResource ThemeTextPrimary}, {DynamicResource ThemeTextSecondary}
  - RepairTrackingWindow.xaml: #333, #888 gibi renkler → {DynamicResource ThemeTextPrimary}, {DynamicResource ThemeTextSecondary}
  - UsersView.xaml: #E3F2FD, #1976D2 gibi renkler → {DynamicResource ThemePrimaryLight}, {DynamicResource ThemePrimary}
- **New Styles Added** (Styles.xaml):
  - `ReadableTextBlock`: Temel okunabilirlik ayarları
  - `HeaderTextBlock`: Başlık stilleri
  - `SubHeaderTextBlock`: Alt başlık stilleri
  - `BodyTextBlock`: Gövde metin stilleri
  - `LabelTextBlock`: Etiket stilleri
  - `CaptionTextBlock`: Küçük metin/açıklama stilleri
- **Dosyalar**: `DarkTheme.xaml`, `LightTheme.xaml`, `Styles.xaml`, `DashboardView.xaml`, `LoginView.xaml`, `RepairTrackingWindow.xaml`, `UsersView.xaml`

## 2026-02-18 (v8.6 - Critical Bug Fix - UsersView Crash)
- **Critical Bug Fix**: `UsersView.xaml` null reference exception çözüldü.
  - **Sorun**: XAML'da `<vm:UsersViewModel/>` ile parametresiz constructor çağrılıyordu ama `UsersViewModel` constructor'ı `IAuthService` gerektiriyor.
  - **Çözüm**: `<UserControl.DataContext>` bloğu XAML'dan kaldırıldı. ViewModel DI container'dan otomatik olarak çözülecek.
  - **Ek**: `LastLoginDate` binding'e `TargetNullValue='-'` eklendi (null tarih değerleri için).
- **Dosyalar**: `UsersView.xaml`

## 2026-02-18 (v8.5 - UI/UX & Algorithm Fixes)
- **UI Layout Fixes**: Üst üste binen yazılar ve düğmeler düzeltildi.
  - `CustomersView.xaml`: StackPanel Grid.Row düzeltmesi (2→4) - butonlar artık doğru konumda
  - `RepairTrackingWindow.xaml`: StringFormat düzeltmesi (`'dd.MM.yyyy'` → `{}{0:dd.MM.yyyy HH:mm}`)
  - `RepairTrackingWindow.xaml`: TextBox'lara `UpdateSourceTrigger=PropertyChanged` eklendi (QuantityToAdd, UnitPriceToAdd)
  - `MainContentView.xaml`: Notification butonuna `ActionCommand` eklendi
- **Algorithm Fixes**: 
  - `DashboardViewModel`: Design-time constructor null reference hatası giderildi
  - `DashboardViewModel`: DesignTimeAuthService eklendi (IAuthService tam implementasyon)
- **Dosyalar**: `CustomersView.xaml`, `RepairTrackingWindow.xaml`, `MainContentView.xaml`, `DashboardViewModel.cs`

## 2026-02-18 (v8.4 - Complete DI Coverage & Security Patch)
- **Complete DI Registration**: 13 eksik ViewModel ve Window DI kaydı eklendi — tutarsız constructor kullanımı nedeniyle oluşabilecek runtime hataları engellendi.
  - ViewModels: `ProjectQuoteEditorViewModel`, `ProjectQuoteViewModel`, `EditUserViewModel`, `PasswordResetViewModel`, `PdfImportPreviewViewModel`, `QuickAssetAddViewModel`, `GlobalSearchViewModel`
  - Windows: `RepairRegistrationWindow`, `RepairTrackingWindow`, `FaultTicketWindow`, `DirectSalesWindow`, `ProjectQuoteEditorWindow`
- **Constructor Refactoring**: Parametresiz ctor + `new AppDbContext()` kullanan 5 ViewModel, DI uyumlu hale getirildi.
  - `AnalyticsViewModel`, `FinancialHealthViewModel`, `PipelineViewModel`, `RoutePlanningViewModel`, `SchedulerViewModel`
- **Null Safety Improvements**: Null reference uyarıları düzeltildi.
  - `AnimationHelper.cs`: Storyboard key null check eklendi
  - `App.xaml.cs`: OnExit metodunda _host null check eklendi, backupScope hata yönetimi iyileştirildi
  - `GetTaskDetailQuery.cs`: Nullable return type eklendi
- **Security Patch**: SixLabors.ImageSharp 3.1.8 → 3.1.12 güncellendi (CVE-2025-XXXX güvenlik açığı kapatıldı).
- **Dosyalar**: `ServiceCollectionExtensions.cs`, `AnimationHelper.cs`, `App.xaml.cs`, `GetTaskDetailQuery.cs`, `AnalyticsViewModel.cs`, `FinancialHealthViewModel.cs`, `PipelineViewModel.cs`, `RoutePlanningViewModel.cs`, `SchedulerViewModel.cs`, `KamatekCrm.API.csproj`

## 2026-02-12 (v8.3 - System Stability Audit — 14 Crash Fix)
- **DI Registration Fix**: 8 eksik ViewModel DI kaydı eklendi — sidebar navigasyonunda `InvalidOperationException` crash'i engellendi.
  - `AnalyticsViewModel`, `PipelineViewModel`, `SchedulerViewModel`, `RoutePlanningViewModel`, `FinancialHealthViewModel`, `PurchaseOrderViewModel`, `StockTransferViewModel`, `AddUserViewModel`
- **XamlParseException Fix**: 3 Window'da XAML `DataContext` bloğu kaldırıldı, code-behind constructor injection ile refactor edildi.
  - `RepairTrackingWindow` (`RepairViewModel` — IAuthService gerektirir)
  - `FaultTicketWindow` (`FaultTicketViewModel` — IToastService gerektirir)
  - `DirectSalesWindow` (`DirectSalesViewModel` — IAuthService, ISalesDomainService gerektirir)
- **Caller Fix**: 3 Window açma metodu DI ile ViewModel çözümleyecek şekilde güncellendi.
  - `MainContentViewModel.OpenRepairTracking()`, `MainContentViewModel.OpenDirectSales()`, `MainViewModel.OpenFaultTicket()`
- **Dosyalar**: `ServiceCollectionExtensions.cs`, `RepairTrackingWindow.xaml/.cs`, `FaultTicketWindow.xaml/.cs`, `DirectSalesWindow.xaml/.cs`, `MainContentViewModel.cs`, `MainViewModel.cs`

## 2026-02-12 (v8.2 - RepairRegistrationWindow DI Fix)
- **Bug Fix**: `XamlParseException` / `MissingMethodException` çözüldü.
  - **Neden**: XAML'de `<vm:RepairViewModel/>` ile parametresiz constructor çağrılıyordu, ancak `RepairViewModel` constructor'ı `IAuthService` gerektiriyor.
  - **Çözüm**: `Window.DataContext` bloğu XAML'den kaldırıldı. `RepairRegistrationWindow.xaml.cs` DI constructor injection ile refactor edildi.
  - **Callers**: `MainContentViewModel.OpenFaultTicket()` ve `RepairListViewModel.ExecuteCreateNewRepair()` DI ile ViewModel çözümleyecek şekilde güncellendi.

## 2026-02-12 (v8.1 - WPF Toast Notification Stabilization)
- **Crash Fix**: `System.Timers.Timer` + `Dispatcher.Invoke` → `DispatcherTimer` ile değiştirildi (deadlock riski ortadan kaldırıldı).
- **Binding Fix**: `HasToasts` property eklendi, `Message` binding yolu düzeltildi (`Message.Title` + `Message.Message`).
- **Command Fix**: `DismissCommand` → `RemoveToastCommand` olarak düzeltildi.
- **Animation**: Slide-in + Fade-in animasyonu eklendi (`CubicEase`).
- **Duplicate Fix**: `MainContentView.xaml`'deki kopya `ToastNotificationControl` kaldırıldı (DataContext'siz ghost instance).
- **Dark Theme**: Pastel renkler → dark tema uyumlu renkler ile değiştirildi.
- **Limit**: Maksimum 5 toast sınırı eklendi (stacking overflow önlemi).

## 2026-02-12 (v8.0 - Blazor → Minimal API + HTMX Migration)
- **Mimari Değişiklik**: Blazor Server + MudBlazor tamamen kaldırıldı. .NET 9 Minimal API + HTMX + Bootstrap 5 ile değiştirildi.
- **CSP Uyumu**: `unsafe-eval` tamamen ortadan kaldırıldı. Artık JavaScript framework'e bağımlılık yok.
- **Kimlik Doğrulama**: JWT + localStorage yerine **Cookie Authentication** (HttpOnly, SameSite=Strict).
- **Yeni Dosyalar**:
    - `Features/Auth/AuthEndpoints.cs`: Login GET/POST + Logout POST
    - `Features/Dashboard/DashboardEndpoints.cs`: Korumalı dashboard sayfası
    - `Shared/HtmlTemplates.cs`: C# raw string interpolation ile HTML şablon motoru
    - `wwwroot/css/site.css`: Premium dark tema (glassmorphism, KPI cards)
    - `wwwroot/js/htmx-config.js`: Antiforgery token otomatik enjeksiyonu
- **Silinen Dosyalar**: `Components/`, `Services/`, `wwwroot/app.css`, `wwwroot/lib/`
- **Paketler**: Blazored.LocalStorage, MudBlazor, System.IdentityModel.Tokens.Jwt kaldırıldı. Serilog.AspNetCore eklendi.
- **Güvenlik**: IIS `web.config` güncellemesi — strict CSP, X-Content-Type-Options, X-Frame-Options, Referrer-Policy eklendi.

## 2026-02-12 (v7.1 - CSP Fix for IIS Reverse Proxy)
- **CSP Double Header Fix**:
    - **Program.cs**: CSP middleware kaldırıldı (IIS ile çift başlık çakışması).
    - **web.config**: Tek otorite olarak güncellendi; `outboundRules` ile upstream CSP temizleme eklendi.
    - **Çözüm**: `eval` engellenmesi, Login butonu ve LocalStorage sorunları giderildi.

## 2026-02-12 (v7.0 - Web Login UX Enhancement)
- **Detailed Error Screen**:
    - **Shared**: `ServiceResponse` modeline `ErrorCode` ve `TechnicalDetails` eklendi.
    - **Service**: `ClientAuthService` bağlantı hatalarını ve exception detaylarını yakalayacak şekilde güncellendi.
    - **UI**: `LoginErrorDetails` bileşeni eklendi; teknik detayları gizlenebilir panelde gösterir.
│   ├── Layout/           # Ana sayfa şablonları (MainLayout, LoginLayout)
│   ├── Pages/            # Sayfalar
│   │   ├── Home.razor        # Dashboard
│   │   ├── Login.razor       # Login Form
│   │   ├── LoginErrorDetails.razor # Zengin Hata Ekranı (YENİ)
│   │   └── Tasks/            # Görev Yönetimi (List & Detail)
    - **Login**: Giriş ekranı zengin hata mesajlarını ve çözüm önerilerini destekleyecek şekilde revize edildi.

## 2026-02-09 (v6.9 - Remote Access & Documentation)
- **Remote Access Configuration**:
    - **Global Bindings**: API (5050) ve Web (7000) artık `0.0.0.0` dinliyor.
    - **Firewall Script**: `Enable-RemoteAccess.ps1` ile otomatik port açma.
    - **Documentation**: `REMOTE_ACCESS_GUIDE.md` ve `WEB.md` eklendi.
- **Web App Hotfixes**:
    - **MudBlazor Integration**: Eksik servis kayıtları ve paketler eklendi.
    - **Port Stability**: Web App portu 7000'e sabitlendi.
    - **Namespace Repair**: `Program.cs` ve Razor dosyalarındaki `CS0234` hataları giderildi.
- **Project Structure**: `docs/` klasörü güncellendi, `TEKNIK_HARITA` hibrit yapıyı kapsacak şekilde revize edildi.

## 2026-02-08 (v6.8 - Build Fixes & Architectural Improvements)
- **Compiler Fixes**: `Enums.` prefix removal and namespace standardization.
- **Null Safety**: `AddProductViewModel` constructor initialization and `EnumToBooleanConverter` null checks.
- **Architecture**: `UnitOfWork` parameterless constructor removed (enforcing DI). `SalesDomainService` and `InventoryDomainService` updated to use manual context temporarily (transaction isolation).
- **WPF Stability**: `MainWindow` changed to Transient to fix re-opening crashes.
- **Web Config**: API BaseUrl moved to `appsettings.json`.

## 2026-02-08 (v6.7 - Technician App Enhancement & Stability)
- **Photo Upload**: Blazor üzerinden fotoğraf yükleme ve galeri görünümü. `IPhotoStorageService` ile thumbnail desteği.
- **Google Maps**: Görev detay sayfasında müşteri konumuna navigasyon ve harita görünümü.
- **Web App Stability**: Namespace çakışmaları ve derleme hataları giderildi. `RootNamespace` tanımlandı.
- **Database Reset**: `SQLite Error (missing columns)` hatası için veritabanı %AppData% altına taşındı ve şema sıfırlandı.
- **DI & Navigation**: ViewModels manuel `new` yerine `NavigationService` üzerinden DI uyumlu hale getirildi.

## 2026-02-08 (v6.6 - Professional UI/UX Enhancement)
- **Toast Notifications**: Modern bildirim sistemi (Success, Error, Warning, Info). `IToastService` ile global yönetim.
- **Loading Overlay**: Asenkron işlemler için global yükleme ekranı. `ILoadingService` ile yönetim.
- **Animations**: Sayfa geçişleri ve liste animasyonları (`AnimationHelper`).
- **Dependency Injection**: UI servisleri (Toast, Loading) tüm ViewModel katmanına entegre edildi.
- **API Fix**: `AppDbContext` için `DbContextOptions` constructor eklendi (ASP.NET Core DI hatası giderildi).

## 2026-02-07 (v6.5 - Logging & Error Handling)
- **Serilog**: Günlük dönen log dosyaları (%AppData%) ve console loglama.
- **Global Exception Handler**: UI ve arka plan hatalarını yakalayan merkezi mekanizma.
- **Custom Exceptions**: `ValidationException`, `NotFoundException`, `BusinessRuleException`.
- **Infrastructure**: Temiz kod prensipleri ve yapısal iyileştirmeler.

## 2026-02-07 (v6.4 - Dependency Injection Refactoring)

### 🏗️ Architecture & DI
- **AuthService Integration**: `AuthService` artık static değil, `IAuthService` olarak inject ediliyor.
- **Domain Services**: `InventoryDomainService` ve `SalesDomainService` constructor injection yapısına geçirildi.
- **ViewModels**: `StockTransferViewModel` ve `ProductViewModel` DI uyumlu hale getirildi.
- **Clean Code**: Manuel servis oluşturma (`new Service()`) desenleri temizlendi.
- **Build Fixes**: Statik üye erişimi kaynaklı tüm derleme hataları giderildi.

## 2026-02-07 (v6.3 - Code Cleanup & Refactoring)

### 🧹 Code Cleanup & MVVM Enforcement
- **Refactored Views**: UI components (`CustomersView`, `StockTransferView`, `ToastNotificationControl`, etc.) refactored to remove code-behind and use MVVM Commands.
- **Login Module**: `LoginViewModel` now handles login logic via `ExecuteLoginAsync` command, removing dependency on code-behind.
- **Compiler Warnings (CS86xx)**: Addressed 50+ nullability warnings in ViewModels, Services, and Models (`CustomerAsset`, `ServiceJobViewModel`, `ProcessManager`, etc.).
- **Async Fixes (CS4014)**: Verified async/await usage across the application.
- **Architecture**: Enforced strict separation of concerns (Views strictly for UI, ViewModels for logic).

## 2026-02-07 (v6.3.1 - Critical API Fixes)

### 🛠️ API Stabilization
- **Middleware Fixes**: Resolved 500 errors by correcting `UseAuthentication` and `UseAuthorization` order.
- **Static Files**: Enabled `UseStaticFiles` and created `wwwroot` to prevent crashes.
- **Database**: Fixed `appsettings.json` connection string and successfully applied initial migrations (`AutoFix_InitialCreate`).
- **Swagger**: Ensured Swagger UI is available for API testing.

## 2026-02-07 (v6.2 - Architecture & Web Technician Integration)

### 🏆 Enterprise Architecture & Web Integration (Final Phase)
Backend API ve Web/Masaüstü istemcileri arasındaki entegrasyon tamamlandı.

- **API Controllers**:
  - `TechnicianController`: Teknisyenlerin kendilerine atanan görevleri görmesi ve durum güncellemesi için eklendi ([Authorize]).
  - `AdminController`: Yöneticilerin görev oluşturması ve ataması için eklendi ([Authorize(Roles = "Admin")]).
  - `AuthController`: JWT token üretiminde `ClaimTypes.NameIdentifier` eksikliği giderildi.
  - `AllowAll` CORS politikası onaylandı.

- **WPF Client Integration**:
  - `ApiService`: `HttpClient` tabanlı API katmanı oluşturuldu.
  - `LoginViewModel`: API üzerinden gerçek `LoginAsync` işlemi yapacak şekilde güncellendi. Token saklama mekanizması entegre edildi.
  - `ServiceJob.cs` (Shared): `Title` ve `AssignedTechnicianId` özellikleri eklendi.

- **Web Technician Panel**:
  - `TechnicianPanel.razor`: Teknisyenlerin görevlerini listelemesi ve durumlarını (Bekliyor, Devam Ediyor, Tamamlandı) güncellemesi için yeni sayfa oluşturuldu.
  - `MainLayout`: Giriş yapmış kullanıcılar için "Teknisyen Paneli" linki eklendi.
  - **Critical Fix**: `IAuthService` hatası giderildi ve `ApiAuthenticationStateProvider` stabil hale getirildi.

## 2026-02-07 (v6.0 - Greenfield Reconfiguration)
...
