# KamatekCRM - Proje Özeti

## Genel Bilgiler

| Özellik | Değer |
|---------|-------|
| **Proje Adı** | KamatekCRM |
| **Amaç** | Teknik Servis & Stok Yönetim Sistemi |
| **Hedef Kitle** | Yerel elektronik/güvenlik şirketleri |
| **Platform** | Windows Desktop |

## Teknoloji Stack'i

- **.NET 8** - Framework
- **WPF (MVVM)** - UI Framework
- **Entity Framework Core** - ORM (Code-First)
- **SQLite** - Veritabanı
- **MaterialDesignInXAML** - UI Theme
- **ClosedXML** - Excel import/export
- **WebView2** - Harita entegrasyonu
- **LiveChartsCore** - BI Grafikleri
- **QuestPDF** - PDF Raporlama

## Ana Özellikler

### 📋 Müşteri Yönetimi
- Bireysel/Kurumsal müşteri tipleri
- Otomatik müşteri kodu oluşturma
- Detaylı adres yönetimi (Türkiye formatı)
- Servis geçmişi ve finansal özet

### 🔧 Servis İş Emirleri
- **Tek sayfalık form arayüzü** (wizard kaldırıldı)
- Yapı Türü seçimi (Müstakil/Apartman/Site/İşyeri)
- 8 farklı iş kategorisi (CCTV, Yangın, Alarm, vb.)
- Dinamik teknik form şablonları
- "Tüm Birimlere Uygula" malzeme çarpanı
- Öncelik ve maliyet hesaplama

### ⚡ Gelişmiş Servis Yaşam Döngüsü
- **Arıza Kaydı**: Hızlı, basitleştirilmiş form + hibrit cihaz seçici
- **Proje Akışı**: 5 fazlı yaşam döngüsü (Keşif → Teklif → Onay → Uygulama → Final)
- Stok rezervasyonu ve final ayarlama mantığı
- Tahmini vs Gerçek miktar karşılaştırma

### 📦 Ürün/Stok Yönetimi
- Excel'den toplu ürün import
- Kategori bazlı ürün tanımı
- Teknik özellikler (JSON formatında)
- Açılış stoğu oluşturma

### 📊 Envanter Takibi
- Depo yönetimi (Ana Depo default)
- Stok hareketleri ve denetim izi
- Depolar arası transfer
- Stok sayım modülü

### 👤 Kullanıcı Yönetimi
- Login/Logout sistemi (SHA256 şifreleme)
- Rol tabanlı erişim kontrolü (Admin, Personel) + Granular Permissions
- Varsayılan: admin.user / 1234

### 🏢 Enterprise ERP (YENİ)
- **BI Analytics**: 6 aylık trend, kategori dağılımı, KPI dashboard
- **B2B Procurement**: Tedarikçi yönetimi, satınalma siparişleri
- **Digital Archive**: Müşteri/ürün/servis belgeleri ve fotoğraflar
- **RBAC**: Buton seviyesinde yetkilendirme

## Mevcut Durum

✅ **Tamamlanan:**
- Temel CRUD işlemleri
- Wizard tabanlı iş emri sistemi
- Excel import ile stok oluşturma
- ServiceJobsView master list tasarımı
- CustomerDetailView tab yapısı
- Login/Logout ve RBAC
- Gelişmiş Servis Yaşam Döngüsü (Arıza + Proje)
- MainContentView hızlı erişim butonları
- **Enterprise ERP Modülleri** (Analytics, B2B, Archive, RBAC)

🔄 **Devam Eden:**
- Proforma PDF oluşturma (QuestPDF)
- Raporlama modülleri
- UI polish ve optimizasyon
