# KAMATEK CRM - TAM TEKNIK DOKÜMANTASYON

Bu belge, KamatekCRM projesine ait tüm teknik kılavuzların, mimari detayların ve kullanım dokümanlarının birleştirilmiş halidir.

---

## 1. BAŞLANGIÇ VE GENEL BAKIŞ (README)

Welcome to the comprehensive technical documentation for KamatekCRM. This portal provides granular details on the system's architecture, data management, and business logic.

---

## 2. PROJE ÖZETİ (PROJE_OZETI.md)

### Genel Bilgiler

| Özellik | Değer |
|---------|-------|
| **Proje Adı** | KamatekCRM |
| **Amaç** | Teknik Servis & Stok Yönetim Sistemi |
| **Hedef Kitle** | Yerel elektronik/güvenlik şirketleri |
| **Platform** | Windows Desktop |

### Teknoloji Stack'i

- **.NET 9** - Framework
- **WPF (MVVM)** - UI Framework
- **Blazor (WebAssembly/Server)** - Technician Web Panel
- **Entity Framework Core** - ORM (Code-First)
- **PostgreSQL** - Veritabanı
- **MaterialDesignInXAML** - UI Theme
- **LiveChartsCore** - BI Grafikleri
- **QuestPDF** - PDF Raporlama

### Ana Özellikler

*   **Müşteri Yönetimi**: Bireysel/Kurumsal müşteri tipleri, otomatik kodlama, detaylı adres yönetimi.
*   **Servis İş Emirleri**: Tek sayfalık form arayüzü, 8 farklı iş kategorisi, dinamik teknik şablonlar.
*   **Servis Yaşam Döngüsü**: Arıza Kaydı ve 5 fazlı Proje Akışı (Keşif → Teklif → Onay → Uygulama → Final).
*   **Envanter Takibi**: WAC (Ağırlıklı Ortalama Maliyet) tabanlı stok yönetimi, depolar arası transfer, Excel import.
*   **Enterprise ERP**: BI Analytics, B2B Procurement, Digital Archive, RBAC.
*   **Teknisyen Web Paneli**: Blazor Server + MudBlazor ile sahadan görev yönetimi ve fotoğraf yükleme.

---

## 3. MİMARİ REHBER (ARCHITECTURE.md)

### 3.1 Hibrit Mimari (Hybrid Architecture)
KamatekCRM utiliza a **Hybrid Architecture** where a desktop application (WPF) serves as the host for a distributed system, including a Web API and a Blazor Web App.

*   **Host (WPF Desktop App)**: Controls the lifecycle of the entire system.
*   **Backend (ASP.NET Core API)**: Self-hosted within the WPF process via Kestrel.
*   **Frontend (Blazor Server)**: A separate process launched and managed by the WPF Host.

### 3.2 Dependency Injection (DI)
The system follows a strict **Constructor Injection** pattern managed by `Microsoft.Extensions.DependencyInjection`.
*   **Singleton**: Services holding global state (e.g., `NavigationService`, `ToastService`).
*   **Scoped**: Database context and Unit of Work (`AppDbContext`, `IUnitOfWork`).
*   **Transient**: ViewModels and lightweight utilities.

### 3.3 CQRS with MediatR
To maintain a clean separation between commands (writes) and queries (reads), the system implements the **CQRS** pattern using **MediatR**.
1.  **Commands**: Located in `Application/Features/[Module]/Commands`.
2.  **Queries**: Located in `Application/Features/[Module]/Queries`.
3.  **Handlers**: Contain the actual business logic.

---

## 4. VERİTABANI REHBERİ (DATABASE.md)

### 4.1 PostgreSQL Entegrasyonu
Sistem, yüksek eşzamanlılık ve profesyonel veri yönetimi için SQLite'tan **PostgreSQL**'e taşınmıştır.
*   **Provider**: `Npgsql.EntityFrameworkCore.PostgreSQL`
*   **JSON Support**: Teknik özellikler için `JSONB` sütun tipi kullanılır.

### 4.2 Gelişmiş Özellikler
*   **Soft Delete**: `ISoftDeletable` arayüzü ve Global Query Filter ile veriler fiziksel olarak silinmez.
*   **Automated Audit Trail**: `CreatedDate`, `ModifiedDate` ve `DeletedAt` alanları otomatik doldurulur. Tüm zaman damgaları **UTC** formatındadır.
*   **Concurrency Control**: Kritik işlemlerde Optimistic Concurrency uygulanır.

---

## 5. TEMEL ALGORİTMALAR (ALGORITHMS.md)

### 5.1 SLA ve Bakım Otomasyonu
`SlaService`, aktif sözleşmeleri (`MaintenanceContract`) tarayarak günü gelen bakımlar için otomatik `ServiceJob` oluşturur ve bir sonraki bakım tarihini hesaplar.

### 5.2 Stok Değerleme (Weighted Average Cost - WAC)
Stok girişlerinde maliyet şu formülle hesaplanır:
$$NewAverageCost = \frac{(CurrentQty \times CurrentCost) + (InboundQty \times InboundCost)}{CurrentQty + InboundQty}$$

### 5.3 Sayfalama (Pagination)
Büyük veri setleri `PagedResult<T>` yapısı ile sunucu tarafında sayfalanır, böylece bellek kullanımı minimize edilir.

---

## 6. WEB API VE TEKNİSYEN ENTEGRASYONU (WEB_API_GUIDE.md)

### 6.1 API Mimarisi
API, WPF içindeki **Kestrel** sunucusu üzerinden host edilir ve `http://0.0.0.0:5050` portundan yayın yapar.

### 6.2 Güvenlik (JWT)
*   **Yöntem**: HS256 algoritmalı stateless JWT.
*   **Akış**: Login → Token Üretimi → `Authorization: Bearer` başlığı ile istek.

### 6.3 Teknisyen İş Akışı
*   Görev Listeleme (`/api/tasks`)
*   Durum Güncelleme (`PUT /api/tasks/{id}/status`)
*   Fotoğraf Yükleme (`POST /api/tasks/{id}/photos`)

---

## 7. TEKNİK HARİTA (TEKNIK_HARITA.md)

### Solution Yapısı
```
KamatekCRM/                       # Solution Root
├── KamatekCrm/                   # WPF Desktop Application
├── KamatekCrm.Web/               # Blazor Web App (Technician Panel)
├── KamatekCrm.API/               # Backend Web API
├── KamatekCrm.Shared/            # Shared Class Library
```

### Kritik Klasörler
*   `ViewModels/`: İş mantığı (MVVM)
*   `Views/`: XAML arayüzleri
*   `Repositories/`: Unit of Work ve Transaction yönetimi
*   `Application/Features/`: CQRS Command ve Query Handler'ları
*   `Infrastructure/`: Global Exception Handler ve Serilog yapılandırması

---

## 8. KEŞİF VE FİYAT TEKLİFİ KILAVUZU (KESIF_TEKLIF_KILAVUZU.md)

Çoklu birim içeren (Site, Fabrika, Apartman) projelerde 4 adımlı sihirbaz akışı kullanılır:
1.  **Proje Bilgileri**: Müşteri ve başlık seçimi.
2.  **Yapı Sihirbazı**: Kat/daire/blok sayısının girilmesi (JSON tabanlı yapı).
3.  **Sistem Seçimi**: "Tüm birimlere uygula" özelliği ile hızlı ürün atama.
4.  **Finansal Özet**: İşçilik ve iskonto yönetimi.

---

## 9. MODERNİZASYON VE UX PLANI (UX_PLAN.md)

Sistem modernizasyonu şu 4 sütun üzerine kuruludur:
1.  **Klavye Hakimiyeti**: Enter ile kaydet, Esc ile iptal, mantıklı Tab sırası.
2.  **Hata Geçirmez Girişler**: `NumericTextBox` ve `ErrorTemplate` stilleri.
3.  **Akışkan Izgaralar**: Çift tıkla düzenle, Delete ile sil binding'leri.
4.  **Anlık Geri Bildirim**: Toast bildirimleri ve Loading Overlay entegrasyonu.

---

## 10. DEĞİŞİKLİK GÜNLÜĞÜ (CHANGELOG.md)

### Son Sürümler
*   **v6.9**: PostgreSQL Geçişi, UTC Fix, Remote Access, Profesyonel Dokümantasyon.
*   **v6.8**: Build Fixes, DI Refactoring, Transient MainWindow.
*   **v6.7**: Photo Upload, Google Maps Navigasyonu, Database Reset.
*   **v6.6**: Toast & Loading UI, Web App Foundation.

---
*Professional CRM Solution for Technical Services - 2024*
