# Keşif ve Fiyat Teklifi Modülü - Kullanım Kılavuzu

## 📋 Genel Bakış

**Keşif ve Fiyat Teklifi** modülü, çoklu birim içeren projelerde (apartman, site, fabrika) hızlı ve sistematik teklif hazırlamak için tasarlanmıştır.

### Temel Özellikler
- 4 adımlı sihirbaz akışı
- Yapı tipi bazlı birim oluşturma
- Toplu ürün atama ("Tüm dairelere uygula")
- Otomatik maliyet hesaplama
- JSON tabanlı yapı saklama

---

## 🚀 Nasıl Kullanılır?

### Erişim
1. Ana ekranda sol menüden **"💰 Keşif & Teklif"** butonuna tıklayın
2. 4 adımlı sihirbaz penceresi açılır

---

## ADIM 1: Proje Bilgileri

| Alan | Açıklama | Zorunlu |
|------|----------|---------|
| **Müşteri** | Kayıtlı müşteriler listesinden seç | ✅ Evet |
| **Proje Başlığı** | Projenin kısa adı | ✅ Evet |
| **Kategori** | CCTV, Alarm, Network vb. | Hayır |
| **Proje Adresi** | Kurulum adresi | Hayır |
| **Keşif Notları** | Sahada alınan notlar | Hayır |

**İleri** butonuna tıklayarak Adım 2'ye geçin.

---

## ADIM 2: Yapı Sihirbazı

Bu adımda projenin fiziksel yapısını tanımlarsınız.

### Yapı Tipleri

#### 1️⃣ Tek Birim
- Villa, müstakil ev, dükkan gibi tek noktalar için
- Sadece 1 birim oluşturulur

#### 2️⃣ Apartman
Kat ve daire sayısını girerek tüm birimleri otomatik oluşturur.

| Parametre | Örnek |
|-----------|-------|
| Kat Sayısı | 5 |
| Her Katta Daire | 4 |
| **Toplam** | 20 daire |

**Ek Alanlar:**
- ☑ Giriş (Bina girişi için ayrı birim)
- ☑ Bahçe
- ☑ Otopark

#### 3️⃣ Site
Blok bazlı yapılar için. **Blok isimlerini manuel girersiniz**.

**Örnek:**
1. "A Blok" yazın → **+ Ekle** tıklayın
2. "B Blok" yazın → **+ Ekle** tıklayın
3. Her blok için kat ve daire sayısı girin

| Parametre | Değer |
|-----------|-------|
| Bloklar | A Blok, B Blok, C Blok |
| Her Blok Kat Sayısı | 10 |
| Her Katta Daire | 4 |
| **Toplam** | 3 × 10 × 4 = 120 daire + 3 giriş |

#### 4️⃣ Fabrika/Ticari
Önceden tanımlı bölgeler seçilir:
- Üretim
- Depo
- Ofis
- Yemekhane
- Güvenlik
- Otopark
- Giriş/Lobi

### Birimleri Oluştur
Parametreleri girdikten sonra **"🔄 Birimleri Oluştur"** butonuna tıklayın.

Oluşturulan birimler listelenir:
```
☑ Daire 1  ☑ Daire 2  ☑ Daire 3  ☑ Daire 4
☑ Daire 5  ☑ Daire 6  ☑ Daire 7  ☑ Daire 8
...
```

> **İpucu:** İstemediğiniz birimlerin checkbox'ını kaldırarak ürün atamasından hariç tutabilirsiniz.

---

## ADIM 3: Sistem Seçimi

Ürünleri seçerek birimlere atarsınız.

### Ürün Ekleme Akışı

1. **Ürün Seç:** Stoktan bir ürün seçin
2. **Birim Başına Adet:** Her daire/birim için kaç adet (örn: 2 kamera)
3. **Uygulama Şekli:**
   - ○ Tüm Birimlere Uygula (20 daire × 2 kamera = 40 kamera)
   - ○ Sadece Girişlere Uygula (sadece bina/blok girişleri)
4. **"+ Teklif'e Ekle"** butonuna tıklayın

### Örnek Senaryo

| Ürün | Birim Başına | Uygulama | Toplam Adet | Birim Fiyat | Tutar |
|------|--------------|----------|-------------|-------------|-------|
| 2MP IP Kamera | 2 | Tüm Birimler (20) | 40 | ₺1.200 | ₺48.000 |
| NVR 16 Kanal | 1 | Sadece Girişler (1) | 1 | ₺5.000 | ₺5.000 |
| CAT6 Kablo (100m) | 1 | Tüm Birimler (20) | 20 | ₺400 | ₺8.000 |

**MALZEME TOPLAMI: ₺61.000**

---

## ADIM 4: Finansal Özet

Son adımda tüm maliyetleri görüntüler ve düzenlersiniz.

### Maliyet Kalemleri

| Kalem | Açıklama |
|-------|----------|
| **Malzeme** | Otomatik hesaplanır (değiştirilemez) |
| **İşçilik** | Manuel giriş yapılır |
| **İskonto (%)** | Yüzde olarak indirim |
| **İskonto Tutarı** | Otomatik hesaplanır |
| **TOPLAM** | Genel toplam |

### Örnek Hesaplama

```
Malzeme:        ₺61.000
İşçilik:        ₺10.000
─────────────────────────
Ara Toplam:     ₺71.000
İskonto (%5):   -₺3.550
─────────────────────────
GENEL TOPLAM:   ₺67.450
```

### Kaydetme
**"💾 Kaydet"** butonuna tıkladığınızda:
- Proje veritabanına kaydedilir
- Proje kodu atanır (örn: `PRJ-2026-001`)
- Yapı tanımı JSON olarak saklanır

---

## 📊 Teknik Detaylar

### Veri Modeli

```
ServiceProject
├── StructureType (Enum)
├── StructureDefinitionJson (Yapı tanımı)
├── TotalUnitCount (Birim sayısı)
├── QuoteItemsJson (Teklif kalemleri)
└── DiscountPercent (İskonto)
```

### Yapı Tipi Enum
```csharp
public enum StructureType
{
    SingleUnit = 0,   // Tek birim
    Apartment = 1,    // Apartman
    Site = 2,         // Site
    Commercial = 3    // Fabrika/Ticari
}
```

### JSON Yapısı (StructureDefinitionJson)

```json
{
  "Type": 1,
  "FloorCount": 5,
  "UnitsPerFloor": 4,
  "IncludeGroundFloor": true,
  "IncludeEntrance": true,
  "BlockNames": [],
  "SelectedZones": []
}
```

---

## 🔄 İş Akışı Diyagramı

```
┌─────────────┐     ┌─────────────┐     ┌─────────────┐     ┌─────────────┐
│   ADIM 1    │     │   ADIM 2    │     │   ADIM 3    │     │   ADIM 4    │
│  Proje &    │ ──► │   Yapı      │ ──► │  Sistem     │ ──► │  Finansal   │
│  Müşteri    │     │  Sihirbazı  │     │  Seçimi     │     │   Özet      │
└─────────────┘     └─────────────┘     └─────────────┘     └─────────────┘
      │                   │                   │                   │
      ▼                   ▼                   ▼                   ▼
  Müşteri seç        Birim oluştur      Ürün ata           Kaydet
  Başlık gir         (20 daire)         (40 kamera)        (PRJ-2026-001)
```

---

## ⚡ Hızlı İpuçları

1. **Blok isimlerini önceden planlayın:** Site projelerinde blok isimlerini sıralı girin
2. **Girişleri ayrı düşünün:** NVR, switch gibi merkezi cihazları "Sadece Girişlere" atayın
3. **İskontoyu sonra girin:** Önce tüm malzemeleri ekleyin, sonra iskonto uygulayın
4. **Notları kullanın:** Keşif notlarına sahada aldığınız bilgileri yazın

---

## 🔜 Gelecek Özellikler

- [ ] Proforma PDF çıktısı (QuestPDF entegrasyonu)
- [ ] Birim bazlı özel ürün ataması
- [ ] Şablon kaydetme ve yükleme
- [ ] Mevcut projeyi düzenleme
