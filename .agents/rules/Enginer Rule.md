---
trigger: always_on
description: KamatekCRM mandatory engineering, architecture, PostgreSQL/EF Core safety, MVVM, WPF 3-theme design system, and code hygiene rules.
---

# KAMATEK CRM - ZORUNLU MÜHENDİSLİK, KALİTE VE MİMARİ KURALLARI

## 0. İLETİŞİM VE DİL KURALLARI
- Kullanıcıya her zaman **Türkçe**, öz ve net yanıt ver (aksi istenmedikçe en fazla 10 satır).
- Tüm UI metinleri, bildirimler ve doğrulama mesajları hatasız, temiz **Türkçe** olmalıdır.

## 1. KEŞİF VE TEKRAR KULLANIM (REUSE BEFORE CREATE)
- Yeni bir sınıf, enum, converter veya yardımcı metot oluşturmadan ÖNCE mutlaka `KamatekCrm.Shared`, `KamatekCrm.Application` ve `KamatekCRM/Converters` dizinlerinde arama yap.
- Sonuç dönüşlerinde YALNIZCA `KamatekCrm.ApplicationCore` içindeki mevcut `Result<T>` sınıfını kullan; yeni `Result` sınıfı tanımlama.
- Parasal yuvarlamalarda kendi `Math.Round` metodunu yazma; YALNIZCA `FinancialCalculator.RoundMoney` ve `FinancialCalculator.CalculateMultiLine` kullan.
- Şifre doğrulama ve üretmede YALNIZCA merkezi `BCrypt` tabanlı servisi kullan; `PBKDF2` veya düz metin karşılaştırması yazma.

## 2. MİMARİ VE BAĞIMLILIK KURALLARI (CLEAN ARCHITECTURE)
- WPF `ViewModel` sınıfları ve `KamatekCrm.API` Controller'ları ASLA doğrudan `AppDbContext`, `IDbContextFactory<AppDbContext>` veya somut `UnitOfWork` sınıfını kullanamaz. Tüm veri erişimi `KamatekCrm.Application` katmanındaki Command/Read servisleri üzerinden yapılmalıdır.
- `((UnitOfWork)_unitOfWork)` gibi arayüzden somut sınıfa downcast yapılması ve `App.ServiceProvider` (Service Locator) kullanımı KESİNLİKLE YASAKTIR.
- ViewModel içinde `MessageBox.Show`, `new ...Window().ShowDialog()` veya `OpenFileDialog` doğrudan çağrılamaz; `IDialogService` / `IToastService` / `IUIService` kullanılmalıdır.
- WPF masaüstü projesinde (`KamatekCRM`) kısa ömürlü servisleri `AddScoped` olarak kaydetme; `AddTransient` kullan.
- Bir sınıf 500 satırı aşıyorsa yeni metot eklemeden önce sınıfı alt sorumluluklara böl.

## 3. POSTGRESQL, EF CORE VE VERİ GÜVENLİĞİ (ZERO P0 CRASHES)
- **Idempotent Migration'lar:** Şema onarımı veya evrimi sırasında mevcut yerel veritabanlarıyla çakışabilecek çıplak `AddColumn` / `CreateIndex` / `CreateTable` üretme; çakışma ihtimali olan yerlerde PostgreSQL `IF NOT EXISTS` SQL koruması kullan.
- **Entity Oluşturmadan Önce Snapshot Kontrolü:** `new User`, `new CustomerInteraction` vb. yazmadan önce `AppDbContextModelSnapshot.cs` içindeki TÜM `nullable: false` / `NOT NULL` kolonları doğrula ve güvenli varsayılan değerler ata.
- **Yalnızca UTC:** PostgreSQL `timestamptz` alanları için her zaman `DateTime.UtcNow` (`DateTimeKind.Utc`) kullan.
- **Güvenli Yerel Postgres Kurulumu:** Parola dosyalarını BOM ve satır sonu olmadan saf ASCII (`Encoding.ASCII`) ile yaz. Sahipsiz `pgdata` dizinlerini yalnızca `IsFirstRunCompleted == false` iken işle.
- **Ham SQL Hatasını UI'a Sızdırma:** `PostgresException` / `DbUpdateException` hatalarını yakala, `GetBaseException()` + `SqlState` detaylarını eksiksiz olarak `Serilog.Log.Error` ile logla (`InnerException` asla yutulmamalı) ve kullanıcıya `IToastService` üzerinden temiz bir Türkçe mesaj göster.
- **Transaction ve Concurrency:** Doğrudan `context.Database.BeginTransactionAsync()` çağırmak YASAKTIR (`NpgsqlRetryingExecutionStrategy` ile çöker); çok adımlı DB yazımlarında her zaman `await context.ExecuteInTransactionAsync(async tx => { ... })` kullan. `SaveChanges` içinde `DbUpdateConcurrencyException` yakalayıp `OriginalValues.SetValues` ile ezmek ve kısmi entity üzerinde `CurrentValues.SetValues(...)` çağırmak YASAKTIR.
- **Async ve Sorgu Disiplini:** Aynı `DbContext` üzerinde `Task.WhenAll` çalıştırmak, `.GetAwaiter().GetResult()` / `.Result` / `.Wait()` kullanmak ve tabloları `.ToListAsync()` ile belleğe çekip C# içinde `.Sum()` / `.Count()` / `.Skip()` yapmak YASAKTIR; tüm filtreleme ve toplamaları SQL (`IQueryable`) seviyesinde yap.

## 4. MVVM, KOMUTLAR VE KLAVYE MİMARİSİ
- **Zorunlu `CanExecute`:** Silme/yıkıcı işlem, gönderme (submit), dışa aktarma (export), ödeme veya sayfalama (paging) yapan HER komut mutlaka `CanExecute` tanımlamalı (örn. `CartItems.Count > 0`, `SelectedItem != null`, `CurrentPage > 1`) ve durum değiştiğinde `NotifyCanExecuteChanged()` çağırmalıdır.
- **Kesin Binding Doğrulaması:** XAML binding yollarını asla tahmin etme. XAML düzenlemeden önce hedef ViewModel/DTO üzerindeki gerçek property adını doğrula.
- **Pencere Seviyesi Kısayollar:** Global kısayollar (`Ctrl+K`, `Ctrl+N`, `Alt+Left`, `Alt+Right`, `Esc`) alt kontrollerin içine hapsedilmemeli, `MainWindow` / pencere seviyesi `InputBindings` içinde tanımlanmalıdır.
- **Açık Başlangıç ve Sınır Durumları:** Varsayılan aktif filtreyi (`Tümü`) açıkça ata, gerçek KPI sayılarını bağla (sabit placeholder kullanma), boş zorunlu alanlar için satır içi kırmızı doğrulama hatası göster ve hata/kritik sayısı `0` olduğunda asla tehlike/kırmızı stil uygulama.

## 5. WPF TASARIM SİSTEMİ, TEMALAR VE YERLEŞİM (ZERO PATCHWORK UI)
- **`GlobalControls.xaml` Güveni:** `Views/*.xaml` içindeki `TextBox`, `ComboBox`, `DatePicker`, `CheckBox` veya ikincil `Button` kontrollerine ASLA yerel sabit (`#hex` veya açık gri) `Background`, `Foreground` veya `BorderBrush` verme. Her zaman `{DynamicResource ...}` semantik token'larını (`SurfaceCardBrush`, `TextPrimaryBrush`, `AccentPrimaryBrush` vb.) kullan; tema fırçaları için `StaticResource` kullanma (`0` `StaticResource` for theme brushes).
- **3-Tema Eşitliği (Parity):** Yeni bir kaynak anahtarı eklerken veya yeniden adlandırırken, aynı anahtar sayısını koruyarak HER ÜÇ tema dosyasına da (`Theme.PremiumLight.xaml`, `Theme.MidnightDark.xaml`, `Theme.Glassmorphism.xaml`) ekle.
- **Bozuk İkon ve Emoji Yasağı:** XAML içinde ASLA ham emoji veya doğrulanmamış Unicode karakterler (`▯`, `Ň` vb.) kullanma; yalnızca vektör `Path` geometrisi veya doğrulanmış Fluent ikonları kullan.
- **Bulanıklık Yapmayan Gölgeler:** `DropShadowEffect` efektini ASLA içinde `TextBlock` barındıran bir kapsayıcıya doğrudan uygulama; kardeş (sibling) bir arka plan `Border` elemanına yerleştir.
- **Çözünürlük Güvenliği (`1366x768` & `1600x938`):** İçeriği kesen sabit genişliklerden kaçın. Filtre çubuklarında `WrapPanel` veya `*` genişlikli `Grid` kolonları, uzun yan panellerde `ScrollViewer`, breadcrumb/uzun kodlarda `TextTrimming="CharacterEllipsis"` ve `DataGrid` kolonlarında `MinWidth` kullan. `ScrollBar` bileşenlerini alt bilgi (footer) ve kenarlıklardan iç boşlukla ayır. `DataGrid` içinde `ScrollViewer.CanContentScroll="False"` yapma ve dış `<ScrollViewer>` içine alma.
- **Kontrol ve Görünürlük Standartları:** Ters görünürlük için `App.xaml` içindeki `{StaticResource InverseBoolToVis}` kaynağını kullan. Yeni bir `CustomControl` (`Km*`) yazdığında `ComponentStyles.xaml` içine `<Style TargetType="...">` ve `<ControlTemplate>` ekle.

## 6. KOD HİJYENİ, GÜVENLİK VE TOKEN VERİMLİLİĞİ
- **Sıfır Regresyon Değişmezleri:** `0` boş `catch` (`Serilog.Log` zorunlu), `0` `MessageBox.Show` (`IDialogService` / `IToastService`), `0` tema fırçası `StaticResource`, constructor/async void tetiklemelerinde çıplak fire-and-forget yerine `SafeFireAndForget`, çok adımlı DB yazımlarında `ExecuteInTransactionAsync`.
- **Güvenlik ve Derleyici:** `.csproj` dosyalarına `<NoWarn>` eklemek veya `PendingModelChangesWarning` gibi EF Core uyarılarını susturmak YASAKTIR. API Controller'larında entity yerine DTO + `[Authorize(Roles = "...")]` kullan; `appsettings.json` içine gerçek parola veya üretim `Jwt:Key` yazma.
- **Minimal Okuma ve Tek Sessiz Derleme:** Dosya okumadan önce `Select-String`/grep kullan ve yalnızca ilgili aralığı (±25 satır) oku. Ara derlemeler çalıştırma; iş sonunda TEK bir final derlemesi çalıştır: `dotnet build KamatekCRM/KamatekCrm.csproj --nologo -v q` -> **0 Error, 0 Warning**.
- **MCP ile Çalışma Zamanı Doğrulaması (Mevcut Olduğunda):** UI veya DB değişikliklerinden sonra yalnızca derlemeye güvenme; `get_diagnostics` (0 binding hatası, 0 Serilog hatası) kontrolü yap ve hem `PremiumLight` hem `MidnightDark` temalarını görsel olarak doğrula.
