# KamatekCRM — Mimari ve Tasarım Dokümanı

**Son güncelleme:** 2026-08-05
**Kapsam:** WPF masaüstü, Application, Infrastructure, Shared, API, Web ve testler.

Bu doküman, projenin mevcut mimarisini, katman desenlerini, iş mantığını ve programın akışını
tek kaynaktan anlatır. Kod değişiklikleriyle birlikte güncel tutulmalıdır.

---

## 1. Genel Bakış

**Ürün:** Güvenlik sistemleri firmaları için tek operasyon merkezi — CRM, servis yönetimi
(keşif → teklif → montaj), saha operasyonu, stok, satın alma, POS/satış, finans, raporlama
ve teklif yönetimi aynı platformda birleştirilmiştir.

**Teknoloji:** .NET 9, WPF (MVVM), EF Core 9 + PostgreSQL (Npgsql), QuestPDF, WPF-UI,
CommunityToolkit.Mvvm, Serilog, Velopack (otomatik güncelleme), HTMX (web arayüzü), xUnit.

---

## 2. Çözüm Yapısı (`KamatekCRM.sln`)

| Proje | Rol | Hedef Çerçeve |
|---|---|---|
| `KamatekCrm.Shared` | Paylaşılan çekirdek: entity modelleri, enum'lar, DTO'lar, UI soyutlamaları (`IDialogService`, `IToastService`, `ILoadingService`), PDF servis arayüzleri, exception tipleri | net9.0 |
| `KamatekCrm.Application` | Uygulama katmanı: servis arayüzleri (CQRS), DTO'lar, saf iş kuralları (politikalar), yetki ve kişisel veri koruma servisleri | net9.0 |
| `KamatekCrm.Infrastructure` | EF Core (`AppDbContext`), migration'lar, komut/sorgu servis implementasyonları, audit, veritabanı başlatma | net9.0 |
| `KamatekCrm` (WPF) | Masaüstü UI: ViewModels, Views, `PdfService`, tema, navigasyon, otomatik güncelleme, arka plan servisleri | net9.0-windows |
| `KamatekCrm.API` | REST API (JWT + MediatR + SignalR), PostgreSQL'e doğrudan bağlanır | net9.0 |
| `KamatekCrm.Web` | HTMX + Minimal API web arayüzü (port 7000); API'ye (5050) HTTP proxy'ler | net9.0 |
| `KamatekCrm.Tests` | xUnit + FluentAssertions + Moq; servis/politika/integration/migration testleri | net9.0-windows |

**Bağımlılık zinciri:**
`Shared ← Application ← Infrastructure ← (WPF / API)`; `Web` yalnızca `Shared`'e bağlanır.
Uygulama (Application) asla Infrastructure'a, UI asla Infrastructure'a doğrudan bağımlı olmaz.

---

## 3. Katman Mimarisi ve Akış Desenleri

### 3.1 Hedef dilim (feature vertical slice)
```
Feature UI → Application use-case → Domain policy → Infrastructure (EF/adaptör)
```
- **WPF:** yalnızca görünüm, kullanıcı etkileşimi ve ekran durumu.
- **Application:** komut/sorgu, doğrulama, yetki ve transaction sınırının tanımı.
- **Domain/politika:** stok, fiyat, SLA, durum geçişi ve finans kuralları (saf C#, DB'den bağımsız).
- **Infrastructure:** EF, dosya, e-posta, yazıcı ve dış sistem adaptörleri.

### 3.2 Komut/Sorgu (CQRS-varyantı) ayrımı
Her modül iki sözleşme ile çalışır:

- **`I…ReadService`** (salt-okunur): `AsNoTracking` + **DTO projection**. UI'ya asla EF entity
  veya `DbContext` taşımaz; her girişte yetki kontrolü yapar.
- **`I…CommandService`** (yazma): tek **transaction sınırı**. Sıra:
  `yetki → doğrulama → idempotency kontrolü → ExecuteInTransactionAsync → audit kaydı → commit`.

Örnek (İş Emirleri):
- `IServiceJobReadService` — workspace (müşteri/ürün/teknisyen), arama, müşteri cihazları,
  malzemeler, geçmiş, dashboard KPI, belge verisi ve `GetWorkOrderWorkflowAsync` (keşif+teklif+montaj aggregate'i).
- `IServiceJobCommandService` — `SaveAsync`, `ChangeStatusAsync`, `ConvertToQuoteAsync`,
  `UpdateQuotationAsync`, `AcceptQuotationAsync`, `RejectQuotationAsync`,
  `PlanInstallationAsync`, `CompleteInstallationAsync`, `DeleteAsync`.

### 3.3 `Result<T>` deseni
Tüm servis çağrıları `Result<T>` / `Result` döner (`IsSuccess`, `Value`, `Error`).
İş kuralları ihlali exception fırlatmak yerine açık hata döner; ViewModel hata metnini toast ile
gösterir. UI'da iş mantığı exception'ı beklenmez.

### 3.4 Transaction ve Veri Bütünlüğü
- `IDbContextFactory<AppDbContext>` + **kısa ömürlü context** (her çağrıda yeni, izlenen varlık UI'a taşınmaz).
- `ExecuteInTransactionAsync` (Infrastructure/Data/DbContextTransactionExtensions.cs):
  PostgreSQL retrying execution strategy ile uyumlu; `BeginTransactionAsync(IsolationLevel.Serializable)`.
- **Idempotency anahtarları:** satış (POS), satın alma teslim alma, stok sayım oturumu,
  teklif kaydı/revizyonu, keşif → teklif dönüşümü. Aynı istek ikinci kez veri oluşturamaz.
- **Stok rezervasyonları:** iş emri/teklif kaydında rezerve edilir; tamamlanmada **tek sefer**
  depo ve ürün stoğundan düşülür, rezervasyon kapatılır; iptalde serbest bırakılır.
  Yetersiz stokta kısmi kayıt veya yarım rezervasyon oluşmaz.
- **Optimistic concurrency:** PostgreSQL `xmin` satır sürüm token'ı; `ResolveConcurrencyConflicts`
  çakışmada DB değerlerini taban alarak yeniden dener.
- **Soft delete:** `ISoftDeletable` global query filter; silme = `IsDeleted` + audit bilgisi.

### 3.5 Domain Politikaları (saf Application katmanı)
- `ServiceJobStatusPolicy` — iş emri durum geçiş matrisi (tek doğruluk kaynağı).
- `ProjectQuoteLifecyclePolicy` — proje teklifi yaşam döngüsü (Taslak/Revize → Gönderildi → Onaylandı | Reddedildi | Süresi Doldu).
- `ProjectQuotePricingPolicy`, `StandardQuotePricingPolicy` — fiyat/iskonto/KDV/marj UI ile servisin aynı sonucu kullanması.
- `FinancialTransactionPolicy` — finansal hareket kuralları.

---

## 4. Veri Katmanı (Infrastructure)

### 4.1 `AppDbContext`
- Tek DbContext (Infrastructure/Data/AppDbContext.cs), hibrit destek yok — **yalnızca Npgsql/PostgreSQL**.
- `SaveChanges` öncesi `PrepareChanges()`: tamamlanmış iade kayıtlarını korur, audit loglarını
  mühürler (`ActivityLogIntegrity.Seal`) ve audit bilgilerini (`CreatedDate/By`, `ModifiedDate/By`) uygular.
- Global soft-delete query filter + PostgreSQL `xmin` concurrency token.

### 4.2 Migration'lar
- **Ana set:** `KamatekCrm.Infrastructure/Migrations/` — Infrastructure assembly sahibidir.
  Zincir `20260409193923_InitialCreate` ile başlar; son eklenen:
  `20260804201621_AddWorkOrderWorkflowEntities` (keşif/teklif/montaj varlıkları).
- `MigrationOwnershipTests` zincir sırasını ve `HasPendingModelChanges == false` koşulunu test eder.
- **Bilinen risk:** `KamatekCrm.API/Migrations/` içinde eski/paralel bir set daha vardır (bkz. §10).

### 4.3 Seed
`DbSeeder` + `IDatabaseInitializationService.InitializeAsync()`: ilk kurulumda kriptografik
geçici parolalı `admin` hesabı üretir (düz metin parola yok); temel depo/kategori/brand seed'leri.

---

## 5. İş Emri İş Akışı (Keşif → Teklif → Montaj)

Yeni akışta her aşama **ayrı varlıkta** saklanır; aynı malzeme listesi aşamalar arasında
ortak kullanılmaz — **kopyalanır**:

```
ServiceJob (İş Emri)
 ├── DiscoveryReport (1:1)
 │     └── DiscoveryMaterial          → keşif: teknik not, fotoğraf, önerilen çözüm, tahmini malzeme (FİYATSIZ)
 ├── WorkOrderQuotation (1..n)
 │     └── QuotationItem             → fiyat teklifi: malzeme, miktar, birim fiyat, iskonto,
 │                                      KDV, işçilik, nakliye, açıklama, garanti, teslim, ödeme şartları
 └── InstallationOrder (1:1)
       ├── InstallationMaterial
       └── InstallationTask          → teknisyen, montaj tarihi, görevler, notlar, tamamlanma,
                                        teslim notu, müşteri imzası
```

**Varlıklar:** `KamatekCrm.Shared/Models/WorkOrders/WorkOrderWorkflowModels.cs`
**Enum:** `KamatekCrm.Shared/Enums/QuotationStatus.cs` → `Draft, Sent, Accepted, Rejected, Cancelled, Expired`

### 5.1 Durum Geçişleri (ServiceJobStatusPolicy)
```
DiscoveryRequest ─→ ConvertedToQuote ─→ InstallationPlanned ─→ InstallationCompleted
        │                   │                    │
        └──────────┬────────┴────────┬───────────┘
                 Cancelled (her aşamadan)
```
- Yalnızca bu akışa izin verilir; kısayol geçişler (ör. keşiften doğrudan montaj) reddedilir.
- **Montaj yalnızca `QuotationStatus.Accepted` teklif için planlanabilir** — hem
  `ChangeStatusAsync` hem `PlanInstallationAsync` tarafında zorunlu.

### 5.2 Keşif → Teklif (`ConvertToQuoteAsync`)
1. Keşif raporu oluşturulur/güncellenir: teknik notlar, önerilen çözüm, fotoğraflar,
   tahmini işçilik, `ServiceJobItems → DiscoveryMaterials` kopyalanır.
2. `WorkOrderQuotation` (Draft) oluşturulur; `DiscoveryMaterials → QuotationItems` kopyalanır.
3. İş emri durumu `ConvertedToQuote` yapılır; audit geçmişi yazılır.
4. **İkinci kez dönüşüm reddedilir** (idempotent), tüm adımlar tek transaction'dadır.

### 5.3 Teklif Yönetimi
- `UpdateQuotationAsync` — kalemler ve ticari şartlar düzenlenir; tutarlar servis tarafında
  yeniden hesaplanır (malzeme ara toplamı − iskonto + işçilik + nakliye → KDV → genel toplam).
- `AcceptQuotationAsync` / `RejectQuotationAsync` — durum + zaman damgası + ret gerekçesi;
  kabul edilmiş teklif reddedilemez.
- UI: `WorkOrderQuotationWindow` + `WorkOrderQuotationViewModel` (düzenlenebilir kalem ızgarası,
  tutar kartı, şartlar, Kabul/Reddet/PDF).

### 5.4 Montaj
- `PlanInstallationAsync` — kabul edilmiş teklif zorunlu; `QuotationItems → InstallationMaterials`
  kopyalanır, varsayılan görevler oluşturulur, teknisyen/montaj tarihi saklanır.
- `CompleteInstallationAsync` — stok tüketimi + tamamlanma tarihi + teknisyen + teslim notu +
  müşteri imzası (base64) montaj emrine yazılır; müşteri aktivitesi/geçmiş aynı akışta.

### 5.5 PDF Sistemi (aşamaya göre doğru belge)
- `IDiscoveryPdfService` — **Keşif Raporu** (fiyat içermez).
- `IQuotationPdfService` — **Fiyat Teklifi** (kalemler, iskonto, KDV, işçilik, nakliye, şartlar).
- `IInstallationPdfService` — **Montaj İş Emri** ve **Montaj Tamamlama Formu** (imza gömülür).
- Tümü tek `PdfService` (QuestPDF) tarafından uygulanır; KVKK maskeleme (`Protect`) + audit erişim kaydı yapar.
- **Varsayılan olarak Keşif PDF'i üretilmez:** `PrintServiceForm` (ServiceJobViewModel) iş emrinin
  durumuna göre doğru belgeyi üretir. Eski `GenerateServiceJobPdf(ServiceJob)` keşif dışı durumlarda
  `NotSupportedException` fırlatır; FaultTicket akışı kendi servis formunu kullanır.

---

## 6. WPF Masaüstü (UI Katmanı)

### 6.1 Başlangıç (composition root — `App.xaml.cs`)
1. Velopack bootstrap → tr-TR kültür → Npgsql legacy timestamp → Serilog.
2. `Host.CreateDefaultBuilder()` + `ServiceCollectionExtensions.AddApplicationServices(configuration)`
   (Infrastructure + Application + ViewModels + Windows + arka plan servisleri).
3. `IDatabaseInitializationService.InitializeAsync()` (ilk kurulum / geçici admin parolası).
4. `NavigationService.NavigateToLogin()` → ana pencere; 5 sn gecikmeli güncelleme kontrolü.

### 6.2 DI Kayıtları
- **Infrastructure** (`DependencyInjection.cs`): `AddDbContextFactory<AppDbContext>` (Npgsql + retry),
  tüm `I…CommandService`/`I…ReadService` (Transient), `IAuditTrailService`, `IExceptionClassifier`.
- **Application** (`DependencyInjection.cs`): `I…AppService` (Scoped), politikalar ve yetki/kişisel veri
  servisleri (Singleton).
- **WPF** (`ServiceCollectionExtensions.cs`): PDF servisleri (tek `PdfService` çoklu arayüz),
  `NavigationService`, `IAuthService`, `IToastService`, `ILoadingService`, ViewModels, Windows,
  `IHostedService`'ler (`NetworkDiscoveryService`, `ConnectionHeartbeatService`).

### 6.3 MVVM
- CommunityToolkit.Mvvm (`[RelayCommand]`, `SetProperty`); temel sınıf `ViewModelBase`.
- Dev ViewModel'ler: `ServiceJobViewModel` (~2500 satır), `ProjectQuoteEditorViewModel`,
  `StockCountViewModel`, `DirectSalesViewModel`, `RepairViewModel`.
- UI soyutlamaları: `IDialogService` (WpfDialogService), `IToastService`, `ILoadingService` —
  ViewModel'ler MessageBox/Win32 diyaloglarından arındırılmıştır (kalan modüller kademeli temizleniyor).

### 6.4 Navigasyon ve Shell
- `MainWindow` + `MainContentView` (kenar menü) + `NavigationService` (ViewModel↔View eşleme).
- ViewModel-View eşlemesi `App.xaml` DataTemplate'lerinde; login → içerik akışı.

### 6.5 Tema ve Bileşenler
- WPF-UI + `Resources/DesignTokens.xaml` / `UxFoundation.xaml` / tema dosyaları (MidnightDark, PremiumLight…).
- Yeniden kullanılabilir `Km*` bileşenleri: KPI kartı, timeline, wizard stepper, filtre paneli,
  arama kutusu, boş durum, rozetler.
- `Converters/` klasöründe çok sayıda değer dönüştürücü.

### 6.6 Global Hata Yönetimi
`App.xaml.cs` içinde üç handler: `DispatcherUnhandledException`, `AppDomain.UnhandledException`,
`TaskScheduler.UnobservedTaskException` — çökme yerine log + toast (Dispatcher güvenli).

---

## 7. API (KamatekCrm.API)

- **Auth:** JWT Bearer; kullanıcı/parola doğrulama; parola politikası + geçici parola değiştirme.
- **Endpoint'ler:** ~18 controller (Auth, Customers, Products, Inventory, Sales, Finance,
  ServiceJobs, Reports, Export, Photo, Pdf, Users, Suppliers, Tasks, Location…).
- **Teknisyen görevleri:** MediatR (CQRS) + SignalR (`NotificationHub`) — API'deki tek MediatR kullanımı.
- **Altyapı:** Serilog, `GlobalExceptionMiddleware`, `RateLimitingConfiguration`, `ActionFilters`,
  Npgsql + retry; port 5050 (`appsettings.json` → `ConnectionStrings:PostgreSQL`).
- **Bilinen risk:** API kendi migration setini taşır (bkz. §10).

---

## 8. Web (KamatekCrm.Web)

- **HTMX + Minimal API**, cookie auth + antiforgery; port 7000 (Program.cs'te `UseUrls` sabit).
- `ApiClient` (IHttpClientFactory) → `http://localhost:5050/`.
- Features: Auth, Dashboard, Customers, Products, Jobs, Sales, Technician, Installations,
  Quotes, Repairs, Route, Location — server-rendered HTML partial'ları.
- `wwwroot`: `site.css`, `htmx-config.js`, PWA manifest + service worker.
- `index.html` (proje kökünde): bağımsız landing sayfası (statik HTML — Preview'da izlenebilir).

---

## 9. Güvenlik, KVKK ve Denetim

- **Yetki:** `ApplicationPermission` enum + `ApplicationAuthorizationService` +
  `ICurrentUserContext` (`DesktopCurrentUserContext`); servis girişlerinde zorunlu.
- **KVKK:** `PersonalDataProtectionService` — telefon/e-posta/adres/TCKN/vergi no yetkisiz
  kullanıcıya maskelenir (fail-closed); arama/ekran/PDF/termal fiş aynı politikayı kullanır.
- **Audit:** `AuditTrailService` — kültürden bağımsız SHA-256 bütünlük mührü
  (`ActivityLogIntegrity`), append-only (uygulama reddi + PostgreSQL trigger),
  `SystemLogsView`'de doğrulama özeti.
- **Yedek:** `IBackupService` — PostgreSQL custom `.backup`, sürümlü manifest + SHA-256,
  geri yükleme öncesi `pg_restore --list` provası, otomatik kurtarma noktası.

---

## 10. Testler ve Kalite Kapıları

- **Servis testleri** (`KamatekCrm.Tests/Services/`): InMemory/SQLite DB + `Mock<IDbContextFactory>`
  ile yetki, idempotency, rollback, politika geçişleri, stok bütünlüğü, audit mühürü, KVKK maskeleme.
- **İş akışı testleri** (`WorkOrderWorkflowTests.cs`): dönüşümde veri kopyalama, çift teklif reddi,
  kabul edilmiş teklif zorunluluğu, montaj planlama/tamamlama, tutar hesapları, aggregate okuma.
- **Migration testleri:** `MigrationOwnershipTests` (zincir + model snapshot uyumu).
- **Kalite kapıları:** 0 hata / 0 yeni uyarı; kritik use-case'lerde başarı + doğrulama + yetki + rollback testi.

---

## 11. Bilinen Riskler ve Yol Haritası

**Riskler / açık işler:**
1. Dev ViewModel'ler (`ServiceJobViewModel`, `StockCountViewModel`, `ProjectQuoteEditorViewModel`,
   `DirectSalesViewModel`) ve `PdfService` — feature bileşenlerine bölünmeli.
2. `KamatekCrm.API/Migrations` ile `KamatekCrm.Infrastructure/Migrations` **çift migration izi** —
   tek assembly (Infrastructure) hedef; üretim veritabanı geçmişiyle karşılaştırılarak tekleştirilmeli.
3. Kalan ViewModel'lerde `MessageBox` / `App.ServiceProvider` / uzun ömürlü DbContext kullanımı —
   `IDialogService` + Application servislerine taşınmalı.
4. Event aboneliklerinde yaşam döngüsü yönetimi (açık unsubscribe / weak event) genişletilmeli.
5. "Acil taslak" yalnızca ekran adı+zaman tutuyor; gerçek form verisi kurtarılamıyor.

**Ürün akışı boşlukları (Faz 3–4):** tekliften tahsilata uçtan uca timeline, bakım sözleşmeleri
ve otomasyonu, RMA/garanti, onay merkezi, operasyon radarı, teklif zekâsı, dijital ikiz,
kârlılık otopsisi, veri kalite asistanı.

---
*Bu doküman, kodla birlikte güncel tutulmalıdır; mimari kararların tek doğruluk kaynağı olması hedeflenir.*
