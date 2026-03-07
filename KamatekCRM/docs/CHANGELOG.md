## v14.6 — Repair List & Fault Ticket Overhaul (2026-03-07)
- **ARCHITECTURE**: `RepairListViewModel` ve `FaultTicketViewModel` tamamen DI, async ve `IToastService` kullanacak şekilde yeniden yazıldı.
- **ARCHITECTURE**: Senkron veritabanı sorguları `async/await` yapısına çevrildi, `AppDbContext` direct usage kaldırıldı. Zaman işlemlerinde `DateTime.UtcNow` standardı getirildi.
- **NEW**: `RepairListView.xaml` premium tasarım — Status filter çipleri, KPI header kartları, GridSplitter, detaylı workflow haritası, cihaz fotoğraf galerisi ve maliyet özeti (ThemeColors kullanılarak).
- **NEW**: `FaultTicketWindow.xaml` 3 kolonlu modern premium form (chip toggle, input feedback vs.) olarak yeniden tasarlandı. `RepairRegistrationWindow` silindi ve kullanımdan kaldırıldı.
- **NEW**: `RepairStatusHelper` XAML CommandParameter statik binding'leri için oluşturuldu.
- **WORKFLOW**: Stok güncellemeleri işlemi `CompleteJobAsync` fonksiyonuna ACID tam transaction ile entegre edildi. Timeline geçmiş kayıtları oluşturuldu.

## v14.5 — Route Planning System Overhaul (2026-03-06)
- **NEW**: `RoutePlanningViewModel` tamamen yeniden yazıldı — teknisyen seçimi, iş atama, sıralama, nearest-neighbor optimizasyon, mesafe/süre hesaplama, DB kayıt.
- **NEW**: `RoutePlanningView.xaml` premium tasarım — split panel, istatistik kartları, tab'lı iş/rota listeleri, WebView2 Leaflet harita.
- **NEW**: API `LocationController` — 4 yeni endpoint: `GET route-plan/all`, `PUT route-plan/reorder`, `PUT route-plan/point/{id}/visit`, `DELETE route-plan/{userId}`.
- **FIX**: WebView2 race condition — harita artık `_isWebViewReady` flag ile doğru zamanda yükleniyor.
- **FIX**: WPF `AppDbContext` → `RoutePoints` ve `TechnicianLocations` DbSet'leri eklendi.
- **FIX**: `RepairListViewModel` — `NpgsqlOperationInProgressException` (eşzamanlı DbContext kullanımı) düzeltildi.
- **NEW**: Web `RouteEndpoints.cs` — `/technician/route` ve `/technician/route/visit/{id}` (HTMX) endpoint'leri.
- **NEW**: Web `RoutePlanningPage` — Leaflet harita, KPI kartları, HTMX ziyaret işaretleme, Google Maps navigasyon, ilerleme çubuğu.
- **NEW**: Web sidebar'a "Rota Planı" navigasyon linki eklendi.

## v14.4 — Quote Management System (2026-03-05)
- **NEW**: `QuoteListView` + `QuoteListViewModel` — Tüm tekliflerin listelenmesi, aranması, filtrelenmesi ve yönetilmesi.
- **NEW**: KPI dashboard kartları — Toplam, Taslak, Gönderildi, Onaylandı, Reddedildi adet ve tutarları.
- **NEW**: `QuoteStatus` enum — Teklif yaşam döngüsü (Draft → Sent → Approved/Rejected/Expired/Revised).
- **NEW**: `QuoteRevision` modeli — Revizyon geçmişi JSON olarak saklanır.
- **NEW**: `QuoteStatusConverters.cs` — QuoteStatus → Türkçe metin ve badge renk dönüştürücüleri.
- **NEW**: Teklif editöründe iskonto (%) ve KDV (%) hesaplama + Grand Total görünümü.
- **NEW**: Teklif editöründe otomatik `QuoteNumber` (TEK-YYYY-NNN) ve revizyon kaydı.
- **FIX**: `ServiceProject` modeli 12 yeni alanla genişletildi (QuoteNumber, RevisionNumber, SentDate, ValidUntil, KdvRate, Notes, PaymentTerms, ApprovedDate, RejectedDate, RejectionReason, RevisionsJson, QuoteStatus).
- **FIX**: `StructureGeneratorService` → `ScopeNode` tabanlı olarak refactored (eski `StructureTreeItem` kaldırıldı).
- **FIX**: Sidebar'dan "Proje & Teklif" butonu artık `QuoteListView`'e inline navigasyon yapıyor.
- **CLEANUP**: `StructureTreeItem` ölü kodu silindi.

## v14.3 — Product Image & BOM PDF Fix (2026-03-05)
- **FIX**: Ürün fotoğraf yükleme artık `ProductImageService` ile tutarlı relative path kaydediyor. Eski AppData absolute path stratejisi kaldırıldı.
- **FIX**: `ProductsView.xaml` thumbnail trigger mantığı düzeltildi — resim artık DataGrid'de doğru gösteriliyor.
- **NEW**: `ImagePathConverter.cs` — Relative/absolute image path'leri WPF Image kaynağına dönüştüren IValueConverter.
- **FIX**: `PdfService.FlattenScopeNodesWithImages()` artık ürün resimlerini PDF'e aktarıyor (`ImagePath = null` hard-code kaldırıldı).
- **FIX**: `ScopeNodeItem` modeline `ProductId` ve `ImagePath` alanları eklendi (Clone/CopyTo destekli).
- **FIX**: `AddProductViewModel.SaveProduct()` — `Task.Run().Result` deadlock riski `async/await` ile giderildi.

## v14.2 — Absolute System Audit & Restoration (2026-03-03)
- **CRITICAL**: Fixed entire WPF, Web and API project compilation errors to achieve a flawless 0-warning build.
- **AUDIT Phase 5**: CS8618 & CS8604 Null Reference Prevention: Eradicated nullable property warnings across `DashboardViewModel`, `CustomerViewModel`, `KmFilterPanel`, and Blazor Web `TechnicianDashboardEndpoints`.
- **AUDIT Phase 5**: API Integrity Restoration: Injected missing `CustomerAssets` and `ServiceProjects` DbSets into `AppDbContext`. Fixed missing DI scoped registration for `ISalesDomainService` in `Program.cs` and purged the invalid WPF container link. Fixed `CS1955` and `CS8629` bugs in `CustomersController` and `DashboardController`.
- **CRITICAL**: Fixed entire WPF project compilation errors and eliminated silent failures.
- **AUDIT Phase 1**: Dependency Injection Purge: Correctly registered missing ViewModels (`CustomerAddViewModel`, `CustomersViewModel`, etc.) and cleaned up static service DI crashes.
- **AUDIT Phase 2**: MVVM Binding Integrity: Restored missing `[ObservableProperty]` bindings (`BlockCount`, `FlatCount`, `TotalUnitCount`) in `ServiceJobViewModel` bridging XAML gaps.
- **AUDIT Phase 3**: The Bridge (WPF -> API E2E Verification): Completely migrated `ServiceJobViewModel` away from direct `AppDbContext` usage. Implemented full `ApiClient` integration for `GetAsync`, `PostAsync`, `PatchAsync`. Added required `POST /api/servicejobs` and `PATCH /api/servicejobs/{id}/status` endpoints to `ServiceJobsController`. Added `POST /api/customers/{id}/assets` to `CustomersController` to support on-the-fly asset creation in Service Job Wizard. Added missing `PatchAsync` method to `ApiClient.cs`.
- **AUDIT Phase 3**: The Bridge (WPF -> API E2E Verification): Completely migrated `RepairViewModel` and `DirectSalesViewModel` (POS) from direct `AppDbContext` to `ApiClient`. Added `POST /api/servicejobs/{id}/history` endpoint for adding job histories. Added `POST /api/sales` endpoint to offload domain logic to the API instead of processing it on the WPF client.
- **AUDIT Phase 3**: The Bridge (WPF -> API E2E Verification): Completely migrated `DashboardViewModel` away from direct DB dependencies. Centralized dashboard stats/charts processing in API under `GET /api/dashboard/summary`. Used LiveChartsCore effectively with API payload data.
- **AUDIT Phase 3**: API E2E Verification: Initiated eradication of direct `AppDbContext` usage in WPF. Fully ported `CustomersViewModel` to standard `ApiClient`.
  - Backed by adding `/api/customers/stats` endpoint to API.
  - Moved `CustomerCode` generation payload to the backend to eliminate race conditions.
- **AUDIT Phase 4**: UX & Silent Failure Prevention: Swept `KamatekCRM/ViewModels` for empty `catch {}` blocks and generic `Console.WriteLine`/`Debug.WriteLine` usages. Converted missing error handling in `ServiceJobViewModel`, `LoginViewModel`, and `MainContentViewModel` to explicitly utilize `IToastService.ShowError()` and `ILoadingService` ensuring no background API exceptions are silently swallowed.

## v14.1 — Enterprise Phase 2: UDP Network Discovery Integration (2026-03-01)
- **NEW**: `NetworkDiscoveryService` integrated entirely into the WPF `LoginViewModel`.
  - Scans for `ServerDiscoveryMessage` on port 5051 via UDP Broadcasts.
  - Features real-time UI binding (Spinner + Status Text) while verifying server availability.
- **ENHANCED**: `ApiClient.cs` now exposes `SetBaseUrl()` to dynamically reroute all endpoints based on the discovered API server IP, completely eliminating hardcoded `localhost` issues for field desktop clients.
- **ENHANCED**: `LoginView` prevents premature login attempts by binding command states to network discovery progress.

## v14.0 — Enterprise Phase 1: Core Stability & Communication (2026-03-01)

## v13.2 — Web Teknisyen App: PWA + Mobile-First (2026-03-01)
- **PWA**: `manifest.json` + Service Worker (offline cache, offline page) → Mobil cihazlarda kurulabilir uygulama.
- **MOBILE**: Hamburger sidebar, bottom navigation bar (5 item), touch-friendly (44px tap target).
- **CSS**: 450+ satır Midnight Blue CSS — KPI glow cards, glassmorphism, skeleton loading, responsive breakpoints.
- **ENHANCED**: TechnicianDashboard → real KPI data, greeting, empty states, quick actions.
- **ENHANCED**: SchedulePage → zaman çizelgesi (08:00-17:00 timeline), tarih navigasyonu, planlanmamış işler.
- **ENHANCED**: TechnicianProfile → avatar kartı, bilgi grid, ayarlar bölümü.
- **ENHANCED**: JobsPage → durum renk badge'leri, filtre dropdown.
- **NEW**: `JobWorkflowEndpoints.cs` → İş detay, işe başla, işi tamamla, not ekleme (HTMX partial).
- **FIX**: Duplicate method tanımları temizlendi, tüm tablo `table-responsive` wraplendi.

## v13.1 — Midnight Blue Dark Theme (2026-03-01)
- **THEME**: Complete dark mode conversion — `CustomTheme.xaml`, `Styles.xaml`, `DesignTokens.xaml` all unified to Midnight Blue palette.
- **PALETTE**: Surfaces (#0B0E14/#141821), Text (#F1F5F9/#94A3B8), Borders (#1E293B), Accent (#3B82F6→#06B6D4).
- **COMPONENTS**: All buttons, cards, DataGrid (rows/headers/cells/alternating), TextBox, ComboBox, DatePicker, ToggleButton, NavButton, FilterBar — dark-native with glassmorphism and neon glow.
- **FIX**: Resolved Light/Dark theme conflict that caused visual inconsistencies.

## v13.0 — CRM Pipeline & Customer Analytics (2026-03-01)
- **ENHANCED**: `CustomersController` — From 5 basic endpoints to 10 enterprise endpoints.
- **NEW**: Advanced search (segment/city/multi-sort), customer detail with job+sales history, customer notes/activity timeline (9 types), RFM scoring (Champions/Loyal/Potential/AtRisk/Hibernating), city distribution analytics, churn risk detection with potential lost revenue.
- **NEW**: `CustomerNote` model — CRM activity types: Note, PhoneCall, Visit, Email, Proposal, Complaint, Payment, ServiceRequest, Follow_Up.

## v12.9 — PDF Report Engine (2026-03-01)
- **NEW**: `PdfReportService` — QuestPDF-based professional PDF engine with shared components (company header, section titles, KPI cards, footer with pagination).
- **TEMPLATES**: ServiceJob Report (job + customer + parts table + pricing), Invoice (dual-column + KDV details), Monthly Summary (KPI cards + top products).
- **NEW**: `PdfController` — 3 download endpoints: `/api/pdf/service-job/{id}`, `/api/pdf/invoice/{orderId}`, `/api/pdf/monthly-summary`.
- **DEP**: Added `QuestPDF 2024.12.3`.

## v12.8 — GPS Tracking & Map Infrastructure (2026-03-01)
- **NEW**: `TechnicianLocation` model — GPS coords, accuracy, speed, heading, battery level, background flag.
- **NEW**: `RoutePoint` model — Daily route planning with ordered stops and ETA.
- **NEW**: `LocationController` — 7 endpoints: single/batch GPS update, active technicians map (last 30 min), location history with Haversine total distance, nearest technicians (for smart job assignment), daily route plan CRUD.

## v12.7 — Sales, Purchases & Finance (2026-03-01)
- **NEW**: `SalesController` — Sales orders with search/filter/pagination, order detail with items+payments, daily summary (revenue/discount/tax/cash flow/payment breakdown), Excel export.
- **NEW**: `PurchasesController` — Purchase orders and invoices (filtered by supplier/status/payment, paginated), payment recording with auto status update (Unpaid→PartiallyPaid→Paid), supplier payment summary.
- **NEW**: `FinanceController` — Cash position (cash/card/total, today's income/expense, 30s cache), filterable cash transactions, create cash transaction, accounts summary (receivable vs payable vs unpaid = net position).

## v12.6 — Products, Inventory & Report Engine (2026-03-01)
- **NEW**: `AppDbContext` — 13 ERP entity DbSets (Product, Brand, Inventory, Warehouse, StockTransaction, SalesOrder, CashTransaction, Supplier, PurchaseOrder, etc.)
- **NEW**: `ProductsController` — Full CRUD + search (name/SKU/barcode) + category/brand filter + low-stock endpoint + brands dropdown.
- **NEW**: `InventoryController` — Stock levels by warehouse, stock transactions (in/out/transfer) with auto quantity update, real-time SignalR stock alerts.
- **NEW**: `SuppliersController` — CRUD + search + soft-deactivation + supplier balance summary with open order count.
- **NEW**: `ReportsController` — 5 analytical endpoints: Technician Performance, Monthly Revenue (sales+service-expenses, profit margin), Stock Valuation (value + potential profit), Customer Segments, SLA Performance.

## v12.5 — WPF Premium Components R2 + Keyboard Shortcuts (2026-03-01)
- **NEW**: `KmAvatar` — Initials/image avatar with deterministic color per person, 4 sizes (S/M/L/XL), online status dot.
- **NEW**: `KmNotificationCenter` — Bell icon with unread badge, popup panel, 7 notification types with emoji icons, Turkish relative time, max 50 notifications.
- **NEW**: `KmSplitButton` — Primary action + dropdown alternatives (5 styles: Primary/Secondary/Success/Danger/Warning).
- **NEW**: `KmWizardStepper` — Multi-step form wizard with progress bar, step validation states, auto CanGoNext/Previous.
- **NEW**: `KeyboardShortcutService` — Global shortcut infrastructure: Ctrl+N/S/F, F5, Esc, Ctrl+E/P, module hotkeys. Smart TextBox awareness.

## v12.4 — Excel Engine & SignalR Real-time (2026-03-01)
- **NEW**: `ExcelService` — Generic ClosedXML export engine. Reflection-based column mapping, styled headers, zebra striping, auto-filter, frozen header row. Pre-built templates for ServiceJobs and Customers with Turkish column names.
- **NEW**: `NotificationHub` — SignalR real-time hub at `/hubs/notifications`. Role-based groups, online/offline status, GPS location relay to Admin, typed event system (JobStatusChanged, NewJobAssigned, StockAlert, DashboardRefresh).
- **NEW**: `INotificationService` — Server-side notification dispatch. Controllers inject this to push events to connected clients.
- **NEW**: `ExportController` — 3 download endpoints: ServiceJobs (date-filtered), Customers (with job count), Categories. Returns timestamped .xlsx files.
- **DEPS**: Added `ClosedXML 0.104.2` package.

## v12.3 — Validation, Result Pattern & Rate Limiting (2026-03-01)
- **NEW**: `Result<T>` + `PagedResult<T>` + `ErrorCodes` — Clean return types for service layer, replaces exception-based control flow.
- **NEW**: Rate Limiting — 3-tier strategy: Global (100 req/10s sliding window), Auth (10 req/min brute-force), Heavy (30 req/min token bucket).
- **NEW**: `ValidationFilter` — DataAnnotation errors → structured JSON with field-level detail.
- **NEW**: `RequestTimingFilter` — Measures every request, logs slow calls (>1s), adds `X-Response-Time-Ms` header.
- **IMPROVED**: `Program.cs` — ValidationFilter + RequestTimingFilter globally registered, RateLimiter middleware in pipeline.

## v12.2 — API Infrastructure & Enterprise Middleware (2026-03-01)
- **NEW**: `GlobalExceptionMiddleware` — Unhandled exception → structured JSON response. Exception→HTTP status mapping, Serilog correlation ID logging, Turkish user-friendly messages in production, stack trace in development only.
- **NEW**: `ApiResponse<T>` — Standardized API response envelope with Success/Data/Error/Meta. `PaginationMeta` with auto-calculated TotalPages, HasPrevious/HasNext.
- **NEW**: `CacheService` — IMemoryCache cache-aside pattern. GetOrCreateAsync factory, prefix-based invalidation (`dashboard:*`), TTL strategy (Dashboard 30s, Lists 5m, Reports 15m).
- **IMPROVED**: `DashboardController` — All 4 endpoints now cached (30s TTL). New `POST /invalidate-cache` endpoint for admin refresh. Responses use `ApiResponse<T>` envelope.
- **IMPROVED**: `Program.cs` — Registered IMemoryCache, ICacheService (singleton), GlobalExceptionMiddleware (first in pipeline).

## v12.1 — Full CRUD API Controllers (2026-03-01)
- **NEW**: `ServiceJobsController` — Full CRUD with multi-filter (search, status, customer, technician, date range), sorting, pagination headers, automatic history tracking on status changes, partial status update (PATCH).
- **NEW**: `CategoriesController` — Full CRUD with duplicate name prevention.
- **NEW**: `DashboardController` — Parallel-query KPI stats, weekly trend data (fills empty days), job category distribution, status distribution.
- **NEW**: `UsersController` — Search/role/technician filter, detail projection (excludes PasswordHash), partial update, soft-deactivation.
- API endpoints expanded from 8 to 23+.

## v12.0 — Design System & Premium Component Library (2026-03-01)
- **NEW**: `DesignTokens.xaml` — Semantic design token system: surfaces, elevations (0-5), animation easings, KPI gradients, status tint colors, interactive states, and component-specific dimensions.
- **NEW**: `ComponentStyles.xaml` — 540+ lines of XAML ControlTemplates for all Km* premium components.
- **NEW**: `KmStatusBadge` — Auto-colored status labels with pulsing dot indicator (Success/Warning/Error/Info/Neutral).
- **NEW**: `KmKpiCard` — Animated KPI card with ease-out cubic counter animation, trend arrows (▲/▼), gradient accent icon, prefix/suffix, subtitle.
- **NEW**: `KmSearchBox` — Debounced search input (configurable delay), clear button, placeholder, focus ring animation.
- **NEW**: `KmEmptyState` — Centered empty-state display with icon circle, title, message, and optional CTA button.
- **NEW**: `KmBreadcrumb` — Hierarchical page navigation with chevron separators.
- **NEW**: `KmTimeline` — Vertical event timeline with color-coded status dots, relative time display ("3 saat önce").
- **NEW**: `KmFilterPanel` — Multi-filter panel: date range, status dropdown, category dropdown, filter count badge, clear-all.
- **DI**: All components registered in `App.xaml` resource dictionaries.

## v11.8 — Network Discovery Service & Auto-Configuration (2026-03-01)
- **NEW**: `NetworkDiscoveryService.cs` in API — Broadcasts server coordinates (API URL, Web URL, Database Host) via UDP port 5051 every 5 seconds.
- **NEW**: `NetworkDiscoveryService.cs` in Desktop (WPF) — Listens for UDP broadcasts on port 5051 to zero-config auto-discover the API on the local network.
- **Config**: Added `NetworkDiscovery` properties to `appsettings.json` for both KamatekCrm.API and KamatekCRM(WPF).

## v11.7 — Technician Web Dashboard & Auth Rollout (2026-03-01)
- **NEW**: `TechnicianDtos.cs` — Shared models for Repairs, Projects, Quotes, and Installations.
- **Web**: Implemented `TechnicianDashboardEndpoints.cs` providing KPI stats and job listings for technicians.
- **Web**: Added `SchedulePage` and `ProfilePage` to technician features.
- **Web**: Refactored `HtmlTemplates.cs` with modern Bootstrap 5 UI for customers, products, and sales.
- **API**: Stabilized `AuthController.cs` for cross-platform JWT authentication.

## v11.6 — JWT Auth Login Endpoint (2026-02-28)
- **NEW**: `AuthController.cs` — `POST /api/auth/login` endpoint for JWT token generation.
- **Security**: Password verification uses PBKDF2 (100K iterations, SHA256) — identical to WPF `AuthService`.
- **Claims**: JWT includes `sub`, `Name`, `Role`, `UserId`, `FullName`.
- **Hardening**: `CryptographicOperations.FixedTimeEquals` for timing-attack-safe password comparison.
- **Audit**: `User.LastLoginDate` updated on every successful login.

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
