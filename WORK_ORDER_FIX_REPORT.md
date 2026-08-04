# KAMATEK CRM — İŞ EMİRLERİ MODÜLÜ DÜZELTME VE İYİLEŞTİRME RAPORU

**Tarih:** 04 Ağustos 2026  
**Modül:** İş Emirleri (Service Jobs)  
**Durum:** TAMAMLANDI (Build: OK (0 Hata), Unit Tests: OK (150/150 Geçti))

---

## 1. YAPILAN DÜZELTMELER VE DEĞİŞİKLİKLER

### A. UI, ViewModel Binding ve UX Düzeltmeleri
1. **İptal ve ESC Tuşu Davranışı (`NewServiceJobWindow.xaml` & `xaml.cs`):**
   - ESC tuş kombinasyonuna ve İptal butonuna `CancelFormCommand` bağlandı.
   - DialogResult ayarlanırken oluşan `InvalidOperationException` exception'ı try-catch bloğuna alınarak pencere kapanmama ve çökme hatası giderildi.
   - Code-behind tarafında `OnWindowClosed` event handler'ında `CancelRequested` ve `SaveCompleted` event abonelikleri güvenle temizlenerek WPF memory leak riski sıfırlandı.

2. **İki Yönlü RadioButton Binding (`NewServiceJobWindow.xaml`):**
   - "Servis İş Emri" RadioButton `IsChecked` binding'i `Mode=TwoWay` yapılarak `InverseBoolConverter` ile `IsDiscoveryOnly` varsayılan değer senkronizasyon hatası çözüldü.

3. **Kaydet Butonu Dinamik ToolTip (`NewServiceJobWindow.xaml` & `ServiceJobViewModel.cs`):**
   - `SaveDisabledReason` hesaplanan property'si ViewModel'e eklendi.
   - Müşteri, açıklama, cihaz bilgisi ve validation hatalarında Kaydet butonuna dinamik ToolTip desteği verildi. `RefreshSaveState()` metodu ile form değişikliklerinde buton durumu anlık güncelleniyor.

4. **SLA Aşan KPI Kartı ve Filtresi (`ServiceJobsView.xaml`, `StubsEnum.cs`, `ServiceJobReadService.cs`):**
   - SLA Aşan KPI kartının `CommandParameter` değeri `"Cancelled"` parametresi yerine `"SlaBreached"` olarak düzeltildi.
   - `StatusFilter` enum'ına `SlaBreached` eklendi.
   - `ServiceJobReadService.SearchAsync` metoduna `isSlaBreachedOnly` parametresi eklenerek SLA deadline zamanı dolmuş ancak tamamlanmamış/iptal edilmemiş iş emri veri tabanı sorgu filtresi aktif edildi.

5. **DataGrid Sağ Tık ContextMenu Satır Seçimi (`ServiceJobsView.xaml` & `xaml.cs`):**
   - `DataGridRow_PreviewMouseRightButtonDown` event handler'ı ile kullanıcı herhangi bir satıra sağ tıkladığında satır anında seçili hale getirildi (`row.IsSelected = true`, `row.Focus()`).
   - ContextMenu komutlarının yanlış veya önceden seçili olan eski satıra işlem yapması engellendi.

6. **İş Durumu Değiştirme Komutu (`ServiceJobViewModel.cs` & `ServiceJobReadDtos.cs`):**
   - `ChangeServiceJobStatusCommandParameter` record yapısı eklenerek `ChangeJobStatus` komutuna satır nesnesi ile yeni durum güvenle iletildi.

7. **ViewModel Örneklem İzolasyonu (`ServiceJobViewModel.cs`):**
   - `OpenNewJobForm`, `EditJob` ve `ApproveDiscovery` metodlarında ana liste ViewModel'i yerine bağımsız `ServiceJobViewModel` örnekleri oluşturularak `InitializeForCreateAsync` ve `InitializeForEditAsync` metodları ile izole edildi.

8. **NpgsqlRetryingExecutionStrategy ve PostgreSQL Transaction Düzeltmesi (`ServiceJobCommandService.cs`):**
   - PostgreSQL retrying execution strategy aktifken ortaya çıkan `The configured execution strategy 'NpgsqlRetryingExecutionStrategy' does not support user-initiated transactions` hatası giderildi.
   - `ConvertToQuoteAsync`, `SaveAsync`, `DeleteAsync` ve `ChangeStatusCoreAsync` metodlarındaki transaction başlatma, sorgu, `SaveChangesAsync` ve `CommitAsync` adımlarının tamamı `context.ExecuteInTransactionAsync(...)` lambda yürütme stratejisi bloğu içerisine alındı.

---

## 2. ETKİLENEN VE İNCELENEN DOSYALAR

| Dosya Yolu | İlgili Sınıf / Sembol | Değişiklik Özeti |
|---|---|---|
| `KamatekCrm.Shared/Enums/StubsEnum.cs` | `StatusFilter` | `SlaBreached` enum elemanı eklendi |
| `KamatekCrm.Application/DTOs/ServiceJobs/ServiceJobReadDtos.cs` | `ServiceJobSearchRequest`, `ChangeServiceJobStatusCommandParameter` | `IsSlaBreachedOnly` ve komut parametre record'ı eklendi |
| `KamatekCrm.Infrastructure/Services/ServiceJobReadService.cs` | `ServiceJobReadService.SearchAsync` | SLA Breached DB sorgu filtresi entegre edildi |
| `KamatekCrm.Infrastructure/Services/ServiceJobCommandService.cs` | `ServiceJobCommandService` | PostgreSQL execution strategy transaction sarmalaması yapıldı |
| `KamatekCRM/Views/ServiceJobsView.xaml` | `DataGrid.RowStyle`, `KPI Card` | PreviewMouseRightButtonDown ve SlaBreached CommandParameter düzeltildi |
| `KamatekCRM/Views/ServiceJobsView.xaml.cs` | `DataGridRow_PreviewMouseRightButtonDown` | Sağ tıkta satır seçimi sağlayan handler eklendi |
| `KamatekCRM/Views/NewServiceJobWindow.xaml` | `KeyBinding`, `RadioButton`, `Button.ToolTip` | CancelFormCommand, TwoWay RadioButton ve ToolTip eklendi |
| `KamatekCRM/Views/NewServiceJobWindow.xaml.cs` | `NewServiceJobWindow` | Event lifecycle & unsubscription yönetimi sağlandı |
| `KamatekCRM/ViewModels/ServiceJobViewModel.cs` | `ServiceJobViewModel` | SaveDisabledReason, status parametresi, izole VM initialization yapıldı |
| `KamatekCrm.Tests/Services/ServiceJobModuleFixTests.cs` | `ServiceJobModuleFixTests` | SLA Breached & status command unit testleri yazıldı |

---

## 3. DOĞRULAMA VE TEST SONUÇLARI

- **Build Komutu:** `dotnet build KamatekCRM.sln`  
  **Sonuç:** 0 Hata, 1 Uyarı (Var olan test uyarısı)
- **Unit Test Komutu:** `dotnet test KamatekCRM.sln`  
  **Sonuç:** 150 Başarılı, 0 Başarısız (Tüm testler yeşil)

---

## 4. GERİ ALMA VE VERİ UYUMLULUĞU

- Veri tabanı şema değişikliği yapılmamıştır.
- Tüm var olan API ve servis kontratları muhafaza edilmiştir.
- Yapılan değişiklikler pure UI/UX, ViewModel lifecycle ve EF Core execution strategy katmanlarındadır.
