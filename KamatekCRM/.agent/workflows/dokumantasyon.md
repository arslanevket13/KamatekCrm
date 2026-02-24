---
description: Dokümantasyon sistemi ile çalışma kuralları
---

# Dokümantasyon Sistemi İş Akışı

Bu workflow, KamatekCRM projesinde yapılacak her değişiklik için uyulması gereken dokümantasyon kurallarını tanımlar.

## Görev Öncesi Hazırlık

### 1. Bağlam Analizi
Her yeni görev öncesinde şu dosyaları oku:

```bash
# Değişiklik geçmişini incele
docs/CHANGELOG.md

# Teknik mimariyi anla
docs/TEKNIK_HARITA.md

# Proje genel yapısını gözden geçir (gerekirse)
docs/PROJE_ÖZETI.md
```

**Amaç:** Önceki değişikliklerden haberdar ol, mevcut mimariyi anla, tutarlı çalışmalar yap.

### 2. Kritik Modül Kontrolü
Eğer yapılacak değişiklik `TEKNIK_HARITA.md` içinde "Kritik Modüller" bölümünde belirtilen dosyalara dokunuyorsa:

**Kritik Dosyalar:**
- `ViewModels/ServiceJobViewModel.cs` (Stok düşüm mantığı)
- `Data/AppDbContext.cs` (Veritabanı yapılandırması)
- `Models/*.cs` (Entity modelleri - migration gerektirebilir)
- `App.xaml` (DataTemplate mapping)

**Aksiyon:**
1. Değişiklik planını hazırla
2. Implementation plan oluştur
3. Kullanıcıdan onay iste (notify_user)
4. Onay aldıktan sonra değişikliği uygula

### 3. Güvenlik Kuralları Kontrolü
`TEKNIK_HARITA.md` → "Güvenlik ve Performans Notları" bölümünü kontrol et:
- Veri validasyonu gereklilikleri
- Delete behavior kuralları
- Transaction yönetimi

## Görev Sırası İş Akışı

### Adım 1: Değişikliği Uygula
Kodlama işlemini yap, testleri çalıştır.

### Adım 2: Dokümantasyonu Güncelle
Her görev tamamlandığında **aynı commit içinde** şu dosyaları güncelle:

#### A. CHANGELOG.md Güncelleme (Zorunlu)
Her değişiklik için yeni bir giriş ekle:

```markdown
## [Versiyon] - YYYY-MM-DD

### [DEĞİŞİKLİK_TÜRÜ] - Kısa Başlık

**Tarih:** YYYY-MM-DD

**Görev Hedefi:** 
Yapılan işin amacı (1-2 cümle)

**Etkilenen Dosyalar:**
- `tam/yol/dosya1.cs` (YENİ/DEĞİŞTİRİLDİ/SİLİNDİ)
- `tam/yol/dosya2.xaml` (DEĞİŞTİRİLDİ)

**Teknik Detay & Gerekçe:**
- Değişikliğin nasıl yapıldığı
- Neden bu yaklaşım seçildi
- Algoritmik değişiklikler
- Bağımlılık güncellemeleri
- Optimizasyonlar

**Etki Analizi & Test Senaryoları:**

**Etkilenen Özellikler:**
- Özellik 1
- Özellik 2

**Test Senaryoları:**
1. ✅ Test senaryosu 1
2. ✅ Test senaryosu 2
3. ✅ Test senaryosu 3
```

**Değişiklik Türleri:**
- `[EKLENDİ]`: Yeni özellik veya dosya
- `[DEĞİŞTİRİLDİ]`: Mevcut fonksiyonalitede değişiklik
- `[KALDIRILDI]`: Özellik veya dosya kaldırıldı
- `[DÜZELTİLDİ]`: Bug fix

#### B. TEKNIK_HARITA.md Güncelleme (Koşullu)
Şu durumlarda güncelle:

**Mimari Değişiklikler:**
- Yeni katman/bileşen eklendi
- Bileşenler arası ilişki değişti
- Yeni design pattern uygulandı

**Veri Akışı Değişiklikleri:**
- Kritik iş mantığı değişti
- Yeni CRUD operasyonu eklendi
- Transaction yönetimi güncellendi

**Yeni Kritik Modül:**
- Yeni bir kritik kod yolu eklendi
- Mevcut kritik modülde önemli değişiklik

**Güvenlik/Performans:**
- Yeni güvenlik kuralı
- Performans optimizasyonu
- Yeni index eklendi

#### C. PROJE_ÖZETI.md Güncelleme (Nadir)
Şu durumlarda güncelle:

**Teknoloji Yığını Değişikliği:**
- Yeni NuGet paketi eklendi
- Framework versiyonu güncellendi
- Yeni dış servis entegrasyonu

**Özellik Değişikliği:**
- Yeni ana özellik eklendi
- Mevcut özellik kaldırıldı
- Sınırlamalar değişti

**Kurulum Talimatları:**
- Yeni ortam değişkeni gerekli
- Kurulum adımları değişti

### Adım 3: Tutarlılık Kontrolü
Üç dosya arasında tutarlılık kontrol et:

**Örnek Kontroller:**
- Tech Stack'e yeni kütüphane eklendiyse → `TEKNIK_HARITA.md` ilgili bölümü güncellendi mi?
- Yeni entity eklendiyse → `TEKNIK_HARITA.md` veri akışı güncellendi mi?
- Migration oluşturulduysa → `CHANGELOG.md` etkilenen dosyalar listesinde var mı?

## Versiyon Yönetimi

### Semantic Versioning
Proje `MAJOR.MINOR.PATCH` formatını kullanır:

- **MAJOR (X.0.0):** Breaking changes (API değişiklikleri, veritabanı şeması değişiklikleri)
- **MINOR (0.X.0):** Yeni özellikler (geriye uyumlu)
- **PATCH (0.0.X):** Bug fixes (geriye uyumlu)

### Versiyon Güncelleme Zamanı
- `PROJE_ÖZETI.md` başlığındaki versiyon numarasını güncelle
- `CHANGELOG.md` içinde yeni versiyon başlığı ekle
- Git tag oluştur: `git tag v1.2.3`

## Özel Durumlar

### Acil Bug Fix
Kritik bug fix için hızlı akış:
1. Bug'ı düzelt
2. `CHANGELOG.md` içine `[DÜZELTİLDİ]` girişi ekle
3. Test senaryolarını mutlaka yaz
4. Diğer dokümantasyon güncellemeleri sonraya bırakılabilir

### Deneysel Özellik
Deneysel/beta özellikler için:
1. `CHANGELOG.md` içinde `[Unreleased]` bölümüne ekle
2. `PROJE_ÖZETI.md` → Sınırlamalar bölümünde belirt
3. Stabil hale gelince versiyonla

### Refactoring
Kod refactoring (davranış değişikliği yok):
1. `CHANGELOG.md` → `[DEĞİŞTİRİLDİ]` olarak kaydet
2. Gerekçeyi açıkla (performans, okunabilirlik, maintainability)
3. Test senaryolarını yaz (mevcut davranışın korunduğunu göster)

## Kontrol Listesi

Her görev sonunda şunu kontrol et:

- [ ] Kod değişiklikleri tamamlandı
- [ ] Testler yazıldı ve geçti
- [ ] `CHANGELOG.md` güncellendi
- [ ] `TEKNIK_HARITA.md` güncellendi (gerekirse)
- [ ] `PROJE_ÖZETI.md` güncellendi (gerekirse)
- [ ] Dosyalar arası tutarlılık kontrol edildi
- [ ] Commit mesajı açıklayıcı yazıldı
- [ ] Kritik modül değişikliği için onay alındı (gerekirse)

## Örnek Commit Mesajı

```
feat: Müşteri profil sayfası eklendi

- CustomerDetailView ve CustomerDetailViewModel oluşturuldu
- Müşteri geçmişi ve finansal bilgiler gösteriliyor
- Inline editing kaldırıldı

Dokümantasyon:
- CHANGELOG.md güncellendi (v0.6.0)
- TEKNIK_HARITA.md veri akışı bölümü güncellendi
```

## Yardımcı Komutlar

```bash
# Dokümantasyon dosyalarını görüntüle
cat docs/CHANGELOG.md
cat docs/TEKNIK_HARITA.md
cat docs/PROJE_ÖZETI.md

# Son değişiklikleri kontrol et
git diff docs/

# Dokümantasyon commit'i
git add docs/
git commit -m "docs: [açıklama]"
```
