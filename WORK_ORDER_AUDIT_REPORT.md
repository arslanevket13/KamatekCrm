# KAMATEK CRM — İŞ EMİRLERİ MODÜLÜ MEVCUT DURUM VE DENETİM RAPORU (WORK_ORDER_AUDIT_REPORT.md)

**Tarih:** 04.08.2026  
**Modül:** İş Emirleri (Service Jobs / Work Orders)  
**Kapsam:** WPF Presentation (Views/ViewModels), Application Services, Infrastructure/Persistence, Domain & Shared DTOs.

---

## 1. MODÜLE AİT GERÇEK DOSYA HARİTASI

### Presentation / WPF (Desktop UI)
- `c:\Antigravity\KamatekCRM\Views\ServiceJobsView.xaml` — İş Emirleri ana liste ekranı, KPI kartları, filtre çubuğu, DataGrid, ContextMenu ve detay paneli.
- `c:\Antigravity\KamatekCRM\Views\ServiceJobsView.xaml.cs` — Code-behind.
- `c:\Antigravity\KamatekCRM\Views\NewServiceJobWindow.xaml` — Yeni İş Emri ve Keşif Talebi oluşturma/düzenleme modal penceresi.
- `c:\Antigravity\KamatekCRM\Views\NewServiceJobWindow.xaml.cs` — Code-behind (Event abonelikleri ve DialogResult yönetimi).
- `c:\Antigravity\KamatekCRM\ViewModels\ServiceJobViewModel.cs` — 2236 satırlık ana ViewModel (Liste, Form, Dashboard, Wizard ve İşlemler).

### Application / Core Interface & Services
- `c:\Antigravity\KamatekCrm.Application\Interfaces\IServiceJobCommandService.cs` — İş Emri yazma/güncelleme/tamamlama/silme/teklif sözleşmesi.
- `c:\Antigravity\KamatekCrm.Application\Interfaces\IServiceJobReadService.cs` — Salt-okunur sorgulama, arama, dashboard ve detay projection sözleşmesi.
- `c:\Antigravity\KamatekCrm.Application\Interfaces\IServiceJobStatusPolicy.cs` — Durum geçiş kuralları arayüzü.
- `c:\Antigravity\KamatekCrm.Application\Services\ServiceJobStatusPolicy.cs` — İş Emri durum geçiş matrisi implementasyonu.

### Infrastructure / Persistence & Services
- `c:\Antigravity\KamatekCrm.Infrastructure\Services\ServiceJobCommandService.cs` — Transaction, stok rezervasyonu, müşteri/cihaz hibrit kaydı ve tamamlama mantığı.
- `c:\Antigravity\KamatekCrm.Infrastructure\Services\ServiceJobReadService.cs` — AsNoTracking optimized EF Core sorguları ve dashboard istatistikleri.
- `c:\Antigravity\KamatekCrm.Infrastructure\Data\AppDbContext.cs` — `DbSet<ServiceJob>`, `DbSet<ServiceJobItem>`, `DbSet<ServiceJobHistory>`, `DbSet<StockReservation>`.

### Domain / Shared DTO & Enums
- `c:\Antigravity\KamatekCrm.Shared\Models\ServiceJob.cs` — Entity modeli.
- `c:\Antigravity\KamatekCrm.Shared\Models\ServiceJobItem.cs` — İş malzemeleri entity modeli.
- `c:\Antigravity\KamatekCrm.Shared\Models\ServiceJobHistory.cs` — İş tarihçesi ve audit entity modeli.
- `c:\Antigravity\KamatekCrm.Shared\DTOs\JobDtos.cs` — DataGrid ve form lookup DTO'ları.
- `c:\Antigravity\KamatekCrm.Shared\Enums\StubsEnum.cs` — `JobStatus`, `JobPriority`, `WorkOrderType`, `StructureType`, `DeviceType`.

---

## 2. KONTROL, COMMAND VE SERVİS DENETİM TABLOSU

| Ekran | Kontrol | Command | CanExecute | Servis | Mevcut Durum | Sorun | Risk |
|---|---|---|---|---|---|---|---|
| ServiceJobsView | "Yeni İş Emri" Butonu | `OpenNewJobFormCommand` | Yok (Always True) | `OpenNewJobForm()` | Yeni `ServiceJobViewModel` oluşturur ve listeleri manuel kopyalar. | Liste verilerini `foreach` ile elle aktarır, DI container yaşam döngüsünü pas geçer. | Orta |
| ServiceJobsView | "Yenile" Butonu | `RefreshListCommand` | Yok (Always True) | `LoadServiceJobs()` | Sadece iş listesini yeniler. | KPI kart sayıları (`LoadDashboardAsync`) yenilenmez, dashboard eski kalır. | Orta |
| ServiceJobsView | KPI Card: Toplam İş | `SelectStatusFilterCommand` | Yok | `SelectStatusFilter("All")` | Filtreyi "All" yapar. | Düzgün çalışıyor. | Düşük |
| ServiceJobsView | KPI Card: Bekleyen | `SelectStatusFilterCommand` | Yok | `SelectStatusFilter("Pending")` | Filtreyi "Pending" yapar. | Düzgün çalışıyor. | Düşük |
| ServiceJobsView | KPI Card: Devam Eden | `SelectStatusFilterCommand` | Yok | `SelectStatusFilter("InProgress")` | Filtreyi "InProgress" yapar. | Düzgün çalışıyor. | Düşük |
| ServiceJobsView | KPI Card: Tamamlanan | `SelectStatusFilterCommand` | Yok | `SelectStatusFilter("Completed")` | Filtreyi "Completed" yapar. | Düzgün çalışıyor. | Düşük |
| ServiceJobsView | KPI Card: SLA Aşan | `SelectStatusFilterCommand` | Yok | `SelectStatusFilter("Cancelled")` | **KRİTİK HATA:** `CommandParameter="Cancelled"` | SLA Aşan kartına tıklandığında SLA aşan işler yerine İptal Edilen işler filtrelenir! | **YÜKSEK** |
| ServiceJobsView | DataGrid ContextMenu: Detay Göster | `ViewJobDetailCommand` | Yok | `ViewJobDetail(job)` | `{Binding SelectedServiceJob}` parametresi ile çalışır. | Sağ tıklanan satır DataGrid'de seçili değilse, önceden seçili olan başka iş emri açılır! | **YÜKSEK** |
| ServiceJobsView | DataGrid ContextMenu: İşi Düzenle | `EditJobCommand` | Yok | `EditJob(job)` | `{Binding SelectedServiceJob}` parametresi ile çalışır. | Sağ tıklanan satır seçili değilse yanlış iş düzenlenir. Ayrıca `this` (Liste VM) DataContext olarak pencereye verilir. | **YÜKSEK** |
| ServiceJobsView | DataGrid ContextMenu: Teklife Dönüştür | `ConvertToQuoteCommand` | Yok | `IServiceJobCommandService.ConvertToQuoteAsync` | `{Binding SelectedServiceJob}` parametresi ile çalışır. | Sağ tıklanan satır seçili değilse yanlış iş teklife dönüştürülür. | **YÜKSEK** |
| ServiceJobsView | DataGrid ContextMenu: Bekliyor Yap | `ChangeJobStatusCommand` | `CanChangeJobStatus` | `IServiceJobCommandService.ChangeStatusAsync` | `CommandParameter="Pending"` (Sadece string durum gider, job gitmez). | Komut hedef iş emrini parametre almaz, global `SelectedServiceJob` kullanır. Yanlış kayıt güncellenir! | **YÜKSEK** |
| ServiceJobsView | DataGrid ContextMenu: Devam Ediyor Yap | `ChangeJobStatusCommand` | `CanChangeJobStatus` | `IServiceJobCommandService.ChangeStatusAsync` | `CommandParameter="InProgress"` | Komut hedef iş emrini parametre almaz, global `SelectedServiceJob` kullanır. Yanlış kayıt güncellenir! | **YÜKSEK** |
| ServiceJobsView | DataGrid ContextMenu: Tamamla | `ChangeJobStatusCommand` | `CanChangeJobStatus` | `IServiceJobCommandService.ChangeStatusAsync` | `CommandParameter="Completed"` | Sağ tık "Tamamla", detay panelindeki `CompleteJobCommand` ile tutarsızdır. Hedef satır gitmez. | **YÜKSEK** |
| ServiceJobsView | DataGrid ContextMenu: İptal Et | `ChangeJobStatusCommand` | `CanChangeJobStatus` | `IServiceJobCommandService.ChangeStatusAsync` | `CommandParameter="Cancelled"` | Komut hedef iş emrini parametre almaz, global `SelectedServiceJob` kullanır. Yanlış kayıt güncellenir! | **YÜKSEK** |
| ServiceJobsView | DataGrid ContextMenu: PDF Yazdır | `PrintServiceFormCommand` | Yok | `PdfService.GenerateServiceJobPdf` | `{Binding SelectedServiceJob}` | Sağ tıklanan satır seçili değilse yanlış iş için PDF üretilir. | **YÜKSEK** |
| ServiceJobsView | DataGrid ContextMenu: Sil | `DeleteJobCommand` | Yok | `IServiceJobCommandService.DeleteAsync` | `{Binding SelectedServiceJob}` | Sağ tıklanan satır seçili değilse yanlış iş silinir! | **YÜKSEK** |
| ServiceJobsView | DataGrid Row Button: "Aç" | `ViewJobDetailCommand` | Yok | `ViewJobDetail(job)` | `{Binding}` (Satır DTO'su parametre gider). | Doğru çalışıyor (Satır DTO'su doğru iletiliyor). | Düşük |
| ServiceJobsView | Sağ Panel: "Detay / Tamamla" | `CompleteJobCommand` | `Status == InProgress` | `IServiceJobCommandService.ChangeStatusAsync(Completed)` | Sadece InProgress işlerde aktiftir. | Buton pasif olduğunda kullanıcı nedeni göremiyor (Tooltip yok). | Orta |
| NewServiceJobWindow | ESC Tuşu | `CancelCommand` | Yok | `Cancel(Window? window = null)` | Parameter null gider. | **KRİTİK HATA:** `window` parameter null olduğu için ESC tuşu pencereyi KAPATMAZ! | **YÜKSEK** |
| NewServiceJobWindow | "İptal Et (Esc)" Butonu | `CancelCommand` | Yok | `Cancel(Window? window = null)` | Parameter null gider. | **KRİTİK HATA:** `CommandParameter` verilmediği için `window` null olur ve buton HİÇBİR ŞEY YAPMAZ! | **YÜKSEK** |
| NewServiceJobWindow | İşlem Türü Radio: Servis İş Emri | RadioButton | N/A | `SelectedWorkOrderType` | `IsChecked="{Binding IsDiscoveryOnly, Converter={StaticResource InverseBoolConverter}, Mode=OneWay}"` | **KRİTİK HATA:** `Mode=OneWay` olduğu için Servis İş Emri radio button'una tıklanınca ViewModel GÜNCELLENMEZ! | **YÜKSEK** |
| NewServiceJobWindow | Mevcut Müşteri RadioButton | RadioButton | N/A | `IsQuickAddCustomer` | `IsChecked="True"` XAML'de sabitlenmiş. | RadioButton XAML'de `IsChecked="True"` sabit olduğu için ViewModel state'i ile çelişebilir. | Orta |
| NewServiceJobWindow | "İŞ EMRİNİ KAYDET (Ctrl+S)" Butonu | `SaveServiceJobCommand` | `CanSaveServiceJob()` | `IServiceJobCommandService.SaveAsync` | Form doğrulama, müşteri, açıklama vb. kontrol eder. | Buton pasif olduğunda kullanıcı neden pasif olduğunu göremiyor (SaveDisabledReason / ToolTip yok). | Yüksek |

---

## 3. MEVCUT BUILD VE TEST TABAN ÇİZGİSİ

- **dotnet build KamatekCRM.sln:**  
  - Hata Sayısı: **0**  
  - Uyarı Sayısı: **0**  
- **dotnet test KamatekCRM.sln:**  
  - Toplam Test: **147**  
  - Başarılı: **147**  
  - Başarısız: **0**  
- **XAML Binding Hataları (Tespit Edilen):**  
  1. `NewServiceJobWindow.xaml`: `CancelCommand` parameter eksikliği nedeniyle pencere kapanmama hatası.  
  2. `NewServiceJobWindow.xaml`: `InverseBoolConverter` OneWay binding nedeniyle RadioButton seçiminin ViewModel'e aktarılamaması.  
  3. `ServiceJobsView.xaml`: SLA Breached KPI kartında `CommandParameter="Cancelled"` semantik hatası.  
  4. `ServiceJobsView.xaml`: DataGrid ContextMenu DataContext kopukluğu ve `SelectedServiceJob` üzerinden yanlış satıra işlem yapılması.

---

## 4. DÜZELTME PLANLARI VE ADIMLARI

1. **Aşama 2 (Kritik UI Hataları):**
   - Single Cancel Event & Command Pattern (`CancelFormCommand` & Code-behind subscription cleanup).
   - DataGrid Row PreviewMouseRightButtonDown & ContextMenu DataContext / Parameter düzeltmeleri.
   - Typed status parameter model (`ChangeServiceJobStatusCommandParameter`).
   - SLA Breached KPI filter fix (`StatusFilter.SlaBreached`).
   - `SaveDisabledReason` property & ToolTip integration.

2. **Aşama 3 (Kayıt ve Form Akışı):**
   - WorkOrderType radio button `Mode=TwoWay` veya enum-based RadioButton converter fix.
   - Editor ViewModel separation (`ServiceJobEditorViewModel` & `ServiceJobListViewModel` refactor).
   - Dynamic validation cleanup for Quick Customer & New Asset modes.

3. **Aşama 4 (İşlemler, Stok ve Servis Katmanı):**
   - Single source of truth status transition policy enforcement.
   - Audit history & stock reservation rollback validation.
   - Comprehensive unit & integration tests.
