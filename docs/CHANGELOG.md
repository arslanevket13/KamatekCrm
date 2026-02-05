# KamatekCRM - Değişiklik Günlüğü


## 2026-02-05 (v5.3 - Project Recovery & Auto-Startup)

### 🧹 Project Recovery (Clean Slate)
- **Web Rebuild**: `KamatekCrm.Web` projesi sıfırdan oluşturuldu (Blazor Server net8.0). Hatalı SDK referansları temizlendi.
- **Mobile Fix**: `KamatekCrm.Mobile` projesi .NET 9.0 altyapısına yükseltildi ve XAML namespace hataları (MC3074) giderildi.
- **Build Success**: Tüm çözüm hatasız derleniyor.

### 🚀 Auto-Startup Integration
- **ProcessManager**: API ve Web uygulamalarını arka planda yöneten servis eklendi.
- **WPF Lifecycle**: Masaüstü uygulaması açıldığında servisleri başlatır, kapanışta temizler (Zombie process koruması).

---

## 2026-02-04 (v5.2 - Build Verification & Integrity)

### ✅ Final Build Fixes
- **Build Success**: Tüm projeler (`KamatekCrm.Shared`, `KamatekCrm.API`, `KamatekCrm`) hatasız derlendi (0 Error).
- **Type Safety**: `ProductCategory` vs `ProductCategoryType` enum karışıklığı giderildi (AddProductViewModel).
- **Stubs Integrity**: `Stubs.cs` dosyası `ServiceProject` ve `StockTransaction` eksik özellikleri ile zenginleştirildi.
- **PipelineViewModel**: Garbled code düzeltildi ve `int?` dönüşüm hatası giderildi.
- **Refactoring**: `ProjectQuoteEditorViewModel` için eksik `Clone(string)` metodu eklendi.

---

## 2026-02-04 (v5.1 - Web API Project)

### 🌐 ASP.NET Core Web API Oluşturuldu
- **KamatekCrm.API** (.NET 8.0 Web API) projesi eklendi.
- SQL Server entegrasyonu (`ApiDbContext`) yapılandırıldı.
- JWT Authentication ve CORS middleware aktif.
- Swagger/OpenAPI UI root'ta erişilebilir (`/`).
- **Controllers**:
  - `ProductsController`, `CustomersController` (CRUD).
  - `AuthController`: Login + JWT (SHA256).
  - `TechnicianJobsController`: İş Takibi, Statü Güncelleme, Detay.
- **DTOs**: Mobil uyumlu veri yapıları (`Shared/DTOs`).
- **Schema**: `ServiceJobHistory` konum ve iş durumu loglama yeteneği kazandı.
- appsettings.json: Connection string ve JWT ayarları.

---

## 2026-02-04 (v5.0 - Web API Architecture Foundation)

### 🏗️ Multi-Project Mimari Geçişi
- **KamatekCrm.Shared** class library oluşturuldu (platform-agnostic).
- Tüm `Models/` ve `Enums/` klasörleri Shared projeye taşındı.
- `ViewModelBase` (INotifyPropertyChanged) Shared'a eklendi.
- WPF projesi artık Shared'ı referans olarak kullanıyor.
- 35+ namespace hatası düzeltildi (XAML + C#).
- **Proje Yapısı**:
  ```
  KamatekCRM/
  ├── KamatekCrm/          # WPF Desktop App
  ├── KamatekCrm.Shared/   # Shared Models & Enums
  └── KamatekCrm.API/      # Web API ✓
  ```

---

## 2026-02-04 (v4.5 - UI Polish)

### 🎨 Arayüz İyileştirmeleri
- **Satın Alma (Purchase Order)**: `PurchaseOrderView` gölgelendirme (Elevation) ayarları optimize edildi.

---

## 2026-02-03 (v4.4 - Modern UI Overhaul)

### 🎨 Material Design Transformation
- **Tedarikçiler (Suppliers)**:
  - Liste ve Detay panelleri modern "Card" yapısına geçirildi.
  - Arama kutusu "Floating Hint" ve ikon desteğiyle güncellendi.
  - Butonlar Material ikonlar ve gölgelendirmelerle yenilendi.
- **Satın Alma (Purchase Order)**:
  - Ürün girişi için özel "Floating Card" paneli tasarlandı.
  - Tablo yapısı "Striped" ve geniş aralıklı hale getirildi.
  - "Onayla ve Stoklara İşle" butonu FAB (Floating Action Button) stiliyle vurgulandı.

---

## 2026-02-03 (v4.3 - PDF & Stock Parsing)

### 📄 PDF Fatura Aktarımı ve Stok Güncelleme
- **PDF Parser**: `PdfPig` ile fatura okuma servisi (`PdfInvoiceParserService`) eklendi.
- **Önizleme Ekranı**: `PdfImportPreviewWindow` ile okunan veriler tablo formatında gösteriliyor, düzenlenebiliyor.
- **Akıllı Eşleşme**: Ürün adı üzerinden veritabanındaki ürünlerle eşleşme kontrolü.
- **Stoklara İşle**: `PurchaseOrderView` altına "KAYDET VE STOKLARA İŞLE" butonu eklendi. Bu buton siparişi "Completed" statüsünde kaydedip, ilgili ürünlerin `TotalStockQuantity` ve `PurchasePrice` değerlerini anında günceller.

---

## 2026-02-03 (v4.2 - Hotfix Compilation)

### 🚑 Critical Fixes
- **DI Failure (CS7036)**: `MainContentViewModel` artık `IUnitOfWork` bekliyor ve `NavigationService` bu bağımlılığı doğru şekilde inject ediyor. (Namespace hatası giderildi).
- **Null Safety (CS8618)**: `EnumBindingSource.EnumType` özelliği varsayılan değer (`typeof(object)`) ile başlatıldı.
- **XAML Errors (MC3000/MC3072)**: `PurchaseOrderView` ve `SuppliersView` yeniden yazılarak hatalı karakterler ve geçersiz `Padding` kullanımları temizlendi.

---

## 2026-02-03 (v4.1 - Greenfield Clean Slate)

### 🧹 Complete Module Rewrite (Suppliers & PurchaseOrder)
- **Zero Legacy Code**: Tüm eski kodlar silindi ve `implementation_tasks` JSON yönergesine göre sıfırdan yazıldı.
- **Strict MVVM**: View arkasında kod bırakılmadı. Tüm mantık ViewModel'de toplandı.
- **Suppliers Module**:
    - `SuppliersViewModel`: `LoadData` ctor içinde çağırılıyor. `SearchText` ile canlı filtreleme.
    - `SuppliersView`: Rigid Grid Layout (StackPanel hataları önlendi). Hardcoded `#1A237E` butonlar.
- **Purchase Order Module**:
    - `PurchaseOrderViewModel`: `AddManualItem` mantığı cilalandı (Adet > 0 kontrolü, Toplam hesabı).
    - `PurchaseOrderView`: 3-Satır Grid Yapısı. Padding hatalarını önlemek için Border kullanımı.
    - **Manuel Giriş**: Ürün seçimi, miktar ve fiyat girişi ile `CurrentOrderItems` listesine ekleme.

---

## 2026-02-03 (v4.0 - Module Rebuild)

### 🚀 Yeniden Yazılan Modüller
- **Suppliers Module (Rewritten)**:
    - View/ViewModel sıfırdan yazıldı. IUnitOfWork + Async/Await mimarisi.
    - Tasarım: Sol liste, Sağ detay (TabControl).
    - Özellikler: Canlı arama, bakiye renklendirme, detaylı iletişim bilgileri.
- **Purchase Order Module (Rewritten)**:
    - View/ViewModel sıfırdan yazıldı. Strict Business Rules entegre edildi.
    - Tasarım: Header, Manuel Giriş (Hızlı), Grid.
    - Kurallar: Tedarikçi seçimi zorunlu, Stok artışı sadece "Teslim Al" ile.
    - Manuel Giriş: Ürün listesi alias'ı ve hızlı ekleme paneli.

### 🎨 UI & UX
- **Hardcoded Styles**: Tüm butonlar `#1A237E` (Lacivert) ve `White` (Beyaz) ile sabitlendi.
- **DataGrid**: Premium Stil uygulandı.

---

## 2026-02-02 (Hotfix v3.2 - Critical Response)

### 🚑 Emergency Fixes (Suppliers & Purchase Order)
- **[CRITICAL] SuppliersView Binding Restore**: `SuppliersView` içerisindeki `ListBox` bileşeni `DataGrid` ile değiştirildi. Binding kaynağı boş olan `FilteredSuppliers` yerine doğrudan `Suppliers` koleksiyonuna yönlendirildi (Code 102).
- **[CRITICAL] PurchaseOrder UI Injection**: "Manuel Ürün Ekle" paneli istenilen XAML yapısıyla (GroupBox, Grid, ToolTip'ler) `ItemsGrid` üzerine zorla enjekte edildi.
- **[CRITICAL] ProductList Binding**: `PurchaseOrderViewModel` içerisinde `ProductList` alias'ı ve manuel giriş property'leri (ManualQuantity, etc.) tanımlandı.
- **[STYLE] Force Visibility**: `ModernButton` stili için renkler (#1A237E / White) stil dosyasında override edildi.

---

## 2026-02-02 (Hotfix v3.1)

### 🚑 Kritik Arayüz ve Fonksiyon Düzeltmeleri
- **Tedarikçiler Modülü**: `SuppliersViewModel` tamamen yeniden yazılarak beyaz ekran sorunu giderildi. Artık veriler `IUnitOfWork` üzerinden güvenli şekilde yükleniyor.
- **Görünmez Butonlar**: `Styles.xaml` içerisindeki `ModernButton` stiline zorla renk ataması (#1A237E) yapılarak temadan kaynaklı görünmezlik sorunu çözüldü.
- **Satın Alma Manuel Giriş**: `PurchaseOrderView` içerisine eksik olan "Manuel Ürün Ekleme" paneli enjekte edildi. `PurchaseOrderViewModel` tarafında gerekli komut ve property'ler (ProductList, ManualQuantity vb.) eklendi.

---

## 2026-01-30

### 🎨 Premium Design System (Refactor v2.0)
Refactored application visual identity from "Material Design" to "Premium Enterprise UX".

**Design System Updates:**
- **New Color Palette:**
  - `PrimaryHue` (#2C3E50) - Dark Blue/Gray theme.
  - `SecondaryHue` (#27AE60) - Green for primary actions.
  - `Background` (#F5F7FA) - Light gray modern background.
- **New Styles:**
  - `BtnPrimary`: Solid green, shadow depth 2, rounded corners (Radius 6).
  - `BtnSecondary`: Transparent/Outlined blue-gray.
  - `PremiumDataGrid`: No vertical lines, transparent header, 40px row height.
  - `PremiumTextBox`: Outlined, 42px height, refined padding.
  - `CardContainer`: White background, shadow depth 1, consistent padding.
  - **Restored & Updated:** `FilterBarPanel`, `CategoryToggleButton`, `IconActionButton`, `NavButton` adapted to new theme.
- **Legacy Compatibility:**
  - Existing `ModernButton`, `ModernDataGrid` etc. mapped to new Design Tokens.

---

## 2026-01-29 (v3)

### 🤖 Yapay Zeka & ERP Standartları

**AI Fatura Tarayıcı (Yeni Modül):**
- `PdfPig` kütüphanesi ile PDF faturalardan metin okuma
- Regex ve Levenshtein Distance ile akıllı ürün eşleştirme
- "Bilinmeyen Ürünler" için manuel onay mekanizması
- `PurchaseOrderView` üzerinden "Faturadan Tara" butonu

**ERP Faz 1: Finansal Çekirdek (Maliyet & Güvenlik):**
- **WAC (Ağırlıklı Ortalama Maliyet):** Stok girişlerinde maliyet otomatik hesaplanıyor.
- **Inventory.cs:** `AverageCost` alanı eklendi.
- **PurchaseOrder.cs:** `CurrencyCode` ve `ExchangeRate` alanları eklendi.
- **Migration:** `UpgradeToProfessionalERP_Phase1` oluşturuldu.

**ERP Standartları (Mal Kabul):**
- **Accrual Accounting:** "Teslim Al" işlemi artık Kasa'dan para çıkışı yapmıyor.
- Sadece Tedarikçi Bakiyesi (Borç/Payable) artırılıyor.
- Stoklar `WaitingInventoryEntry` statüsü ile yönetilebiliyor.
- `PurchaseStatus.Completed` durumu eklendi.

---

## 2026-01-29 (v2)

### 🏭 Uçtan Uca Profesyonel Satın Alma Sistemi

**Model Güncellemeleri:**
- `Supplier.cs` → LeadTimeDays, MinOrderAmount, CurrencyCode eklendi
- `PurchaseOrder.cs` → SupplierId (FK), WarehouseId (FK), IsProcessedToStock, ProcessedDate eklendi
- Migration: `ExtendSupplierAndPurchaseOrder`

**Stok Entegrasyonu:**
- `PurchaseOrderViewModel.ReceiveGoods()` güncellendi:
  - Dinamik WarehouseId kullanımı (hardcoded 1 yerine)
  - `IsProcessedToStock` flag ile çift işlem önleme
  - `SupplierId` FK ile tedarikçi bağlantısı
  - `StockTransaction.UserId` audit logging

**Dijital Arşiv Entegrasyonu:**
- `SuppliersViewModel` → AttachmentService bağlantısı
- Dosya ekleme (OpenFileDialog), silme, açma komutları
- `SupplierAttachments` ObservableCollection

**3-Panel SuppliersView UI:**
- Panel 1: Liste + Arama + Borçlu/Pasif filtreleri
- Panel 2: Detay/Düzenleme (Firma, Ticari, Banka bilgileri)
- Panel 3: Sipariş Geçmişi + Dijital Arşiv (dosya önizleme)

---

## 2026-01-29

### 🏢 Gelişmiş Tedarikçi Modülü (SRM v2)

**Yeni Model Özellikleri:**
- `Enums/SupplierType.cs` - Tedarikçi tipi enum (Toptancı, Servis, Üretici, Distribütör)
- `Supplier.cs` güncellendi: `SupplierType`, `PaymentTermDays`, `Website` alanları
- DataAnnotation doğrulamaları: `[EmailAddress]`, `[Url]`, `[Range]`

**Mimari İyileştirmeler:**
- `IUnitOfWork` → `SaveChangesAsync()` async metot eklendi
- `SuppliersViewModel` → IUnitOfWork enjeksiyonu (DI ready)
- Tüm CRUD operasyonları async/await ile yeniden yazıldı

**Gelişmiş Filtreleme:**
- Borçlu tedarikçiler filtresi (`ShowDebtorsOnly`)
- Pasif tedarikçileri göster (`ShowInactiveSuppliers`)
- Tip bazlı filtreleme (`SelectedSupplierTypeFilter`)

**UI/UX İyileştirmeleri:**
- `SuppliersView.xaml` tam yeniden tasarım
- Bakiye DataTrigger renklendirmesi (Kırmızı: Borçlu, Yeşil: Dengeli)
- Filtreleme paneli (CheckBox + ComboBox)
- Yeni form alanları: Tip, Vade Günü, Web Sitesi
- Sipariş geçmişine "Detay" butonu

**Yeni Converter:**
- `Converters/GreaterThanZeroConverter.cs` - DataTrigger koşulları için

---

## 2026-01-28



**UI Yeniden Tasarımı:**
- `Views/SuppliersView.xaml` - Modern iki panelli layout (Sol: Liste, Sağ: Detay)
- Arama kutusu ile gerçek zamanlı filtreleme
- Tab yapısıyla "Genel Bilgiler" ve "Sipariş Geçmişi" bölümleri
- TwoWay binding ile tüm form alanları düzenlenebilir
- Finansal özet kartları (Toplam Sipariş, Bakiye, Durum)

**ViewModel Güncellemeleri:**
- `ViewModels/SuppliersViewModel.cs` - Arama/filtreleme mantığı eklendi
- `FilteredSuppliers` - Gerçek zamanlı filtrelenmiş tedarikçi listesi
- `SupplierPurchaseHistory` - Tedarikçinin sipariş geçmişi
- Soft delete implementasyonu (IsActive = false)
- DbUpdateException handling eklendi

**Düzeltilen Hatalar:**
- CS1061: `PurchaseOrder.SupplierId` referansı kaldırıldı (özellik mevcut değil)
- XamlParseException: `RoundedButtonStyle` yerine `ModernButton` kullanıldı

---

### 📋 Hızlı Kabul Modern UI (RepairRegistrationWindow)

**Tamamen Yeniden Tasarlanan Form:**
- `Views/RepairRegistrationWindow.xaml` - 3 sütunlu modern kart layout (320 satır)
- Sol Panel: Müşteri seçimi + ⚡ Hızlı müşteri ekleme toggle
- Orta Panel: 📹 Kamera / 🔔 Diafon kategori seçimi + Manuel cihaz tipi girişi
- Sağ Panel: Arıza açıklaması + Aksesuar checkbox'ları

**Yeni ViewModel Özellikleri:**
- `ViewModels/RepairViewModel.cs` - 115+ satır yeni kod
- `IsCameraCategory` / `IsDiafonCategory` - Kategori seçimi
- `DeviceTypeOptions` - Kategoriye göre değişen cihaz tipi listesi
- `SelectedDeviceTypeName` - Manuel giriş destekli cihaz tipi
- `AccessoryAdapter`, `AccessoryCable`, `AccessoryRemote` - Aksesuar takibi
- `IsQuickAddCustomer`, `QuickCustomerName`, `QuickCustomerPhone` - Hızlı müşteri
- `UpdateDeviceTypeOptions()` - Dinamik liste yükleme

---

### 🚀 Pro UX ve Satınalma Mantığı Yükseltmesi

**Modül 1: Akıllı Login "Beni Hatırla"**
- `Properties/Settings.settings` - RememberMe ve SavedUsername ayarları eklendi
- `Properties/Settings.Designer.cs` - Generated property'ler güncellendi
- `ViewModels/LoginViewModel.cs` - RememberMe mantığı, LoadSavedCredentials() ve SaveCredentials() metodları eklendi
- `Views/LoginView.xaml` - "Beni Hatırla" CheckBox eklendi

**Modül 2: Modern Tamir/Servis Formu UI**
- `Views/NewServiceJobWindow.xaml` - MaterialDesign kartları ve gölge efektleri ile yeniden tasarlandı
- Üst kısımda müşteri bilgi kartı (Müşteri, Öncelik, Tarih seçimi)
- İkonlu alan başlıkları (👤, 🚨, 📅, 🏠, 📂, 📝, 📦)
- Filtrelenebilir IsEditable ComboBox'lar
- DropShadowEffect ile premium görünüm

**Modül 3: Kapsamlı Satınalma Mantığı**
- `Models/PurchaseOrderItem.cs` - TaxRate, DiscountRate, SubTotal, DiscountAmount, TaxAmount, LineTotal property'leri eklendi
- Migration: `AddPurchaseOrderItemFinancials` oluşturuldu ve uygulandı
- `ViewModels/PurchaseOrderViewModel.cs` - OrderSubTotal, OrderTaxAmount, OrderDiscountAmount, OrderGrandTotal ve CalculateOrderTotals() eklendi
- `Views/PurchaseOrderView.xaml` - Sipariş özeti footer paneli eklendi (Ara Toplam, İndirim, KDV, Genel Toplam)

---

### 🗄️ Hibrit Veritabanı Mimarisi

**Yeni Özellik:** Uygulama artık hem SQLite (geliştirme) hem de SQL Server (production) veritabanlarını destekliyor.

**Yapılandırma:**
- `appsettings.json` dosyasından `DatabaseType` değeri ile provider seçimi
- `"SQLite"` veya `"SqlServer"` değerleri destekleniyor
- Bağlantı dizeleri merkezi olarak yönetiliyor

**Eşzamanlılık Kontrolü:**
- `Inventory` entity'sine `RowVersion` özelliği eklendi
- `UnitOfWork.SaveChangesWithConcurrencyHandling()` metodu ile optimistic concurrency desteği
- "Kayıt başka bir kullanıcı tarafından değiştirildi" hatası artık düzgün yakalanıyor

**Yeni Dosyalar:**
- `appsettings.json` - Veritabanı ve uygulama yapılandırması
- `Settings/AppSettings.cs` - Yapılandırma okuyucu (singleton)

**Güncellenen Dosyalar:**
- `KamatekCrm.csproj` - SqlServer ve Configuration NuGet paketleri eklendi
- `Data/AppDbContext.cs` - Dinamik provider seçimi
- `Models/Inventory.cs` - RowVersion özelliği
- `Repositories/UnitOfWork.cs` - Concurrency handling

---

### 🏗️ Enterprise Mimari Dönüşümü

**Yeni Mimari Bileşenler:**
- ✅ **Unit of Work Pattern** - Transaction yönetimi merkezileştirildi (`Repositories/IUnitOfWork.cs`, `UnitOfWork.cs`)
- ✅ **Domain Services** - İş mantığı ViewModel'lerden ayrıldı:
  - `SalesDomainService` - Thread-safe satış işlemleri (SemaphoreSlim)
  - `InventoryDomainService` - Thread-safe stok operasyonları
- ✅ **Event Bus** - ViewModel'ler arası iletişim (`Services/EventAggregator.cs`, WeakReference ile memory-safe)
- ✅ **Custom Exceptions** - Özelleştirilmiş hata türleri:
  - `InsufficientStockException` - Yetersiz stok
  - `ReferentialIntegrityException` - Bağımlılık hatası
- ✅ **Event DTOs** - Pub/Sub mesajları (`SaleCompletedEvent`, `StockUpdatedEvent`)

**Refactored ViewModels:**
- `DirectSalesViewModel.cs` - 140 satır → 60 satır (SalesDomainService'e delege)
- `StockTransferViewModel.cs` - 70 satır → 30 satır (InventoryDomainService'e delege)

**Yeni Dosyalar:**
- `Repositories/IUnitOfWork.cs`
- `Repositories/UnitOfWork.cs`
- `Services/Domain/ISalesDomainService.cs`
- `Services/Domain/SalesDomainService.cs`
- `Services/Domain/IInventoryDomainService.cs`
- `Services/Domain/InventoryDomainService.cs`
- `Services/EventAggregator.cs`
- `Exceptions/InsufficientStockException.cs`
- `Exceptions/ReferentialIntegrityException.cs`
- `Events/SaleCompletedEvent.cs`
- `Events/StockUpdatedEvent.cs`

---

### 🎨 UI Profesyonelleştirme - Sprint 1-3

**Sprint 1: Foundation & UI Yenileme**
- ✅ **Dark Mode / Light Mode** - Tam tema sistemi (`LightTheme.xaml`, `DarkTheme.xaml`)
- ✅ **Collapsible Sidebar** - Daraltılabilir menü (65px ↔ 250px)
- ✅ **Sayfa Geçiş Animasyonları** - FadeIn, SlideIn efektleri (`Animations.xaml`)
- ✅ **Loading Skeleton** - Dashboard yükleme göstericisi

**Sprint 2: Dashboard Revival**
- ✅ **Modern Dashboard** - Hover efektli widget kartları
- ✅ **LiveCharts Entegrasyonu** - 7 günlük performans grafiği + İş kategorileri pie chart

**Sprint 3: UX Polish**
- ✅ **Quick Add Modal (Ctrl+K)** - Evrensel arama/aksiyon menüsü
- ✅ **Keyboard Shortcuts** - Ctrl+B (sidebar), Ctrl+D (tema), Ctrl+N (arıza kabul)

**Yeni Dosyalar:**
- `Resources/Themes/LightTheme.xaml`
- `Resources/Themes/DarkTheme.xaml`
- `Resources/Animations.xaml`
- `Services/ThemeService.cs`
- `Views/LoadingSkeletonControl.xaml`
- `Views/QuickAddModal.xaml`
- `Properties/Settings.settings` + `Settings.Designer.cs`

**Güncellenen Dosyalar:**
- `App.xaml` - Tema ve animasyon ResourceDictionary entegrasyonu
- `MainContentView.xaml` - Tamamen yeniden tasarım
- `MainContentViewModel.cs` - Sidebar, tema ve QuickAdd komutları
- `DashboardView.xaml` - Modern grafik tasarımı
- `DashboardViewModel.cs` - LiveCharts veri kaynakları

### 🐛 Hata Düzeltmeleri (ViewModel Fixes)
- **AnalyticsViewModel**: Null Reference uyarıları için constructor initialization yapıldı. Deprecated `PrimaryValue` kullanımı `Coordinate.PrimaryValue` ile güncellendi.
- **FinancialHealthViewModel**: `ProjectProfitItem` ve grafik serileri için null safety sağlandı.
- **RoutePlanningViewModel**: `MapHtmlContent` ve marker özellikleri initialize edildi.

### 🚑 Kritik Düzeltmeler (Hotfix)
- **Veritabanı**: Giriş hatasına (`SQLite Error 1: no such column: c.Latitude`) neden olan eksik kolonlar için `AddCustomerCoordinates` migration'ı uygulandı. `Customers` tablosuna `Latitude` ve `Longitude` eklendi.
- **UI**: `AnalyticsView` ve `FinancialHealthView` açılırken çökmesine neden olan (`System.Windows.Markup.XamlParseException`) eksik `DropShadow` kaynağı `App.xaml` içerisine eklendi.
- **UI**: Finansal Sağlık raporunun beyaz ekran açılmasına neden olan eksik `DataTemplate` tanımı `App.xaml` dosyasına eklendi.
- **UI**: Sidebar menüde mükerrer olan "İş Analitiği" butonu kaldırıldı.
- **Refactoring**: `FinancialHealthViewModel` içerisinde veri yükleme işlemi güvenli hale getirildi (Try-Catch eklendi), olası veritabanı hatalarında kullanıcının bilgilendirilmesi sağlandı.

---

## 2026-01-27

### 🔍 Kapsamlı Kod İncelemesi ve Düzeltmeler
Kullanıcı perspektifinden uygulama test edildi, 10 sorun tespit edildi ve kritik olanlar düzeltildi.

**Düzeltilen Sorunlar:**
- **InvertedBooleanToVisibilityConverter**: Yeni converter eklendi (`Converters/InvertedBooleanToVisibilityConverter.cs`). "Boş durum" metinleri artık doğru görünüyor.
- **RepairListView UI**: Test metni ("Filter Section OK") silindi, İngilizce metin Türkçeye çevrildi.
- **Fotoğraf Ekleme**: Çoklu fotoğraf seçimi eklendi, fotoğraf eklendikten sonra UI otomatik yenileniyor.
- **Dashboard Karşılama**: "Hoşgeldin, Admin" statik metni dinamik kullanıcı adına dönüştürüldü.
- **Kullanılmayan Kod**: `ExecuteNotifyCustomer`, `ExecuteShowPhotos`, `ExecuteOpenDetail` metodları silindi.

**Güncellenen Dosyalar:**
- `Converters/InvertedBooleanToVisibilityConverter.cs` [YENİ]
- `App.xaml` - Yeni converter tanımı
- `Views/RepairListView.xaml` - UI düzeltmeleri
- `ViewModels/RepairListViewModel.cs` - Ölü kod temizliği + Fotoğraf UI yenileme
- `ViewModels/DashboardViewModel.cs` - Dinamik kullanıcı adı

---

## 2026-01-25

### ✅ Enterprise ERP Megamodule
CRM uygulaması 4 büyük kurumsal modül ile ERP seviyesine yükseltildi.

| Modül | Açıklama |
|-------|----------|
| **BI Analytics** | LiveCharts ile 6 aylık trend, kategori dağılımı, top 5 ürün |
| **B2B Procurement** | Tedarikçi borç takibi, PO oluşturma, stok güncelleme |
| **Digital Archive** | Attachment entity, GUID dosyalar, AppData arşivi |
| **RBAC** | 5 granular izin: Finance, Analytics, Delete, Purchase, Settings |

**Yeni Dosyalar:**
- `ViewModels/AnalyticsViewModel.cs`
- `ViewModels/PurchaseOrderViewModel.cs`
- `Views/AnalyticsView.xaml`
- `Views/PurchaseOrderView.xaml`
- `Models/Attachment.cs`
- `Enums/AttachmentEntityType.cs`
- `Services/AttachmentService.cs`

**Güncellenen Dosyalar:**
- `Models/User.cs` - RBAC izin alanları
- `Models/Supplier.cs` - Balance, Email, IsActive
- `Services/AuthService.cs` - RBAC property'leri
- `Views/MainContentView.xaml` - Yeni navigation butonları
- `App.xaml` - DataTemplates

**Migration:** `AddERPEnhancements`

### 🐛 Hata Düzeltmeleri
- **AnalyticsViewModel**: EF Core LINQ Translation hatası (`IsIncome`/`IsExpense` unmapped properties) düzeltildi. Sorgularda explicit `TransactionType` kontrolüne geçildi.
- **FinanceViewModel**: `LoadData()` metodunda benzer LINQ Translation hatası düzeltildi (`IsExpense` yerine `TransactionType`).
- **PurchaseOrderViewModel**: `TotalSupplierDebt` hesaplarken SQLite `Sum` (decimal) hatası giderildi (`AsEnumerable` ile client-side calculation).
- **DashboardViewModel**: `LoadFinancialSummary` metodunda LINQ Translation hatası oluşabilecek sorgular explicit enum kontrolü ile güvenli hale getirildi.
- **AnalyticsViewModel & DashboardViewModel**: `decimal` tipindeki alanlar için SQLite `Sum` hatası (`NotSupportedException`) giderildi. Hesaplama client-side (`AsEnumerable`) tarafına alındı.

### [NEW] Ultimate Smart ERP Ecosystem
- **Kanban Sales Pipeline**: `SalesPipelineView` ile sürükle-bırak destekli görsel satış takibi (Lead -> Won). `PipelineStage` enum yapısı ve `ServiceProject` entegrasyonu.
- **Technician Scheduler**: `SchedulerView` ile atanmamış işlerin teknisyenlere sürükle-bırak yöntemiyle takvim üzerinde atanması. `ServiceJob.AssignedUserId` alanı.
- **SLA Automation Engine**: `SlaService` ile süresi gelen bakım sözleşmelerinden (`MaintenanceContract`) otomatik iş emri oluşturma (arka plan servisi).
- **Smart Action Center**: `MainContentView` başlığında Bildirim Merkezi (Çan ikonu). Düşük stok ve unutulmuş teklif bildirimleri (`NotificationService`).
- **Veritabanı**: `PipelineStage` Enum, `MaintenanceContracts` tablosu eklendi. `JobCategory.Other` seçeneği eklendi. Migration: `AddSmartERPCore`.
- **UI FIX**: "Beyaz Ekran" sorunu (ViewModel binding hatası) ve "Görünmeyen Bildirim Butonu" (Stil hatası) düzeltildi.
- **CRITICAL FIX**: `SlaService` UI bloklama sorunu giderildi (Async Task). `PipelineViewModel` DragDrop çökmesi (InvalidCast) düzeltildi. `NotificationService` bildirim döngüsü engellendi (Stateful memory).
- **UX/UI OVERHAUL**: Klavye kısayolları (Enter/Esc), TabIndex sıralaması, DataGrid 'Delete' tuşu desteği ve Numeric TextBox stilleri eklendi.
- **REPAIR SYSTEM FIX**: Arıza listesi ("RepairListView") asenkron yüklenecek şekilde (`async/await`) optimize edildi.
- **MAJOR FEATURE**: Arıza Takip Ekranı (`RepairTrackingWindow`) tamamen yenilendi.
    - **Parça Yönetimi**: Arıza kaydına malzeme/yedek parça ekleme özelliği getirildi.
    - **Maliyet Takibi**: Malzeme + İşçilik + İndirim hesaplaması eklendi.
    - **Servis Fişi**: Müşteri için PDF servis formu yazdırma özelliği entegre edildi.
    - **Stok Entegrasyonu**: Kullanılan parçaların stoktan otomatik düşülmesi sağlandı.
- **NEW FEATURES**: Arıza Listesi'ne "+ Yeni Arıza Kaydı" butonu eklendi. Navigasyon iyileştirildi.

---

## 2026-01-24

### ✅ Kritik Üretim Düzeltmeleri (Production-Ready)
5 kritik sistem açığı/hatası düzeltildi:

| # | Sorun | Düzeltme |
|---|-------|----------|
| 1 | SMS simülasyon modunda | Production-ready API çağrısı (placeholder kontrollü) |
| 2 | Gmail normal şifre hatası | Google App Password zorunluluğu + dokümantasyon |
| 3 | POS'ta stok düşmüyor (inventory yoksa) | Eksik Inventory kaydı otomatik oluşturma |
| 4 | Temp PDF dosyaları birikimi | try-finally ile otomatik temizlik |
| 5 | Restore sonrası Ghost Data | Restart önceliklendirme, EF cache bypass |

**Güncellenen Dosyalar:**
- `Services/SmsService.cs`
- `Services/EmailService.cs`
- `ViewModels/DirectSalesViewModel.cs`
- `ViewModels/ProjectQuoteEditorViewModel.cs`
- `ViewModels/SettingsViewModel.cs`

---

## 2026-01-23

### ✅ İletişim Motoru (SMS & E-Posta)
Müşterilerle iletişim için profesyonel SMS ve E-Posta altyapısı eklendi.

**Yeni Servisler:**
- `Services/EmailService.cs` [YENİ]: SMTP ile PDF teklif gönderimi
- `Services/SmsService.cs` [YENİ]: HTTP API ile SMS bildirimi (NetGSM/Twilio uyumlu)

**Entegrasyonlar:**
- `ProjectQuoteEditorViewModel.cs`: "📧 E-POSTA GÖNDER" komutu eklendi (PDF eklentiyle)
- `RepairViewModel.cs`: Cihaz "Hazır" durumuna geçtiğinde otomatik SMS bildirimi
- `ProjectQuoteEditorWindow.xaml`: E-posta gönder butonu eklendi

---

### ✅ Otomatik Yedekleme Sistemi
SQLite veritabanı için kapsamlı yedekleme ve geri yükleme işlevselliği.

**Yeni Dosyalar:**
- `Services/BackupService.cs` [YENİ]: SQLite Backup API + ZIP sıkıştırma
- `ViewModels/SettingsViewModel.cs` [YENİ]: Yedekleme UI mantığı
- `Views/SettingsView.xaml` [YENİ]: Ayarlar ekranı (Yedek Al / Yedekten Yükle)

**Özellikler:**
- **Manuel Yedekleme:** "💾 ŞİMDİ YEDEK AL" butonu
- **Geri Yükleme:** "📂 YEDEKTEN YÜKLE" butonu (ZIP seçimi)
- **Otomatik Çıkış Yedeği:** Uygulama kapanırken arka planda yedek alınır
- **Yedek Konumu:** `Belgelerim/KamatekBackups/KamatekBackup_YYYY-MM-DD_HHmm.zip`

**Entegrasyonlar:**
- `MainContentView.xaml`: Sidebar'a "⚙️ Ayarlar" butonu eklendi
- `MainContentViewModel.cs`: `NavigateToSettingsCommand` eklendi
- `App.xaml`: `SettingsViewModel` → `SettingsView` DataTemplate eşlemesi
- `App.xaml.cs`: `OnExit` override ile otomatik yedekleme

---

### ✅ İş Emirleri Sadeleştirmesi
- `Views/ServiceJobsView.xaml`: Liste kaldırıldı, sadece "Yeni İş Emri" oluşturma butonu kaldı
- Mevcut işler için "🔧 Tamir Listesi" ve "🚜 Saha İşleri" kullanılacak

### ✅ Dashboard Intelligence (Komut Merkezi)
- `DashboardViewModel.cs`: 3 widget ile yeniden yazıldı:
  1. **🚨 Kritik Uyarılar**: Stok <= 5 olan ürünler (renk kodlu badge)
  2. **🔧 Bugünün İşleri**: Bugün planlanan işler + Teslime hazır tamirler
  3. **💰 Aylık Özet**: Toplam satış, tamamlanan işler, aktif işler
- `DashboardView.xaml`: Modern 3-kolon layout

---

## 2026-01-22

### ✅ Profesyonel Liste Görünümleri (Yeni)
**Dosyalar:**
- `ViewModels/RepairListViewModel.cs` [YENİ]: Tamir listesi filtreleme ve aksiyonlar
- `ViewModels/FieldJobListViewModel.cs` [YENİ]: Saha işleri filtreleme ve aksiyonlar
- `Views/RepairListView.xaml` [YENİ]: Modern tamir listesi UI
- `Views/FieldJobListView.xaml` [YENİ]: Modern saha işleri UI
- `Views/MainContentView.xaml`: Sidebar navigasyon butonları eklendi
- `ViewModels/MainContentViewModel.cs`: Navigasyon komutları eklendi
- `Resources/Styles.xaml`: StatusBadge, IconActionButton, FilterBarPanel, CategoryToggleButton stilleri

**Özellikler:**
- Durum badge'leri (renk kodlu pill'ler)
- Gelişmiş filtre bar (tarih, durum, arama)
- Kategori multi-select toggle butonları
- Aksiyon butonları (SMS, Fotoğraf, Yazdır, Harita, Tamamla)
- Google Maps entegrasyonu (saha işleri)

---

## 2026-01-19

### ✅ Tamamlanan

#### Manuel Stok Sayım Modülü (Yeni Özellik)
Mevcut Excel tabanlı sayım modülüne ek olarak, tekil veya belirli ürünleri hızlıca sayma imkanı sağlayan "Manuel Hızlı Sayım" sekmesi eklendi.

- **StockCountView.xaml**: TabControl yapısına dönüştürüldü
  - Tab 1: "📤 Excel Toplu Sayım" (mevcut)
  - Tab 2: "🖐️ Manuel Hızlı Sayım" (yeni)
  
- **StockCountViewModel.cs**: Manuel sayım mantığı eklendi
  - Ürün arama (SKU, Barkod, Ürün Adı)
  - Listeye ekleme/çıkarma
  - Stok fark hesaplama (renk kodlu)
  - `StockTransaction` kayıtları oluşturma (MANUAL-* referans)
  
- **Yeni Özellikler:**
  - Barkod tarayıcı desteği (arama alanına odaklanarak)
  - Anlık fark hesaplama (yeşil: fazla, kırmızı: eksik)
  - Özet kartları (Toplam, Fazla, Eksik, Farklı)
  - ReferenceId formatı: `MANUAL-yyyyMMdd-HHmmss-WarehouseId`

#### Perakende Satış (POS) Modülü (Yeni)
Mouse/Klavye için optimize edilmiş doğrudan satış modülü eklendi.

- **Yeni Entity'ler:**
  - `Models/SalesOrder.cs` - Satış siparişi
  - `Models/SalesOrderItem.cs` - Sipariş kalemi
  - `Enums/PaymentMethod.cs` - Ödeme yöntemi (Nakit/Kredi Kartı)

- **DirectSalesViewModel.cs**: POS iş mantığı
  - Anlık ürün arama (Ad/Model/SKU)
  - Sepet yönetimi (ekle/çıkar/miktar değiştir)
  - Nakit ve Kredi Kartı ödeme işleme
  - StockTransaction (Sale) kaydı

- **DirectSalesWindow.xaml**: Bölünmüş ekran arayüzü
  - Sol Panel (%60): Ürün Kataloğu + Arama
  - Sağ Panel (%40): Sepet + Ödeme Butonları
  - Büyük tıklanabilir satırlar (RowHeight=45)

- **Navigation Entegrasyonu:**
  - `MainContentViewModel.cs`: `OpenDirectSalesCommand` eklendi
  - `MainContentView.xaml`: "🛒 HIZLI SATIŞ (KASA)" yeşil buton eklendi

---

## 2026-01-14

### ✅ Tamamlanan

#### Servis İş Emri UI Yeniden Tasarımı (Single-Page Form)
4 adımlı Wizard kaldırıldı, tek sayfalık form + çoklu kategori desteği eklendi.

- **NewServiceJobWindow.xaml**: ~510 satır → ~320 satır (JobDetail template'ları kaldırıldı)
- **ServiceJobViewModel.cs**: Wizard mantığı kaldırıldı, CategoryItems eklendi
- **ServiceJob.cs**: `CategoriesJson` alanı eklendi (çoklu kategori JSON)
- **CategorySelectItem.cs**: Checkbox binding için yeni model
- **Yeni Özellikler:**
  - Çoklu kategori seçimi (CheckBox'larla)
  - Yapı Türü seçimi (Müstakil, Apartman, Site, İşyeri)
  - "Tüm Birimlere Uygula" checkbox (malzeme çarpanı)
  - Büyük açıklama kutusu (detay formlarının yerini aldı)
  - 2 sütunlu layout: Sol (İş Bilgileri) / Sağ (Malzeme)
- **Migration**: `AddMultiCategorySupport`

---

## 2026-01-16

### ✅ Tamamlanan

#### Proje Editörü Ağaç Yönetimi İyileştirmeleri (Usability)
- **Sibling Addition (Kardeş Ekleme):** "Daire Ekle" komutu artık bir daire seçiliyken de çalışıyor (Kardeş olarak ekler).
- **Gelişmiş Yeniden Adlandırma:** "Yeniden Adlandır / Etiketle" özelliği ile birimlere özel müşteri isimleri atanabilir (örn. 'Daire 5 - Ahmet Bey').
- **UI UX:** Sağ tık menüsünde isimlendirme başlığı güncellendi.
- **Bug Fix:** TreeView'da isim değişikliğinin anlık yansımaması sorunu (`ScopeNode.HeaderDisplay` binding notification) düzeltildi.
- **Bug Fix:** Node silme işleminde `Parent` node'un toplamlarının güncellenmemesi sorunu (`NotifyTotalsChanged`) giderildi.
- **Logic Update:** `AddFlat` mantığı güncellenerek daire seçiliyken kardeş olarak ekleme (Parent üzerinden) işlemi garanti altına alındı.
- **Critical Bug Fix:** `ScopeNode.Children` koleksiyonu `ObservableCollection` tipine dönüştürüldü. Bu sayede TreeView'a yeni eklenen veya silinen node'lar anında arayüze yansıyor.

#### Profesyonel Tamir & Servis Modülü (Yeni)
- **Database:** `ServiceJob` entity'sine cihaz marka/model/seri no, aksesuar ve durum alanları eklendi.
- **Workflow:** `RepairStatus` enum ile 10 adımlı tamir takip süreci tanımlandı.
- **History:** `ServiceJobHistory` tablosu ile her tamir adımı, teknisyen notu ve durum değişikliği loglanıyor.
- **UI:** 
  - `RepairRegistrationWindow`: Detaylı cihaz kabul ekranı.
  - `RepairTrackingWindow`: Master-Detail yapısında aktif arıza takip ve işlem merkezi.
  - **Logic:** `RepairViewModel` ile durum makinesi (State Machine) yönetimi ve history entegrasyonu sağlandı.

#### Genel İyileştirmeler
- **Login Ekranı:** Geçici olarak devre dışı bırakılan giriş ekranı tekrar aktif edildi. Uygulama açılışında artık kullanıcı girişi zorunlu.


---

## 2026-01-12

### ✅ Tamamlanan

#### Profesyonel Mühendislik Tezgahı (Enterprise Quote Editor)
Mevcut basit 'Keşif & Teklif' modülü tamamen yeniden yazıldı.

- **Yeni Modeller:**
  - `Models/ScopeNode.cs` - Recursive, JSON-serializable tree node (Proje > Blok > Kat > Daire)
  - `Models/ScopeNodeItem.cs` - Finansal derinlikli kalem (UnitCost, UnitPrice, LaborCost, MarginPercent, IsOptional)
  
- **ServiceProject Güncellemeleri:**
  - `ProjectScopeJson` - Hiyerarşik yapı ağacı (JSON)
  - `TotalCost` - Toplam maliyet
  - `TotalProfit` - Toplam kar

- **Yeni Servis:**
  - `Services/ProjectScopeService.cs` - JSON serialize/deserialize, Save/Load operations

- **Yeni ViewModel:**
  - `ViewModels/ProjectQuoteEditorViewModel.cs` - Üç panelli workbench mantığı
  - Tree yönetimi: AddBlock, AddFloor, AddFlat, DuplicateNode, RemoveNode
  - Drag & Drop: IDropTarget implementasyonu (gong-wpf-dragdrop)
  - Context menü: Rename, Apply to All Siblings
  - Finansal hesaplamalar: Real-time Maliyet/Kar/Marj

- **Yeni View:**
  - `Views/ProjectQuoteEditorWindow.xaml` - Üç Panelli Komuta Merkezi
  - Sol Panel: Yapı Ağacı (TreeView + live cost badges)
  - Orta Panel: Mahal Listesi (DataGrid - inline editing)
  - Sağ Panel: Ürün & Hizmet Kataloğu (Drag source)
  - Alt Panel: Finansal özet (Maliyet/Satış/Kar/Marj)

- **Bağımlılık:**
  - `gong-wpf-dragdrop` v3.2.1 NuGet paketi eklendi

---

## 2026-01-09

### ✅ Tamamlanan

#### Kurumsal Logo Entegrasyonu
- **Yeni Klasör:** `Assets/Images/` - Görsel dosyalar için
- **Yeni Dosya:** `KamatekLogo.jpg` - Dağ + KAMATEKCRM logosu
- **LoginView.xaml:** Emoji (🏢) yerine logo görseli eklendi
- **MainContentView.xaml:** Sidebar başlığındaki text yerine logo eklendi
- **KamatekCrm.csproj:** Resource tanımı eklendi

#### Proje Ekranları Birleştirme (UI Simplification)
- **Silinen View'lar:**
  - `Views/ProjectWorkflowWindow.xaml` (5 fazlı proje akışı)
  - `Views/DiscoveryQuoteWindow.xaml` (4 adımlı keşif sihirbazı)
  - `Views/ProjectEditorWindow.xaml` (3 panelli workbench)
  
- **Silinen ViewModel'ler:**
  - `ViewModels/ProjectWorkflowViewModel.cs`
  - `ViewModels/DiscoveryQuoteViewModel.cs`
  - `ViewModels/ProjectEditorViewModel.cs`

- **Yeni Basit Arayüz:**
  - `Views/ProjectQuoteWindow.xaml` - TabControl ile 2 sekmeli basit pencere
  - `ViewModels/ProjectQuoteViewModel.cs` - Çarpan mantığı ile teklif oluşturma

- **Özellikler:**
  - Tab 1: Keşif & Yapı (Müşteri seçimi, Proje adı, Yapı tanımı)
  - Tab 2: Teklif Hazırla (Ürün kataloğu, Teklif kalemleri DataGrid)
  - Otomatik hesaplama: Blok × Kat × Daire = Toplam Birim
  - Çarpan mantığı: Birim başına adet × Toplam birim = Toplam miktar

- **MainContentView Güncellemesi:**
  - 3 eski buton kaldırıldı (Proje Akışı, Keşif & Teklif, Proje Editörü)
  - 1 yeni buton eklendi: "🏗️ PROJE & TEKLİF" (mavi vurgulu)

---

## 2025-01-05

### ✅ Tamamlanan

#### Proje Editörü (3 Panelli Workbench)
- **Yeni Enum:** `Enums/NodeType.cs` - Tree node tipleri (Project, Block, Floor, Flat, Zone)
- **Yeni Modeller:**
  - `Models/StructureTreeItem.cs` - Recursive tree node yapısı
  - `Models/ScopeItem.cs` - Mahal kalemi (ürün ataması)
- **Yeni Servis:** `Services/StructureGeneratorService.cs` - Otomatik yapı oluşturucu
- **Yeni ViewModel:** `ViewModels/ProjectEditorViewModel.cs` - 3 panel workbench mantığı
- **Yeni View:** `Views/ProjectEditorWindow.xaml` - Yapı Ağacı + Mahal Listesi + Ürün Kataloğu

**Özellikler:**
- TreeView ile hiyerarşik yapı görünümü
- Node bazlı ürün ataması (Scope)
- Smart Propagation: İçeriği kopyala → Tümüne yapıştır
- Recursive maliyet hesaplama

---

#### Keşif ve Fiyat Teklifi Modülü (Discovery & Quote Manager)
- **Yeni Enum'lar:**
  - `Enums/StructureType.cs` - Yapı tipi (SingleUnit, Apartment, Site, Commercial)
  - `Enums/UnitType.cs` - Birim tipi (Block, Flat, Entrance, Zone, CommonArea)
  - `Enums/PredefinedZone.cs` - Önceden tanımlı fabrika bölgeleri

- **Yeni In-Memory Modeller:**
  - `Models/ProjectUnit.cs` - Oluşturulan birimler (Daire, Blok girişi, vb.)
  - `Models/StructureDefinition.cs` - Yapı tanımı (JSON serialize)
  - `Models/QuoteLineItem.cs` - Teklif kalemleri

- **ServiceProject Güncellemeleri:**
  - `StructureType` - Yapı tipi alanı
  - `StructureDefinitionJson` - Yapı tanımı (JSON)
  - `TotalUnitCount` - Toplam birim sayısı
  - `QuoteItemsJson` - Teklif kalemleri (JSON)
  - `DiscountPercent` - Proje iskontosu

- **Yeni ViewModel & View:**
  - `ViewModels/DiscoveryQuoteViewModel.cs` - 4 adımlı sihirbaz mantığı
  - `Views/DiscoveryQuoteWindow.xaml` - Yapı Sihirbazı UI

- **4 Adımlı Akış:**
  1. Proje & Müşteri Bilgileri
  2. Yapı Sihirbazı (Apartman/Site/Fabrika)
  3. Sistem Seçimi (Toplu ürün atama)
  4. Finansal Özet (İskonto, toplam)

---

## 2025-01-03

### ✅ Tamamlanan

#### Gelişmiş Servis Yaşam Döngüsü Mimarisi
- **Yeni Enum'lar:**
  - `Enums/WorkflowStatus.cs` - 9 farklı proje durumu (Draft → Completed)
  - `Enums/ServiceJobType.cs` - Fault (Arıza) / Project (Proje) ayrımı

- **ServiceJob Entity Güncellemeleri:**
  - `ServiceJobType` - Arıza vs Proje ayrımı
  - `WorkflowStatus` - 5 fazlı proje yaşam döngüsü
  - `IsStockReserved` / `IsStockDeducted` - Stok takibi
  - `ProposalSentDate`, `ApprovalDate`, `ProposalNotes` - Teklif alanları

- **Arıza & Servis Ekranı:**
  - `ViewModels/FaultTicketViewModel.cs` - Hibrit cihaz seçici mantığı
  - `Views/FaultTicketWindow.xaml` - Hızlı arıza kaydı formu
  - Mevcut cihaz veya yeni cihaz kaydı desteği
  - Maliyet tahmini bölümü

- **Proje & Kurulum Ekranı (5 Fazlı):**
  - `ViewModels/ProjectWorkflowViewModel.cs` - Keşif → Teklif → Onay → Uygulama → Final
  - `Views/ProjectWorkflowWindow.xaml` - Stepper UI ile faz navigasyonu
  - Stok rezervasyonu ve final düzeltme mantığı
  - `FinalAdjustmentItem` sınıfı - Tahmini vs Gerçek karşılaştırma

- **Navigasyon Entegrasyonu:**
  - `MainContentViewModel.cs` - `OpenFaultTicketCommand`, `OpenProjectWorkflowCommand` eklendi
  - `MainContentView.xaml` - "HIZLI ERİŞİM" bölümü ile butonlar eklendi

- **Bug Fixes:**
  - `IsExistingAsset` readonly property binding hatası düzeltildi (Mode=OneWay)
  - Komutlar yanlış ViewModel'e (`MainViewModel`) eklenmişti, `MainContentViewModel`'e taşındı

---

## 2025-12-26

### ✅ Tamamlanan

#### Kullanıcı Girişi ve Yetkilendirme (Login & RBAC)
- `Models/User.cs` entity modeli oluşturuldu (Ad, Soyad, Username, PasswordHash, Role)
- `Services/AuthService.cs` oluşturuldu (Login, Logout, SHA256 hashing)
- Varsayılan admin kullanıcısı: **admin.user / 1234**
- `Views/LoginView.xaml` modern kart tasarımı ile giriş ekranı
- `LoginViewModel.cs` ile MVVM pattern uygulandı
- `App.xaml.cs` başlangıç mantığı güncellendi (Login önce açılır)
- `AppDbContext.cs`'e `Users` DbSet eklendi

#### Kullanıcı Yönetimi
- `UsersViewModel.cs` ve `UsersView.xaml` oluşturuldu
- Kullanıcı listesi DataGrid (Ad Soyad, Username, Rol, Durum, Son Giriş)
- Arama/filtreleme özelliği
- Yeni kullanıcı ekleme (`AddUserView`)
- Kullanıcı silme (kendini silemez)
- Şifre sıfırlama (1234)
- **Rol Gösterimi:** Admin → "Patron", Technician → "Personel"

#### MainWindow Güncellemeleri
- Sol panele "👤 Kullanıcılar" butonu eklendi (Sadece Admin)
- Alt panele kullanıcı bilgisi kartı eklendi (Ad Soyad + Rol)
- "🚪 Çıkış Yap" butonu eklendi
- Rol tabanlı görünürlük (Admin bölümü sadece Patron'a görünür)

---


## 2025-12-25

### ✅ Tamamlanan

#### Dashboard Ana Sayfa Modülü
- `DashboardViewModel.cs` oluşturuldu (KPI sayaçları + veri koleksiyonları)
- `DashboardView.xaml` profesyonel grid layout ile tasarlandı
- 4 KPI Kartı: Aktif İşler, Kritik Stok, Bu Ay İşler, Toplam Müşteri
- Acil İşler DataGrid (Urgent/Critical priority)
- Son Stok Hareketleri listesi (Son 10)
- Kritik Stoklar uyarı listesi (Kırmızı)
- Yeni Müşteriler listesi (Son 5)
- MainWindow'a "🏠 Ana Sayfa" navigasyon butonu eklendi
- Uygulama açılışında Dashboard varsayılan sayfa olarak ayarlandı

#### PDF Raporlama Modülü
- QuestPDF kütüphanesi eklendi (v2025.12.0)
- `Services/PdfService.cs` oluşturuldu
- Profesyonel Servis Formu PDF tasarımı:
  - Header: Şirket bilgisi, İş ID, Tarih
  - Müşteri ve İş detayları
  - Kullanılan malzemeler tablosu
  - Maliyet özeti
  - İmza alanları (Teknisyen + Müşteri)
  - Garanti / sorumluluk notu
- `ServiceJobViewModel`'e `PrintServiceFormCommand` eklendi
- `ServiceJobsView.xaml`'e 🖨️ PDF Yazdır butonu eklendi
- SaveFileDialog ile kaydetme ve otomatik açma

---

## 2025-12-23

### ✅ Tamamlanan

#### ServiceJobsView Master List Tasarımı
- `ServiceJobsView.xaml` profesyonel DataGrid listesine dönüştürüldü
- Arama, tarih filtresi, durum filtresi eklendi
- Status badge'leri (renkli) ve priority ikonları eklendi
- Wizard UI ayrı pencereye (`NewServiceJobWindow.xaml`) taşındı

#### CustomerDetailView Tab Yapısı
- 4 tab eklendi: Genel Bilgiler, Aktif İşler, Servis Geçmişi, Finansal
- `ActiveJobs` ve `PastJobs` koleksiyonları eklendi
- Aktif işler (Status != Completed) ayrı tab'da gösteriliyor

#### Excel Import Stok Düzeltmesi
- `ProductViewModel.ImportFromExcel()` güncellendi
- Import sırasında `Inventory` ve `StockTransaction` kayıtları oluşturuluyor
- Ana Depo otomatik bulunuyor/oluşturuluyor

---

## Önceki Değişiklikler

### ✅ Tamamlanan
- WebView2 Map Fix (Async initialization)
- Product Excel Import (Auto-Inventory Creation)
- Add Product UI (Editable Unit + Initial Stock field)
- Financial Health Report White Screen Fix (Missing code-behind + Async Refactor)
- Purchase Order Manual Entry (Editable Product + Auto-Create Stock Card + Validation)
- UI Fix: Forced Button Visibility (#1A237E) & Manual Entry Panel Restoration
- Hotfix: Resolved 'Empty Suppliers Screen' by enforcing DataContext binding.
- Hotfix: Fixed 'White-on-White' buttons by adding BorderBrush to ModernButton.
- Navigation Buttons: Stock Count & Reports
- Customer Type: Individual/Corporate selection
- Dynamic Job Details: 8 category support

### 🔄 Planlanan
- Raporlama modülleri geliştirme
- Dashboard ekranı
- PDF export özelliği
