# KamatekCRM - AI Agent Memory

## 📍 Proje Konumu
**Ana Dizin:** `C:\Antigravity Proje`
**Solution:** `KamatekCRM.sln`

## 🏗️ Proje Yapısı
```
KamatekCRM/
├── KamatekCrm/                   # WPF Desktop Application
├── KamatekCrm.Web/               # Minimal API + HTMX Web App
├── KamatekCrm.API/               # Backend Web API
├── KamatekCrm.Shared/            # Shared Class Library
└── docs/                         # Dokümantasyon
    ├── CHANGELOG.md              # Tüm değişiklikler
    ├── TEKNIK_HARITA.md          # Mimari dokümantasyon
    └── MEMORY.md                 # Bu dosya
```

## 🎯 Yapılan Temel Çalışmalar (v8.8)

### 1. Dependency Injection (DI) Düzeltmeleri
- **Sorun:** Birçok ViewModel DI container'a kayıtlı değildi
- **Çözüm:** 13+ ViewModel ve Window DI'ya eklendi
- **Dosya:** `Extensions/ServiceCollectionExtensions.cs`

### 2. NullReferenceException Çözümleri
Aşağıdaki View'lerde XAML inline ViewModel oluşturma hatası düzeltildi:
- ✅ `UsersView.xaml`
- ✅ `SystemLogsView.xaml`
- ✅ `FieldJobListView.xaml`
- ✅ `ProjectQuoteEditorWindow.xaml`
- ✅ `ProjectQuoteWindow.xaml`

**Çözüm:** `<UserControl.DataContext>` blokları kaldırıldı, ViewModel'ler DI'dan çözülüyor.

### 3. Constructor Refactoring
Parametresiz ctor kullanan ViewModel'ler DI uyumlu hale getirildi:
- `AnalyticsViewModel` → `AppDbContext` inject
- `FinancialHealthViewModel` → `AppDbContext` inject
- `PipelineViewModel` → `AppDbContext` inject
- `RoutePlanningViewModel` → `AppDbContext` inject
- `SchedulerViewModel` → `AppDbContext` inject

### 4. UI/UX ve Renk Tutarlılığı
#### Tema Renkleri (Dark/Light)
- `ThemePrimary` - Ana renk
- `ThemeSuccess` - Başarı/Yeşil
- `ThemeError` - Hata/Kırmızı
- `ThemeWarning` - Uyarı/Turuncu
- `ThemeTextPrimary` - Ana metin
- `ThemeTextSecondary` - İkincil metin
- `ThemeTextTertiary` - Üçüncül metin
- `ThemeBackground` - Arka plan
- `ThemeSurface` - Kart/ yüzey arka planı

#### Yeni Stiller Eklendi (Styles.xaml)
- `ReadableTextBlock` - Temel okunabilirlik
- `HeaderTextBlock` - Başlık stilleri
- `SubHeaderTextBlock` - Alt başlık stilleri
- `BodyTextBlock` - Gövde metni
- `LabelTextBlock` - Etiket stilleri
- `CaptionTextBlock` - Küçük metinler

### 5. Hardcoded Renk Dönüşümleri
| Eski Renk | Yeni Renk |
|-----------|-----------|
| `#3B82F6` | `{DynamicResource ThemePrimary}` |
| `#10B981` | `{DynamicResource ThemeSuccess}` |
| `#EF4444` | `{DynamicResource ThemeError}` |
| `#F59E0B` | `{DynamicResource ThemeWarning}` |
| `#424242` | `{DynamicResource ThemeTextPrimary}` |
| `#616161` | `{DynamicResource ThemeTextSecondary}` |
| `#757575` | `{DynamicResource ThemeTextSecondary}` |
| `#888` | `{DynamicResource ThemeTextSecondary}` |
| `#F5F5F5` | `{DynamicResource ThemeBackground}` |
| `#E3F2FD` | `{DynamicResource ThemePrimaryLight}` |

### 6. Güvenlik Güncellemesi
- **SixLabors.ImageSharp** 3.1.8 → 3.1.12 güncellendi

## 📋 Çalışma Protokolü

### Her Oturum Başlangıcında:
1. `docs/CHANGELOG.md` dosyasını oku
2. `docs/TEKNIK_HARITA.md` dosyasını incele
3. Kritik modülleri kontrol et:
   - `ViewModels/` - DI kayıtları
   - `Views/` - XAML binding hataları
   - `Extensions/ServiceCollectionExtensions.cs` - DI container

### Yeni View/ViewModel Ekleme:
1. ViewModel constructor'ında parametre varsa (örn: `IAuthService`):
   - XAML'da `<UserControl.DataContext>` koyma!
   - Sadece DI container'a kaydet (`ServiceCollectionExtensions.cs`)
2. Renkler için hardcoded hex kod kullanma!
   - Kullan: `{DynamicResource ThemeTextPrimary}` vb.

### Değişiklik Kaydı:
Her düzeltmeden sonra `docs/CHANGELOG.md` güncellenmeli.

## 🔧 Kritik Dosyalar
- `Extensions/ServiceCollectionExtensions.cs` - DI kayıtları
- `Resources/Themes/DarkTheme.xaml` - Dark tema
- `Resources/Themes/LightTheme.xaml` - Light tema
- `Resources/Styles.xaml` - Stil tanımlamaları
- `App.xaml.cs` - Uygulama başlangıcı

## ⚠️ Bilinen Sınırlamalar
- `GetTaskDetailQuery.cs` - 4 null reference uyarısı (çalışmayı etkilemez)
- Tüm ViewModel'ler DI container'dan çözülmeli
- Inline XAML ViewModel instantiation yasak!

## 📅 Son Güncelleme
2026-02-18 - v8.8

## 🔄 Sonraki Oturumda Yapılacaklar
- [ ] Yeni UI/UX iyileştirmeleri
- [ ] Performans optimizasyonları
- [ ] Yeni özellik geliştirmeleri
- [ ] Hata ayıklama ve test

---
**Not:** Bu dosya otomatik olarak güncellenir. Elle düzenlemeyin.
