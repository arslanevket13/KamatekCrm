# KamatekCRM - Teknik Harita

## Solution Yapısı

```
KamatekCRM/                       # Solution Root
├── KamatekCrm/                   # WPF Desktop Application (net9.0-windows)
├── KamatekCrm.Web/               # Blazor Web App (Technician Panel) (net9.0)
├── KamatekCrm.API/               # Backend Web API (net9.0)
├── KamatekCrm.Shared/            # Shared Class Library (net9.0)
```

## WPF Proje Detayları

```
KamatekCrm/
│
...
```

## Web Proje Detayları (KamatekCrm.Web)

```
KamatekCrm.Web/
│
├── Components/           # Blazor Bileşenleri
│   ├── Layout/           # Ana sayfa şablonları (MainLayout, LoginLayout)
│   ├── Pages/            # Sayfalar
│   │   ├── Home.razor        # Dashboard
│   │   ├── Login.razor       # Login Form
│   │   └── Tasks/            # Görev Yönetimi (List & Detail)
│   └── _Imports.razor    # Global usings
│
├── Services/             # İstemci Servisleri (HTTP Client)
│   ├── IClientAuthService.cs   # Login/Logout yönetim
│   ├── ClientAuthService.cs
│   ├── ITaskService.cs         # API ile görev iletişimi
│   ├── TaskService.cs
│   └── CustomAuthenticationStateProvider.cs # Blazor Auth State
│
├── wwwroot/              # Statik Dosyalar
│   ├── css/              # Bootstrap & App Styles
│   └── js/               # App Logic
│
└── Program.cs            # Web Host Config (Port 7000)
```

## WPF Proje Detayları

```
KamatekCrm/
│
├── ViewModels/           # İş mantığı (MVVM)
│   ├── MainViewModel.cs          # Ana navigation kontrolü + Logout
│   ├── LoginViewModel.cs         # Giriş ekranı mantığı
│   ├── UsersViewModel.cs         # Kullanıcı listesi + yönetimi
│   ├── AddUserViewModel.cs       # Hızlı kullanıcı ekleme
│   ├── DashboardViewModel.cs     # Dashboard KPI ve özet verileri
│   ├── ServiceJobViewModel.cs    # İş emri wizard + liste mantığı
│   ├── FaultTicketViewModel.cs   # Arıza kaydı (hibrit cihaz seçici)
│   ├── ProjectQuoteEditorViewModel.cs  # Üç Panelli Teklif Editörü
│   ├── RepairListViewModel.cs    # Tamir Listesi (Profesyonel UI)
│   ├── FieldJobListViewModel.cs  # Saha İşleri Listesi
│   ├── CustomerDetailViewModel.cs # Müşteri profili (tabs)
│   ├── AnalyticsViewModel.cs     # BI Dashboard (LiveCharts) (YENİ)
│   ├── PurchaseOrderViewModel.cs # B2B Satın Alma ve Tedarikçi (YENİ)
│   ├── ProductViewModel.cs       # Ürün listesi + Excel import
│   ├── AddProductViewModel.cs    # Ürün ekleme/düzenleme
│   ├── StockTransferViewModel.cs # Stok transfer işlemleri
│   ├── SettingsViewModel.cs      # Yedekleme/Ayarlar mantığı
│   └── ...
│
├── Views/                # XAML arayüzleri
│   ├── LoginView.xaml            # Giriş ekranı
│   ├── UsersView.xaml            # Kullanıcı listesi
│   ├── AddUserView.xaml          # Hızlı kullanıcı ekleme (popup)
│   ├── DashboardView.xaml        # Ana sayfa dashboard
│   ├── ServiceJobsView.xaml      # Master list (DataGrid)
│   ├── NewServiceJobWindow.xaml  # Wizard (ayrı pencere)
│   ├── FaultTicketWindow.xaml    # Arıza kaydı formu
│   ├── ProjectQuoteEditorWindow.xaml  # Üç Panelli Teklif Editörü
│   ├── CustomerDetailView.xaml   # 4 Tab yapısı
│   ├── AnalyticsView.xaml        # BI Dashboard charts (YENİ)
│   ├── PurchaseOrderView.xaml    # Satın alma ekranı (YENİ)
│   ├── ProductsView.xaml         # Ürün listesi
│   ├── AddProductWindow.xaml     # Ürün formu (dinamik specs)
│   ├── SettingsView.xaml         # Ayarlar ekranı
│   ├── LoadingOverlay.xaml       # Global Loading Indicator (YENİ)
│   ├── ToastNotificationControl.xaml # Bildirim kontrolü (YENİ)
│   └── ...
│
├── Models/               # Entity sınıfları
│   ├── Customer.cs       # Müşteri (Type, Address fields)
│   ├── Supplier.cs       # Tedarikçi (SupplierType, PaymentTermDays, Website, Balance)
│   ├── Attachment.cs     # Dijital Arşiv
│   ├── ServiceJob.cs     # İş emri (JobCategory, Priority)
│   ├── PurchaseOrder.cs  # Satın alma emri (YENİ)
│   ├── PurchaseOrderItem.cs # Sipariş kalemi (YENİ)
│   ├── ServiceProject.cs # Proje (ProjectScopeJson, TotalCost, TotalProfit)
│   ├── Product.cs        # Ürün (TechSpecsJson)
│   ├── ScopeNode.cs      # Kapsam ağacı node (Recursive, JSON)
│   ├── ScopeNodeItem.cs  # Kapsam kalemi (Finansal alanlar)
│   ├── Inventory.cs      # Stok (ProductId + WarehouseId)
│   ├── StockTransaction.cs # Stok hareketi (audit trail)
│   └── JobDetails/       # Dinamik iş detayları
│
├── Enums/                # Enum tanımları
│   ├── JobCategory.cs    # 8 iş kategorisi
│   ├── JobStatus.cs      # Pending, InProgress, Completed
│   ├── JobPriority.cs    # Low, Normal, Urgent, Critical
│   ├── ServiceJobType.cs # Fault / Project ayrımı
│   ├── WorkflowStatus.cs # 9 farklı proje durumu
│   ├── DeviceType.cs     # Cihaz türleri (IP Kamera, DVR vb.)
│   ├── AttachmentEntityType.cs # Dosya entity türleri
│   ├── SupplierType.cs   # Tedarikçi tipi (Toptancı, Servis, Üretici, Distribütör)
│   └── StockTransactionType.cs
│
├── Converters/           # Değer dönüştürücüler
│   ├── IntToVisibilityConverter.cs               # int → Visibility
│   ├── InvertedBooleanToVisibilityConverter.cs   # bool (ters) → Visibility
│   ├── GreaterThanZeroConverter.cs               # Sayı > 0 kontrolü (DataTrigger)
│
├── Helpers/              # Yardımcı sınıflar
│   ├── WebViewHelper.cs  # WebView2 HTML binding
│   └── ProcessManager.cs # API/Web Process Lifecycle (Auto-Start) (YENİ)
│
├── Infrastructure/       # Altyapı
│   └── GlobalExceptionHandler.cs # Merkezi Hata Yönetimi
│
├── Data/
│   └── AppDbContext.cs   # EF Core DbContext
│
├── Services/             # İş servisleri
│   ├── AddressService.cs     # Adres veri yönetimi
│   ├── PdfService.cs         # PDF oluşturma (QuestPDF)
│   ├── ProjectScopeService.cs # Proje ağacı ve veri yönetimi
│   ├── EmailService.cs       # SMTP e-posta gönderimi
│   └── Domain/               # Domain Servisleri
│       ├── InventoryDomainService.cs # Stok işlemleri (DI)
│       ├── SalesDomainService.cs     # Satış işlemleri (DI)
│   ├── SmsService.cs         # HTTP API SMS gönderimi
│   ├── AttachmentService.cs  # Dijital arşiv dosya yönetimi
│   ├── BackupService.cs      # SQLite yedekleme/geri yükleme
│   ├── EventAggregator.cs    # Pub/Sub Event Bus (YENİ)
│   │
│   └── Domain/               # Domain Services (YENİ)
│       ├── ISalesDomainService.cs      # Satış interface
│       ├── SalesDomainService.cs       # Thread-safe satış işlemleri
│       ├── IInventoryDomainService.cs  # Stok interface
│       └── InventoryDomainService.cs   # Thread-safe stok işlemleri
│   ├── LoadingService.cs     # Global Loading Overlay yönetimi (YENİ)
│   ├── ToastService.cs       # Toast Bildirim yönetimi (YENİ)
│
├── Repositories/         # Data Access Layer (YENİ)
│   ├── IUnitOfWork.cs    # Unit of Work interface
│   └── UnitOfWork.cs     # Transaction yönetimi
│
├── Exceptions/           # Custom Exceptions (YENİ)
│   ├── ValidationException.cs        # Doğrulama hatası
│   ├── NotFoundException.cs          # Kayıt bulunamadı hatası
│   ├── BusinessRuleException.cs      # İş kuralı hatası
│   ├── InsufficientStockException.cs # Yetersiz stok hatası
│   └── ReferentialIntegrityException.cs # Bağımlılık hatası
│
├── Events/               # Event DTOs (YENİ)
│   ├── SaleCompletedEvent.cs   # Satış tamamlandı eventi
│   └── StockUpdatedEvent.cs    # Stok güncellendi eventi
│
├── Views/ (Devam)
│   ├── RepairRegistrationWindow.xaml # Cihaz kabul formu
│   ├── RepairTrackingWindow.xaml     # Arıza takip ve işlem merkezi
│   ├── DirectSalesWindow.xaml        # Perakende Satış (POS)
│   └── ...
│
├── ViewModels/ (Devam)
│   ├── RepairViewModel.cs        # Tamir iş akışı ve tarihçe yönetimi
│   ├── DirectSalesViewModel.cs   # POS sepet ve satış mantığı
│   └── ...
│
├── Models/ (Devam)
│   ├── ServiceJobHistory.cs      # İşlem logları
│   ├── SalesOrder.cs             # Satış siparişi
│   ├── SalesOrderItem.cs         # Sipariş kalemi
│   └── ...
│
├── Enums/ (Devam)
│   ├── RepairStatus.cs          # Tamir durumları (Workflow)
│   ├── PaymentMethod.cs         # Ödeme yöntemi (Cash/Card)
│   └── ...
│
└── docs/                 # Bu dokümantasyon
```

## Kritik Mimariler

### 0. Hibrit Mimari (WPF + Web)
- **Host**: WPF uygulaması, API (5050) ve Web (7000) sunucularını yönetir.
- **ProcessManager**: Web uygulamasını (`KamatekCrm.Web.exe`) arka planda başlatır ve portları (`0.0.0.0`) yönetir.
- **Data Flow**: Web App → API (JWT) → Database.

### 1. Navigation (Single Window)
- `NavigationService` → Singleton pattern
- `MainContentView` = Sidebar + ContentControl
- `MainContentViewModel` = DataContext (navigation commands)
- DataTemplate binding → ViewModel → View eşleşmesi

### 2. Dinamik İş Detayları
- `JobCategory` enum → `JobDetailTemplateSelector` → DataTemplate
- Her kategori kendi `JobDetailBase` alt sınıfına sahip

### 3. Stok Mantığı
- `Inventory`: ProductId + WarehouseId = Stok miktarı
- `StockTransaction`: Audit trail
- `CreateInitialStock()`: Açılış stoğu oluşturur

### 4. Servis Yaşam Döngüsü
- **Arıza (Fault)**: Hızlı kayıt → `FaultTicketWindow`
- **Proje (Project)**: 5 fazlı akış → `ProjectWorkflowWindow`
  - Keşif → Teklif → Onay → Uygulama → Final
- `IsStockReserved` / `IsStockDeducted` → Stok takibi

### 5. Enterprise ERP (ERP Megamodule)
- **BI Analytics**: `LiveChartsCore.SkiaSharpView.WPF` ile dashboard
- **B2B Procurement**: Tedarikçi borç takibi, stok entegreli satın alma
- **Digital Archive**: `AttachmentService` ile merkezi dosya yönetimi (GUID filenames)
- **RBAC**: `User` modelinde granular permission alanları (`CanViewFinance` vb.)

### 6. System Configuration (Greenfield)
- **Ports**: API (5050), Web (7000) - Hardcoded.
- **Launcher**: `ProcessManager` visible console windows (`UseShellExecute=true`).
- **Environment**: Web App forced to `Development` for static asset compatibility.
