# KamatekCRM - Değişiklik Günlüğü

## 2026-01-24

### ✅ Kritik Üretim Düzeltmeleri (Production-Ready)
5 kritik sistem açığı/hatası düzeltildi:

| # | Sorun | Düzeltme |
|---|-------|----------|
| 1 | SMS simülasyon modunda | Production-ready API çağrısı (placeholder kontrollü) |
| 2 | Gmail normal şifre hatası | Google App Password zorunluluğu + dokümantasyon |
| 3 | POS'ta stok düşmüyor (inventory yoksa) | Eksik Inventory kaydı otomatik oluşturma |
| 4 | Temp PDF dosyaları birikimi | try-finally ile otomatik temizlik |
| 5 | Restore sonrası Ghost Data | Restart önceliklendirme, EF cache bypass |

**Güncellenen Dosyalar:**
- `Services/SmsService.cs`
- `Services/EmailService.cs`
- `ViewModels/DirectSalesViewModel.cs`
- `ViewModels/ProjectQuoteEditorViewModel.cs`
- `ViewModels/SettingsViewModel.cs`

---

## 2026-01-23

### ✅ İletişim Motoru (SMS & E-Posta)
Müşterilerle iletişim için profesyonel SMS ve E-Posta altyapısı eklendi.

**Yeni Servisler:**
- `Services/EmailService.cs` [YENİ]: SMTP ile PDF teklif gönderimi
- `Services/SmsService.cs` [YENİ]: HTTP API ile SMS bildirimi (NetGSM/Twilio uyumlu)

**Entegrasyonlar:**
- `ProjectQuoteEditorViewModel.cs`: "📧 E-POSTA GÖNDER" komutu eklendi (PDF eklentiyle)
- `RepairViewModel.cs`: Cihaz "Hazır" durumuna geçtiğinde otomatik SMS bildirimi
- `ProjectQuoteEditorWindow.xaml`: E-posta gönder butonu eklendi

---

### ✅ Otomatik Yedekleme Sistemi
SQLite veritabanı için kapsamlı yedekleme ve geri yükleme işlevselliği.

**Yeni Dosyalar:**
- `Services/BackupService.cs` [YENİ]: SQLite Backup API + ZIP sıkıştırma
- `ViewModels/SettingsViewModel.cs` [YENİ]: Yedekleme UI mantığı
- `Views/SettingsView.xaml` [YENİ]: Ayarlar ekranı (Yedek Al / Yedekten Yükle)

**Özellikler:**
- **Manuel Yedekleme:** "💾 ŞİMDİ YEDEK AL" butonu
- **Geri Yükleme:** "📂 YEDEKTEN YÜKLE" butonu (ZIP seçimi)
- **Otomatik Çıkış Yedeği:** Uygulama kapanırken arka planda yedek alınır
- **Yedek Konumu:** `Belgelerim/KamatekBackups/KamatekBackup_YYYY-MM-DD_HHmm.zip`

**Entegrasyonlar:**
- `MainContentView.xaml`: Sidebar'a "⚙️ Ayarlar" butonu eklendi
- `MainContentViewModel.cs`: `NavigateToSettingsCommand` eklendi
- `App.xaml`: `SettingsViewModel` → `SettingsView` DataTemplate eşlemesi
- `App.xaml.cs`: `OnExit` override ile otomatik yedekleme

---

### ✅ İş Emirleri Sadeleştirmesi
- `Views/ServiceJobsView.xaml`: Liste kaldırıldı, sadece "Yeni İş Emri" oluşturma butonu kaldı
- Mevcut işler için "🔧 Tamir Listesi" ve "🚜 Saha İşleri" kullanılacak

### ✅ Dashboard Intelligence (Komut Merkezi)
- `DashboardViewModel.cs`: 3 widget ile yeniden yazıldı:
  1. **🚨 Kritik Uyarılar**: Stok <= 5 olan ürünler (renk kodlu badge)
  2. **🔧 Bugünün İşleri**: Bugün planlanan işler + Teslime hazır tamirler
  3. **💰 Aylık Özet**: Toplam satış, tamamlanan işler, aktif işler
- `DashboardView.xaml`: Modern 3-kolon layout

---

## 2026-01-22

### ✅ Profesyonel Liste Görünümleri (Yeni)
**Dosyalar:**
- `ViewModels/RepairListViewModel.cs` [YENİ]: Tamir listesi filtreleme ve aksiyonlar
- `ViewModels/FieldJobListViewModel.cs` [YENİ]: Saha işleri filtreleme ve aksiyonlar
- `Views/RepairListView.xaml` [YENİ]: Modern tamir listesi UI
- `Views/FieldJobListView.xaml` [YENİ]: Modern saha işleri UI
- `Views/MainContentView.xaml`: Sidebar navigasyon butonları eklendi
- `ViewModels/MainContentViewModel.cs`: Navigasyon komutları eklendi
- `Resources/Styles.xaml`: StatusBadge, IconActionButton, FilterBarPanel, CategoryToggleButton stilleri

**Özellikler:**
- Durum badge'leri (renk kodlu pill'ler)
- Gelişmiş filtre bar (tarih, durum, arama)
- Kategori multi-select toggle butonları
- Aksiyon butonları (SMS, Fotoğraf, Yazdır, Harita, Tamamla)
- Google Maps entegrasyonu (saha işleri)

---

## 2026-01-19

### ✅ Tamamlanan

#### Manuel Stok Sayım Modülü (Yeni Özellik)
Mevcut Excel tabanlı sayım modülüne ek olarak, tekil veya belirli ürünleri hızlıca sayma imkanı sağlayan "Manuel Hızlı Sayım" sekmesi eklendi.

- **StockCountView.xaml**: TabControl yapısına dönüştürüldü
  - Tab 1: "📤 Excel Toplu Sayım" (mevcut)
  - Tab 2: "🖐️ Manuel Hızlı Sayım" (yeni)
  
- **StockCountViewModel.cs**: Manuel sayım mantığı eklendi
  - Ürün arama (SKU, Barkod, Ürün Adı)
  - Listeye ekleme/çıkarma
  - Stok fark hesaplama (renk kodlu)
  - `StockTransaction` kayıtları oluşturma (MANUAL-* referans)
  
- **Yeni Özellikler:**
  - Barkod tarayıcı desteği (arama alanına odaklanarak)
  - Anlık fark hesaplama (yeşil: fazla, kırmızı: eksik)
  - Özet kartları (Toplam, Fazla, Eksik, Farklı)
  - ReferenceId formatı: `MANUAL-yyyyMMdd-HHmmss-WarehouseId`

#### Perakende Satış (POS) Modülü (Yeni)
Mouse/Klavye için optimize edilmiş doğrudan satış modülü eklendi.

- **Yeni Entity'ler:**
  - `Models/SalesOrder.cs` - Satış siparişi
  - `Models/SalesOrderItem.cs` - Sipariş kalemi
  - `Enums/PaymentMethod.cs` - Ödeme yöntemi (Nakit/Kredi Kartı)

- **DirectSalesViewModel.cs**: POS iş mantığı
  - Anlık ürün arama (Ad/Model/SKU)
  - Sepet yönetimi (ekle/çıkar/miktar değiştir)
  - Nakit ve Kredi Kartı ödeme işleme
  - StockTransaction (Sale) kaydı

- **DirectSalesWindow.xaml**: Bölünmüş ekran arayüzü
  - Sol Panel (%60): Ürün Kataloğu + Arama
  - Sağ Panel (%40): Sepet + Ödeme Butonları
  - Büyük tıklanabilir satırlar (RowHeight=45)

- **Navigation Entegrasyonu:**
  - `MainContentViewModel.cs`: `OpenDirectSalesCommand` eklendi
  - `MainContentView.xaml`: "🛒 HIZLI SATIŞ (KASA)" yeşil buton eklendi

---

## 2026-01-14

### ✅ Tamamlanan

#### Servis İş Emri UI Yeniden Tasarımı (Single-Page Form)
4 adımlı Wizard kaldırıldı, tek sayfalık form + çoklu kategori desteği eklendi.

- **NewServiceJobWindow.xaml**: ~510 satır → ~320 satır (JobDetail template'ları kaldırıldı)
- **ServiceJobViewModel.cs**: Wizard mantığı kaldırıldı, CategoryItems eklendi
- **ServiceJob.cs**: `CategoriesJson` alanı eklendi (çoklu kategori JSON)
- **CategorySelectItem.cs**: Checkbox binding için yeni model
- **Yeni Özellikler:**
  - Çoklu kategori seçimi (CheckBox'larla)
  - Yapı Türü seçimi (Müstakil, Apartman, Site, İşyeri)
  - "Tüm Birimlere Uygula" checkbox (malzeme çarpanı)
  - Büyük açıklama kutusu (detay formlarının yerini aldı)
  - 2 sütunlu layout: Sol (İş Bilgileri) / Sağ (Malzeme)
- **Migration**: `AddMultiCategorySupport`

---

## 2026-01-16

### ✅ Tamamlanan

#### Proje Editörü Ağaç Yönetimi İyileştirmeleri (Usability)
- **Sibling Addition (Kardeş Ekleme):** "Daire Ekle" komutu artık bir daire seçiliyken de çalışıyor (Kardeş olarak ekler).
- **Gelişmiş Yeniden Adlandırma:** "Yeniden Adlandır / Etiketle" özelliği ile birimlere özel müşteri isimleri atanabilir (örn. 'Daire 5 - Ahmet Bey').
- **UI UX:** Sağ tık menüsünde isimlendirme başlığı güncellendi.
- **Bug Fix:** TreeView'da isim değişikliğinin anlık yansımaması sorunu (`ScopeNode.HeaderDisplay` binding notification) düzeltildi.
- **Bug Fix:** Node silme işleminde `Parent` node'un toplamlarının güncellenmemesi sorunu (`NotifyTotalsChanged`) giderildi.
- **Logic Update:** `AddFlat` mantığı güncellenerek daire seçiliyken kardeş olarak ekleme (Parent üzerinden) işlemi garanti altına alındı.
- **Critical Bug Fix:** `ScopeNode.Children` koleksiyonu `ObservableCollection` tipine dönüştürüldü. Bu sayede TreeView'a yeni eklenen veya silinen node'lar anında arayüze yansıyor.

#### Profesyonel Tamir & Servis Modülü (Yeni)
- **Database:** `ServiceJob` entity'sine cihaz marka/model/seri no, aksesuar ve durum alanları eklendi.
- **Workflow:** `RepairStatus` enum ile 10 adımlı tamir takip süreci tanımlandı.
- **History:** `ServiceJobHistory` tablosu ile her tamir adımı, teknisyen notu ve durum değişikliği loglanıyor.
- **UI:** 
  - `RepairRegistrationWindow`: Detaylı cihaz kabul ekranı.
  - `RepairTrackingWindow`: Master-Detail yapısında aktif arıza takip ve işlem merkezi.
  - **Logic:** `RepairViewModel` ile durum makinesi (State Machine) yönetimi ve history entegrasyonu sağlandı.

#### Genel İyileştirmeler
- **Login Ekranı:** Geçici olarak devre dışı bırakılan giriş ekranı tekrar aktif edildi. Uygulama açılışında artık kullanıcı girişi zorunlu.


---

## 2026-01-12

### ✅ Tamamlanan

#### Profesyonel Mühendislik Tezgahı (Enterprise Quote Editor)
Mevcut basit 'Keşif & Teklif' modülü tamamen yeniden yazıldı.

- **Yeni Modeller:**
  - `Models/ScopeNode.cs` - Recursive, JSON-serializable tree node (Proje > Blok > Kat > Daire)
  - `Models/ScopeNodeItem.cs` - Finansal derinlikli kalem (UnitCost, UnitPrice, LaborCost, MarginPercent, IsOptional)
  
- **ServiceProject Güncellemeleri:**
  - `ProjectScopeJson` - Hiyerarşik yapı ağacı (JSON)
  - `TotalCost` - Toplam maliyet
  - `TotalProfit` - Toplam kar

- **Yeni Servis:**
  - `Services/ProjectScopeService.cs` - JSON serialize/deserialize, Save/Load operations

- **Yeni ViewModel:**
  - `ViewModels/ProjectQuoteEditorViewModel.cs` - Üç panelli workbench mantığı
  - Tree yönetimi: AddBlock, AddFloor, AddFlat, DuplicateNode, RemoveNode
  - Drag & Drop: IDropTarget implementasyonu (gong-wpf-dragdrop)
  - Context menü: Rename, Apply to All Siblings
  - Finansal hesaplamalar: Real-time Maliyet/Kar/Marj

- **Yeni View:**
  - `Views/ProjectQuoteEditorWindow.xaml` - Üç Panelli Komuta Merkezi
  - Sol Panel: Yapı Ağacı (TreeView + live cost badges)
  - Orta Panel: Mahal Listesi (DataGrid - inline editing)
  - Sağ Panel: Ürün & Hizmet Kataloğu (Drag source)
  - Alt Panel: Finansal özet (Maliyet/Satış/Kar/Marj)

- **Bağımlılık:**
  - `gong-wpf-dragdrop` v3.2.1 NuGet paketi eklendi

---

## 2026-01-09

### ✅ Tamamlanan

#### Kurumsal Logo Entegrasyonu
- **Yeni Klasör:** `Assets/Images/` - Görsel dosyalar için
- **Yeni Dosya:** `KamatekLogo.jpg` - Dağ + KAMATEKCRM logosu
- **LoginView.xaml:** Emoji (🏢) yerine logo görseli eklendi
- **MainContentView.xaml:** Sidebar başlığındaki text yerine logo eklendi
- **KamatekCrm.csproj:** Resource tanımı eklendi

#### Proje Ekranları Birleştirme (UI Simplification)
- **Silinen View'lar:**
  - `Views/ProjectWorkflowWindow.xaml` (5 fazlı proje akışı)
  - `Views/DiscoveryQuoteWindow.xaml` (4 adımlı keşif sihirbazı)
  - `Views/ProjectEditorWindow.xaml` (3 panelli workbench)
  
- **Silinen ViewModel'ler:**
  - `ViewModels/ProjectWorkflowViewModel.cs`
  - `ViewModels/DiscoveryQuoteViewModel.cs`
  - `ViewModels/ProjectEditorViewModel.cs`

- **Yeni Basit Arayüz:**
  - `Views/ProjectQuoteWindow.xaml` - TabControl ile 2 sekmeli basit pencere
  - `ViewModels/ProjectQuoteViewModel.cs` - Çarpan mantığı ile teklif oluşturma

- **Özellikler:**
  - Tab 1: Keşif & Yapı (Müşteri seçimi, Proje adı, Yapı tanımı)
  - Tab 2: Teklif Hazırla (Ürün kataloğu, Teklif kalemleri DataGrid)
  - Otomatik hesaplama: Blok × Kat × Daire = Toplam Birim
  - Çarpan mantığı: Birim başına adet × Toplam birim = Toplam miktar

- **MainContentView Güncellemesi:**
  - 3 eski buton kaldırıldı (Proje Akışı, Keşif & Teklif, Proje Editörü)
  - 1 yeni buton eklendi: "🏗️ PROJE & TEKLİF" (mavi vurgulu)

---

## 2025-01-05

### ✅ Tamamlanan

#### Proje Editörü (3 Panelli Workbench)
- **Yeni Enum:** `Enums/NodeType.cs` - Tree node tipleri (Project, Block, Floor, Flat, Zone)
- **Yeni Modeller:**
  - `Models/StructureTreeItem.cs` - Recursive tree node yapısı
  - `Models/ScopeItem.cs` - Mahal kalemi (ürün ataması)
- **Yeni Servis:** `Services/StructureGeneratorService.cs` - Otomatik yapı oluşturucu
- **Yeni ViewModel:** `ViewModels/ProjectEditorViewModel.cs` - 3 panel workbench mantığı
- **Yeni View:** `Views/ProjectEditorWindow.xaml` - Yapı Ağacı + Mahal Listesi + Ürün Kataloğu

**Özellikler:**
- TreeView ile hiyerarşik yapı görünümü
- Node bazlı ürün ataması (Scope)
- Smart Propagation: İçeriği kopyala → Tümüne yapıştır
- Recursive maliyet hesaplama

---

#### Keşif ve Fiyat Teklifi Modülü (Discovery & Quote Manager)
- **Yeni Enum'lar:**
  - `Enums/StructureType.cs` - Yapı tipi (SingleUnit, Apartment, Site, Commercial)
  - `Enums/UnitType.cs` - Birim tipi (Block, Flat, Entrance, Zone, CommonArea)
  - `Enums/PredefinedZone.cs` - Önceden tanımlı fabrika bölgeleri

- **Yeni In-Memory Modeller:**
  - `Models/ProjectUnit.cs` - Oluşturulan birimler (Daire, Blok girişi, vb.)
  - `Models/StructureDefinition.cs` - Yapı tanımı (JSON serialize)
  - `Models/QuoteLineItem.cs` - Teklif kalemleri

- **ServiceProject Güncellemeleri:**
  - `StructureType` - Yapı tipi alanı
  - `StructureDefinitionJson` - Yapı tanımı (JSON)
  - `TotalUnitCount` - Toplam birim sayısı
  - `QuoteItemsJson` - Teklif kalemleri (JSON)
  - `DiscountPercent` - Proje iskontosu

- **Yeni ViewModel & View:**
  - `ViewModels/DiscoveryQuoteViewModel.cs` - 4 adımlı sihirbaz mantığı
  - `Views/DiscoveryQuoteWindow.xaml` - Yapı Sihirbazı UI

- **4 Adımlı Akış:**
  1. Proje & Müşteri Bilgileri
  2. Yapı Sihirbazı (Apartman/Site/Fabrika)
  3. Sistem Seçimi (Toplu ürün atama)
  4. Finansal Özet (İskonto, toplam)

---

## 2025-01-03

### ✅ Tamamlanan

#### Gelişmiş Servis Yaşam Döngüsü Mimarisi
- **Yeni Enum'lar:**
  - `Enums/WorkflowStatus.cs` - 9 farklı proje durumu (Draft → Completed)
  - `Enums/ServiceJobType.cs` - Fault (Arıza) / Project (Proje) ayrımı

- **ServiceJob Entity Güncellemeleri:**
  - `ServiceJobType` - Arıza vs Proje ayrımı
  - `WorkflowStatus` - 5 fazlı proje yaşam döngüsü
  - `IsStockReserved` / `IsStockDeducted` - Stok takibi
  - `ProposalSentDate`, `ApprovalDate`, `ProposalNotes` - Teklif alanları

- **Arıza & Servis Ekranı:**
  - `ViewModels/FaultTicketViewModel.cs` - Hibrit cihaz seçici mantığı
  - `Views/FaultTicketWindow.xaml` - Hızlı arıza kaydı formu
  - Mevcut cihaz veya yeni cihaz kaydı desteği
  - Maliyet tahmini bölümü

- **Proje & Kurulum Ekranı (5 Fazlı):**
  - `ViewModels/ProjectWorkflowViewModel.cs` - Keşif → Teklif → Onay → Uygulama → Final
  - `Views/ProjectWorkflowWindow.xaml` - Stepper UI ile faz navigasyonu
  - Stok rezervasyonu ve final düzeltme mantığı
  - `FinalAdjustmentItem` sınıfı - Tahmini vs Gerçek karşılaştırma

- **Navigasyon Entegrasyonu:**
  - `MainContentViewModel.cs` - `OpenFaultTicketCommand`, `OpenProjectWorkflowCommand` eklendi
  - `MainContentView.xaml` - "HIZLI ERİŞİM" bölümü ile butonlar eklendi

- **Bug Fixes:**
  - `IsExistingAsset` readonly property binding hatası düzeltildi (Mode=OneWay)
  - Komutlar yanlış ViewModel'e (`MainViewModel`) eklenmişti, `MainContentViewModel`'e taşındı

---

## 2025-12-26

### ✅ Tamamlanan

#### Kullanıcı Girişi ve Yetkilendirme (Login & RBAC)
- `Models/User.cs` entity modeli oluşturuldu (Ad, Soyad, Username, PasswordHash, Role)
- `Services/AuthService.cs` oluşturuldu (Login, Logout, SHA256 hashing)
- Varsayılan admin kullanıcısı: **admin.user / 1234**
- `Views/LoginView.xaml` modern kart tasarımı ile giriş ekranı
- `LoginViewModel.cs` ile MVVM pattern uygulandı
- `App.xaml.cs` başlangıç mantığı güncellendi (Login önce açılır)
- `AppDbContext.cs`'e `Users` DbSet eklendi

#### Kullanıcı Yönetimi
- `UsersViewModel.cs` ve `UsersView.xaml` oluşturuldu
- Kullanıcı listesi DataGrid (Ad Soyad, Username, Rol, Durum, Son Giriş)
- Arama/filtreleme özelliği
- Yeni kullanıcı ekleme (`AddUserView`)
- Kullanıcı silme (kendini silemez)
- Şifre sıfırlama (1234)
- **Rol Gösterimi:** Admin → "Patron", Technician → "Personel"

#### MainWindow Güncellemeleri
- Sol panele "👤 Kullanıcılar" butonu eklendi (Sadece Admin)
- Alt panele kullanıcı bilgisi kartı eklendi (Ad Soyad + Rol)
- "🚪 Çıkış Yap" butonu eklendi
- Rol tabanlı görünürlük (Admin bölümü sadece Patron'a görünür)

---


## 2025-12-25

### ✅ Tamamlanan

#### Dashboard Ana Sayfa Modülü
- `DashboardViewModel.cs` oluşturuldu (KPI sayaçları + veri koleksiyonları)
- `DashboardView.xaml` profesyonel grid layout ile tasarlandı
- 4 KPI Kartı: Aktif İşler, Kritik Stok, Bu Ay İşler, Toplam Müşteri
- Acil İşler DataGrid (Urgent/Critical priority)
- Son Stok Hareketleri listesi (Son 10)
- Kritik Stoklar uyarı listesi (Kırmızı)
- Yeni Müşteriler listesi (Son 5)
- MainWindow'a "🏠 Ana Sayfa" navigasyon butonu eklendi
- Uygulama açılışında Dashboard varsayılan sayfa olarak ayarlandı

#### PDF Raporlama Modülü
- QuestPDF kütüphanesi eklendi (v2025.12.0)
- `Services/PdfService.cs` oluşturuldu
- Profesyonel Servis Formu PDF tasarımı:
  - Header: Şirket bilgisi, İş ID, Tarih
  - Müşteri ve İş detayları
  - Kullanılan malzemeler tablosu
  - Maliyet özeti
  - İmza alanları (Teknisyen + Müşteri)
  - Garanti / sorumluluk notu
- `ServiceJobViewModel`'e `PrintServiceFormCommand` eklendi
- `ServiceJobsView.xaml`'e 🖨️ PDF Yazdır butonu eklendi
- SaveFileDialog ile kaydetme ve otomatik açma

---

## 2025-12-23

### ✅ Tamamlanan

#### ServiceJobsView Master List Tasarımı
- `ServiceJobsView.xaml` profesyonel DataGrid listesine dönüştürüldü
- Arama, tarih filtresi, durum filtresi eklendi
- Status badge'leri (renkli) ve priority ikonları eklendi
- Wizard UI ayrı pencereye (`NewServiceJobWindow.xaml`) taşındı

#### CustomerDetailView Tab Yapısı
- 4 tab eklendi: Genel Bilgiler, Aktif İşler, Servis Geçmişi, Finansal
- `ActiveJobs` ve `PastJobs` koleksiyonları eklendi
- Aktif işler (Status != Completed) ayrı tab'da gösteriliyor

#### Excel Import Stok Düzeltmesi
- `ProductViewModel.ImportFromExcel()` güncellendi
- Import sırasında `Inventory` ve `StockTransaction` kayıtları oluşturuluyor
- Ana Depo otomatik bulunuyor/oluşturuluyor

---

## Önceki Değişiklikler

### ✅ Tamamlanan
- WebView2 Map Fix (Async initialization)
- Product Excel Import (Auto-Inventory Creation)
- Add Product UI (Editable Unit + Initial Stock field)
- Navigation Buttons: Stock Count & Reports
- Customer Type: Individual/Corporate selection
- Dynamic Job Details: 8 category support

### 🔄 Planlanan
- Raporlama modülleri geliştirme
- Dashboard ekranı
- PDF export özelliği
