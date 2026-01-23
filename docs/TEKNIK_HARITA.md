# KamatekCRM - Teknik Harita

## Klasör Yapısı

```
KamatekCrm/
├── App.xaml              # Uygulama girişi, global stiller
├── MainWindow.xaml       # Ana pencere (sidebar navigation)
│
├── Assets/               # Görsel dosyalar
│   └── Images/
│       └── KamatekLogo.jpg   # Kurumsal logo
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
│   ├── RepairListViewModel.cs    # Tamir Listesi (Profesyonel UI) (YENİ)
│   ├── FieldJobListViewModel.cs  # Saha İşleri Listesi (YENİ)
│   ├── CustomerDetailViewModel.cs # Müşteri profili (tabs)
│   ├── ProductViewModel.cs       # Ürün listesi + Excel import
│   ├── AddProductViewModel.cs    # Ürün ekleme/düzenleme
│   ├── StockTransferViewModel.cs # Stok transfer işlemleri
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
│   ├── ProjectQuoteEditorWindow.xaml  # Üç Panelli Teklif Editörü (YENİ)
│   ├── CustomerDetailView.xaml   # 4 Tab yapısı
│   ├── ProductsView.xaml         # Ürün listesi
│   ├── AddProductWindow.xaml     # Ürün formu (dinamik specs)
│   └── ...
│
├── Models/               # Entity sınıfları
│   ├── Customer.cs       # Müşteri (Type, Address fields)
│   ├── ServiceJob.cs     # İş emri (JobCategory, Priority)
│   ├── ServiceProject.cs # Proje (ProjectScopeJson, TotalCost, TotalProfit)
│   ├── Product.cs        # Ürün (TechSpecsJson)
│   ├── ScopeNode.cs      # Kapsam ağacı node (Recursive, JSON) (YENİ)
│   ├── ScopeNodeItem.cs  # Kapsam kalemi (Finansal alanlar) (YENİ)
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
│   └── StockTransactionType.cs
│
├── Helpers/              # Yardımcı sınıflar
│   ├── WebViewHelper.cs  # WebView2 HTML binding
│   └── Converters
│
├── Data/
│   └── AppDbContext.cs   # EF Core DbContext
│
├── Services/             # İş servisleri
│   ├── AddressService.cs # Adres veri yönetimi
│   ├── PdfService.cs     # PDF oluşturma (QuestPDF)
│   └── ProjectScopeService.cs # Proje ağacı ve veri yönetimi
│
├── Views/ (Devam)
│   ├── RepairRegistrationWindow.xaml # Cihaz kabul formu (YENİ)
│   ├── RepairTrackingWindow.xaml     # Arıza takip ve işlem merkezi (YENİ)
│   ├── DirectSalesWindow.xaml        # Perakende Satış (POS) (YENİ)
│   └── ...
│
├── ViewModels/ (Devam)
│   ├── RepairViewModel.cs        # Tamir iş akışı ve tarihçe yönetimi (YENİ)
│   ├── DirectSalesViewModel.cs   # POS sepet ve satış mantığı (YENİ)
│   └── ...
│
├── Models/ (Devam)
│   ├── ServiceJobHistory.cs      # İşlem logları (YENİ)
│   ├── SalesOrder.cs             # Satış siparişi (YENİ)
│   ├── SalesOrderItem.cs         # Sipariş kalemi (YENİ)
│   └── ...
│
├── Enums/ (Devam)
│   ├── RepairStatus.cs          # Tamir durumları (Workflow) (YENİ)
│   ├── PaymentMethod.cs         # Ödeme yöntemi (Cash/Card) (YENİ)
│   └── ...
│
└── docs/                 # Bu dokümantasyon
```

## Kritik Mimariler

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
