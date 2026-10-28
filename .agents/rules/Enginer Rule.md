---
trigger: always_on
description: Consult the graphify knowledge graph at graphify-out/ for codebase and architecture questions.
---

# KAMATEK CRM - ZORUNLU MÜHENDİSLİK VE MİMARİ KURALLARI

## 1. KEŞİF VE TEKRAR KULLANIM (REUSE BEFORE CREATE)
- Yeni bir sınıf, enum, converter veya yardımcı metot oluşturmadan ÖNCE mutlaka `KamatekCrm.Shared`, `KamatekCrm.Application` ve `KamatekCRM/Converters` dizinlerinde arama yap.
- Sonuç dönüşlerinde YALNIZCA `KamatekCrm.ApplicationCore` içindeki mevcut `Result<T>` sınıfını kullan; yeni `Result` sınıfı tanımlama.
- Parasal yuvarlamalarda kendi `Math.Round` metodunu yazma; YALNIZCA `FinancialCalculator.RoundMoney` ve `FinancialCalculator.CalculateMultiLine` kullan.
- Şifre doğrulama ve üretmede YALNIZCA merkezi `BCrypt` tabanlı servisi kullan; `PBKDF2` veya düz metin karşılaştırması yazma.

## 2. MİMARİ VE BAĞIMLILIK KURALLARI (CLEAN ARCHITECTURE)
- WPF `ViewModel` sınıfları ve `KamatekCrm.API` Controller'ları ASLA doğrudan `AppDbContext`, `IDbContextFactory<AppDbContext>` veya somut `UnitOfWork` sınıfını kullanamaz. Tüm veri erişimi `KamatekCrm.Application` katmanındaki Command/Read servisleri üzerinden yapılmalıdır.
- `((UnitOfWork)_unitOfWork)` gibi arayüzden somut sınıfa downcast yapılması ve `App.ServiceProvider` (Service Locator) kullanımı KESİNLİKLE YASAKTIR.
- ViewModel içinde `MessageBox.Show`, `new ...Window().ShowDialog()` veya `OpenFileDialog` doğrudan çağrılamaz; `IDialogService` / `IUIService` kullanılmalıdır.
- WPF masaüstü projesinde (`KamatekCRM`) kısa ömürlü servisleri `AddScoped` olarak kaydetme; `AddTransient` kullan.
- Bir sınıf 500 satırı aşıyorsa yeni metot eklemeden önce sınıfı alt sorumluluklara böl.

## 3. EF CORE, POSTGRESQL VE ASYNC KURALLARI
- `AppDbContext.SaveChanges` / `SaveChangesAsync` içinde `DbUpdateConcurrencyException` yakalayıp `OriginalValues.SetValues` ile ezmek YASAKTIR.
- Kısmi doldurulmuş entity'ler üzerinde `CurrentValues.SetValues(...)` çağırmak YASAKTIR; yalnızca güncellenen özellikleri açıkça (explicit) ata.
- Doğrudan `context.Database.BeginTransactionAsync()` çağırmak YASAKTIR (`NpgsqlRetryingExecutionStrategy` ile çöker). Her zaman `await context.ExecuteInTransactionAsync(async tx => { ... })` kullan.
- Aynı `DbContext` örneği üzerinde `Task.WhenAll` ile paralel sorgu çalıştırmak YASAKTIR.
- `.GetAwaiter().GetResult()`, `.Result` veya `.Wait()` kullanımı YASAKTIR. Tüm zincir `async Task` olmalıdır. Constructor içinde `_ = Refresh()` gibi fire-and-forget çağrılar başlatma.
- Tabloları `.ToListAsync()` ile belleğe çekip C# içinde `.Sum()` / `.Count()` / `.Skip()` yapma; tüm filtreleme ve toplamaları SQL (`IQueryable`) seviyesinde yap.

## 4. XAML VE UI/UX KURALLARI
- Bir XAML dosyasında `{DynamicResource ...}` kullanmadan önce, o anahtarın HER ÜÇ tema dosyasında (`Theme.PremiumLight.xaml`, `Theme.MidnightDark.xaml`, `Theme.HighContrast.xaml`) tanımlı olduğunu doğrula.
- Ters görünürlük için standart `BooleanToVisibilityConverter` + `ConverterParameter=Inverse` KULLANMA; `App.xaml` içindeki `{StaticResource InverseBoolToVis}` kaynağını kullan.
- Yeni bir `CustomControl` (`Km*`) yazdığında `ComponentStyles.xaml` içine mutlaka `<Style TargetType="...">` ve `<ControlTemplate>` ekle.
- `DataGrid` bileşenlerinde `ScrollViewer.CanContentScroll="False"` yapma ve `DataGrid`'i dış bir `<ScrollViewer>` içine koyma (sanallaştırmayı bozar).

## 5. GÜVENLİK VE DERLEYİCİ KURALLARI
- `.csproj` dosyalarına `<NoWarn>` eklemek veya `PendingModelChangesWarning` gibi EF Core uyarılarını susturmak YASAKTIR.
- API Controller'larında EF Core entity'lerini doğrudan `[FromBody]` olarak alma; her zaman DTO kullan ve yetki gerektiren uç noktalarda `[Authorize(Roles = "...")]` belirt.
- `appsettings.json` içine gerçek parola veya üretim `Jwt:Key` yazma.