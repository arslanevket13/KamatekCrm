# KamatekCRM — Otonom MCP Kalite, Hata ve UX Denetim Raporu

- **Denetim Tarihi:** 2026-11-25 / 2026-11-26
- **Denetim Altyapısı:** `KamatekCrm.McpServer` (Stdio JSON-RPC 2.0) -> `127.0.0.1:19876` (`#if DEBUG` `DebugAutomationBridge` & `DebugDiagnosticsCollector`)
- **Oturum Kimliği:** `#if DEBUG` `DevQuickLoginBtn` (`Şevket arslan / Patron`)
- **Kanıt Dizini (29 Ekran Görüntüsü):** `C:\Users\Admin\AppData\Local\Temp\kamatek_mcp_shots\` (`01_dashboard_light.png` .. `17_quick_add_modal_light.png`)
- **Veritabanı Temizliği:** `0` kalıcı test kaydı (tüm `[MCP-TEST]` denemeleri salt arama/iptal veya şema hatası nedeniyle geri alındı)

---

## 1. Özet Skor Tablosu

| Metrik | Sonuç |
| --- | --- |
| **Taranan Ana Modül Sayısı** | **13 / 13** (`Dashboard`, `Customers`, `Interactions`, `ManagerAgenda`, `ServiceJobs`, `Repairs`, `Quotations`, `DirectSales`, `Stock`, `Finance`, `Analytics`, `Users`, `Settings`) |
| **Çift Tema Görsel Taraması** | **26 / 26** (`13 × PremiumLight` + `13 × MidnightDark`) + **3 E2E Ekran Görüntüsü** |
| **Tamamlanan E2E Senaryo Sayısı** | **5 / 5** (`Customers`, `Interactions -> ManagerAgenda`, `ServiceJobs & Quotations`, `DirectSales POS`, `Klavye & Navigasyon Kabuğu`) |
| **Yakalanan WPF Binding Hatası** | **1 benzersiz kırık binding** (`DrawerCallerContext.MatchedPhone` — her telefon girişinde tekrarlanıyor) |
| **Yakalanan Serilog `Error` Sayısı** | **3 benzersiz kök hata** (her oturumda ve ekran geçişinde tekrarlanıyor; toplam **8+** örnek yakalandı) |
| **Yakalanan Serilog `Warning` Sayısı** | **2 benzersiz uyarı** (`ConnectionHeartbeatService` geliştirici bypass + `DEV-ONLY quick admin login`) |
| **Sonsuz `IsBusy == true` Kilitlenmesi** | **0** *(Not: Visual Studio `F5` ilk şans istisna duraklatması açıkken yakalanan `PostgresException`'lar debugger'da duraksamaya yol açmaktadır; bağımsız Debug çalışmasında `isBusy` takılması yoktur)* |
| **Toplam Bulgu Dağılımı (`P0 / P1 / P2`)** | **4 P0 · 8 P1 · 12 P2 (Toplam 24 Kanıtlı Bulgu)** |

---

## 2. P0 — Kritik Hatalar (Serilog Error, Veritabanı Şema Kırılması, Kırık WPF Binding, Ham SQL İfşası)

| Kod | Ekran | Tema | Kanıt / Tam Hata Metni | Yeniden Üretme Adımı |
| --- | --- | --- | --- | --- |
| **P0-1** | `Login` (Açılış Bootstrap) | Tümü (`PremiumLight` / `Glassmorphism`) | **Serilog `Error` (x2):** `Veritabanı otomatik kurulum veya şema güncelleme hatası.` -> `PostgresException: 42701: column "IsOnDuty" of relation "Users" already exists` ve ardından `Exception: Veritabanı güncellenirken bir sorun oluştu. Sistem bakım modunda.` | Uygulamayı mevcut yerel PostgreSQL (`Port: 5439`) veritabanıyla başlatın; idempotent olmayan migration `Users.IsOnDuty` sütununda çakışarak sonraki tüm şema güncellemelerini durdurur. |
| **P0-2** | `Interactions` (`CustomerInteractionsView`) | Tümü | **Serilog `Error`:** `Takvim gün sayıları yüklenirken hata oluştu.` -> `PostgresException: 42703: column c2.BallInCourt does not exist (POSITION: 299)`. Ekranda kullanıcıya hiçbir hata gösterilmeden tüm klasör sayaçları `0` ve liste `"Kayıt bulunamadı"` döner (`03_interactions_light.png`). | `navigate_and_theme(route: "Interactions")` çağırın; `P0-1` yüzünden uygulanamayan `BallInCourt` sütunu tüm takvim/klasör sorgularını düşürür. |
| **P0-3** | `Interactions` -> `"Vaka Kaydını Aç"` (`e76`) | `PremiumLight` | **Kayıt Başarısızlığı + Ham SQL Toast İfşası:** Yeni vaka formu doldurulup `"Vaka Kaydını Aç"` tıklandığında kayıt veritabanına yazılamaz ve üst sağ köşede son kullanıcıya ham veritabanı istisnası gösterilir: **`42703: column c.BallInCourt does not exist / POSITION: 62 / Hata`** (`15_service_job_workspace_light.png`). Visual Studio debugger bağlıysa süreç exception üzerinde donar; `ManagerAgenda` ekranına hiçbir kayıt yansımaz. | `Interactions` -> `+ Yeni Kayıt (Ctrl+N)` -> Telefon `05550009988`, İsim `[MCP-TEST] Deneme Firma`, Konu `[MCP-TEST] Fiyat Teklifi Talebi` girip `"Vaka Kaydını Aç"` butonuna tıklayın. |
| **P0-4** | `Interactions` (Sağ Kayıt Çekmecesi) | Tümü | **WPF DataBinding `Error 40`:** `BindingExpression path error: 'MatchedPhone' property not found on 'object' ''CallerContextDto''. BindingExpression:Path=DrawerCallerContext.MatchedPhone; DataItem='CustomerInteractionsViewModel'; target element is 'TextBlock'; target property is 'Text'` (`14_interactions_submit_light.png`). | `+ Yeni Kayıt` çekmecesinde telefon kutusuna (`e63`) numara yazın; açılan arayan bilgi kartının sol üst başlığı kırık binding nedeniyle boş kalır. |

---

## 3. P1 — Çalışmayan / Eksik Özellikler, Kısayol Kırıkları ve Ölü/ Korumasız Kontroller

| Kod | Ekran | Kanıtlı Bulgu ve Davranış |
| --- | --- | --- |
| **P1-1** | `DirectSales` (`DirectSalesWindow` — POS) | **Boş Sepetle Ödeme Butonları Aktif:** Sepet tamamen boşken (`rows:0`, `GENEL TOPLAM: 0,00 ₺`) `💵 NAKİT (F8)` (`e35`), `💳 KART (F9)` (`e36`), `📋 CARİ (F10)` (`e37`), `📌 Park Et (F6)` (`e38`) ve `🗑️ Temizle (F5)` (`e39`) butonlarının tamamı `isEnabled: true` durumdadır (`08_direct_sales_light.png`). Yalnızca `Fiş Bas (F11)` pasiftir. |
| **P1-2** | `Quotations` (`QuoteListView`) | **Seçimsiz / Boş Tabloda Alt Aksiyon Butonları Aktif:** `QuotesDataGrid` içinde `rows:0, selected:-1` (`"0 teklif yüklendi."`) olmasına rağmen `Düzenle` (`e50`), `PDF` (`e51`), `Kopyala` (`e52`), `İş Emri` (`e53`) ve kırmızı `Sil` (`e54`) butonlarının tamamı `isEnabled: true` bırakılmıştır (`07_quotations_light.png`). Boş tabloda `Düzenle` tıklandığında hiçbir işlem veya bilgilendirme oluşmaz. |
| **P1-3** | Kabuk Üst Bar (`MainWindow`) | **`Alt+Left` Geri Kısayolu Çalışmıyor:** Üst bardaki geri butonunun ipucu `"Geri (Alt+Sol Arrow)"` (`e27`) olarak ilan edilmesine rağmen `Customers -> Stock` geçişi sonrasında `press_key("Alt+Left")` komutu `MainWindow.InputBindings` içinde karşılık bulmamakta ve sayfa `Product` (`Stock`) ekranında kalmaktadır (`Ctrl+K` ve `F5` `KeyBinding` olarak çalışırken `Alt+Left` bağlanmamıştır). |
| **P1-4** | `Interactions` (`CustomerInteractionsView`) | **`Ctrl+N` Kısayolu Odak Dışında Çalışmıyor:** Buton metni `"+ Yeni Kayıt (Ctrl+N)"` (`e40`) olmasına rağmen sayfaya ilk girildiğinde klavye odağı `CustomerInteractionsView` içinde olmadığından `Ctrl+N` çekmeceyi açmamaktadır. |
| **P1-5** | `Interactions` (Yeni Vaka Çekmecesi) | **Zorunlu Alan Validasyon Geri Bildirimi Eksik:** `"Görüşme / Talep Konusu"` (`e72`) boş bırakılıp `"Vaka Kaydını Aç"` (`e76`, boşken bile `isEnabled: true`) tıklandığında formda ne kırmızı validasyon çerçevesi ne de alan altı hata mesajı gösterilmektedir (`14_interactions_submit_light.png`). |
| **P1-6** | `Customers` (`CustomersView`) | **Sayfalama (`<` / `>`) Butonları Tek Sayfada Aktif:** Toplam 1 müşteri (`Sayfa 1 / 1`) ve arama sonucu `0` müşteri olduğunda dahi önceki `<` (`e48`) ve sonraki `>` (`e49`) sayfa butonları `isEnabled: true` kalmaktadır (`02_customers_light.png`). |
| **P1-7** | `Users` (`UsersView`) | **Boş Durum Sütunu, Filtre Taşması ve Veri Hatası:** (a) Tabloda `"Durum"` sütunu tamamen boştur (`Aktif/Pasif` rozeti yok); (b) `get_ui_tree` içinde var olan rol/durum `ComboBox`'ları (`e40`, `e41`) ve `Filtrele`/`Temizle` butonları (`e42`, `e43`) orta filtre kutusuna sığmayıp sağdan tamamen kırpılmıştır; (c) KPI kartı `"Aktif Personel: 0"` gösterirken listede aktif yönetici vardır ve alt unvanda `"Yönrtici"` yazım hatası mevcuttur (`12_users_light.png`). |
| **P1-8** | `Repairs` (`RepairListView`) & `Dashboard` | **Varsayılan Filtre ve Boş Durum Eksikliği:** `Repairs` açılışında `"📋 Tümü"` (`e37`) dahil 10 durum `RadioButton` filtresinin tamamı `isChecked: false` gelir ve sol liste boşken hiçbir `Empty State` mesajı göstermez (`06_repairs_light.png`). `Dashboard` ekranında ise stoklar normalken (`"Stoklar Yeterli"`) kart başlığı kırmızı tehlike bandıyla `"⚠️ Kritik Stok Uyarısı"` göstermektedir (`01_dashboard_light.png`). |

---

## 4. P2 — Görsel, Kontrast ve UX Geliştirme Önerileri

### 4.1 Çift Tema Kontrast, Kırık İkon ve Yerleşim Kusurları

| Kod | Ekran | Tema | Görsel Kusur & Kanıt Dosyası |
| --- | --- | --- | --- |
| **P2-1** | `Settings` | `MidnightDark` | **Koyu Zeminde Siyah Form Etiketleri (Okunamaz):** Tüm form alan başlıkları (`Firma Unvanı`, `Alt Başlık`, `Şube / Bölge`, `Vergi Dairesi`, `Vergi / TC No`, `Telefon Numarası`, `E-Posta Adresi`, `Firma Logosu`) koyu kart zemini (`#111827`) üzerinde siyah metin olarak kalmıştır (`13_settings_dark.png`). |
| **P2-2** | `Customers`, `Interactions`, `ServiceJobs`, `Quotations`, `Stock`, `Analytics`, `Users`, `Settings` | `MidnightDark` | **Patlayan Açık Gri Input ve Buton Yamaları:** Koyu temada arama/form `TextBox` kontrolleri ve ikincil butonlar (`Tümü/Bireysel/Kurumsal`, `Yenile`, `Excel'den Yükle`, `Düzenle/PDF/Kopyala`, `Rapor İndir`) `#C8CCD0` açık gri dolgulu kalmaktadır (`02_customers_dark.png`, `07_quotations_dark.png`, `13_settings_dark.png`). |
| **P2-3** | `Customers` & `Analytics` | `MidnightDark` | **Koyu Zeminde Siyah Sayfalama ve Grafik Metinleri:** `Customers` tablosu sağ altındaki `1 / 1` sayfa numarası (`02_customers_dark.png`) ile `Analytics` halka grafiğindeki `1 (%100,0)` etiketi ve `10 / 5 / 0` eksen sayıları (`11_analytics_dark.png`) koyu zeminde siyah/koyu gri kalarak kaybolmaktadır. |
| **P2-4** | `DirectSales` (`POS`) | `PremiumLight` & `MidnightDark` | **POS Görsel ve Başlık Çubuğu Kusurları:** (a) Pencere üst başlık çubuğu görünmez/kesik durumdadır; (b) `PremiumLight`'ta koyu yeşil `NAKİT (F8)` butonu üzerinde siyah metin/ikon düşük kontrastlıdır; (c) `MidnightDark`'ta sol ve sağ panel arasındaki dikey boşluk bembeyaz parlamaktadır; (d) `Ekle (Enter) →` hap butonunun solundaki `"E"` harfi ve sepet `"İnd.%"` başlığı kesilmektedir (`08_direct_sales_light.png`, `08_direct_sales_dark.png`). |
| **P2-5** | `Dashboard`, `Interactions`, `ManagerAgenda`, `Finance` | Her İki Tema | **Bozuk İkon / Glif Karakterleri (4 Ekran):** (a) `Dashboard` `"Bugünkü Satış"` mor kutusunda kırık tofu ikonu `▯` (`01_dashboard_light.png`); (b) `Interactions` sol menüde `"Ň Patronda"` (`\u0147\u000F`) bozuk karakteri (`03_interactions_light.png`); (c) `ManagerAgenda` `"Müdahale Paneli"` üstünde ikon yerine tek başına virgül `,` (`04_manager_agenda_light.png`); (d) `Finance` tarih `<`/`>` butonlarının içi tamamen boş ve sağ alt yenile butonu `(` şeklinde yarım kesik (`10_finance_light.png`). |
| **P2-6** | `Customers`, `ServiceJobs`, `Quotations`, `Settings` | Her İki Tema | **1600x938 Çözünürlükte Sütun ve Etiket Kesilmeleri:** `Customers`'ta `"Bireys"`, `"054426608"`, `"Toplam Ha"` ve ham iç kod `SJ607a78a2e1584136` (`02_customers_light.png`); `ServiceJobs`'ta `"Keşi"`, `"Keşif Ta"`, `"Nor"`, `"Teknisye"`, `"Planlana"`, `"Aksiy"` (`05_service_jobs_light.png`); `Quotations`'ta `"İskont"` (`07_quotations_light.png`); `Settings`'te `"Uygulama Güncelleme"`, `"Vergi / TC Nc"`, `"Ticaret Sicil M"` ve alt `"Banka & IBAN Hesapları"` başlığı (`13_settings_light.png`) kesilmektedir. |
| **P2-7** | `QuickAddModal` (`Ctrl+K`) | `PremiumLight` | **Modal İçerik Kesilmesi ve Çakışma:** `17_quick_add_modal_light.png` üzerinde 5. satır (`Ana Sayfa (Dashboard)` + `SAYFA` rozeti) alt bilgi barı tarafından ortadan kesilmekte, seçili ilk kartın sağ mavi çerçevesi `ScrollBar` ile üst üste binmektedir. |
| **P2-8** | `Users` | Her İki Tema | **KPI Başlıklarında Bulanık Render (Blur):** `"Toplam Kullanıcı"`, `"Aktif Personel"` ve `"Yöneticiler"` kart başlıkları ebeveyn gölge/efekt katmanı nedeniyle bulanık görünmektedir (`12_users_light.png`). |
| **P2-9** | `Finance` & Üst Breadcrumb | Her İki Tema | **Dikey ve Yatay Kırpılma:** `Finance` boş durum alt açıklama satırı alttan kesilmekte (`10_finance_light.png`), üst bar breadcrumb metni `"Müşteri İletişim ve Tal"` noktasında `TextTrimming="CharacterEllipsis"` olmadan kesilmektedir (`03_interactions_light.png`). |
| **P2-10** | Sol Menü (`Sidebar`) | Her İki Tema | **10 Ana Modülün İlk Bakışta Gizli Kalması:** `1600x938` tam pencerede dahi `Proje & Teklif` altındaki 10 modül (`Hızlı Satış`, `Finans`, `Analitik`, `Satın Alma`, `Tedarikçiler`, `Finansal Rapor`, `Stok Sayım`, `Standart Raporlar`, `Kullanıcılar`, `Loglar`) kaydırma altında gizli kalmaktadır (`01_dashboard_light.png`, `16_sidebar_collapsed_light.png`). |
| **P2-11** | Ana Pencere Çerçevesi & Hata Toast | `MidnightDark` / `PremiumLight` | **Pencere Gölge Şeridi ve Toast İkon Kontrastı:** `MidnightDark`'ta ana pencerenin sol/alt dış gölge boşluğu beyaz şerit bırakmakta (`01_dashboard_dark.png`); hata toast bildiriminde sol çarpı ikonu koyu bordo zemin üzerinde siyah kalarak okunmamaktadır (`15_service_job_workspace_light.png`). |
| **P2-12** | `ServiceJobs` & `Customers` | `PremiumLight` | **Etiketsiz Tarih Seçiciler ve Seçili Filtre Vurgusu Eksikliği:** `ServiceJobs` ekranındaki iki `DatePicker` kutusunda `Başlangıç / Bitiş` etiketi yoktur (`05_service_jobs_light.png`); `Customers` ekranındaki `Tümü / Bireysel / Kurumsal / Perakende` butonlarında aktif filtreyi gösteren renk vurgusu bulunmamaktadır (`02_customers_light.png`). |

---

### 4.2 Kullanıcıyı Hızlandıracak 5 Somut UX Geliştirme Önerisi

1. **Global Klavye Kısayollarını `MainWindow.InputBindings` Üzerinde Merkezileştirin:** İpucu metinlerinde vaat edilen `Alt+Left` (Geri), `Alt+Right` (İleri), `Ctrl+B` (Menü Daralt) ve aktif görünümün `Ctrl+N` (Yeni Kayıt) komutlarını doğrudan `MainWindow` düzeyinde bağlayarak odak bağımlılığını ortadan kaldırın.
2. **Tablo ve POS Aksiyonlarına `CanExecute` Koruması Ekleyin:** `Quotations` alt aksiyonlarını (`Düzenle`, `PDF`, `Kopyala`, `İş Emri`, `Sil`), `Customers` sayfalama (`<`/`>`) butonlarını ve `DirectSales` ödeme (`NAKİT`/`KART`/`CARİ`) butonlarını satır seçimi / sepet doluluğu koşuluna (`CanExecute`) bağlayın.
3. **Kullanıcı Dostu Hata Katmanı ve Satır İçi Form Validasyonu Kurun:** Ham `PostgresException` metinlerini toast üzerinden son kullanıcıya göstermek yerine korelasyon kodlu anlaşılır Türkçe mesaj gösterin; `Interactions` çekmecesinde zorunlu alanları (`Konu`, `Telefon`) anlık satır içi uyarıyla işaretleyin.
4. **Sol Menüyü Daraltılabilir Kategori Grupları veya Kompakt Yoğunlukla Yeniden Düzenleyin:** `1600x938` ekranda 10 modülün kaydırma altında kaybolmasını önlemek için menü öğesi dikey boşluklarını sıkılaştırın veya `OPERASYON / SATIŞ / FİNANS / YÖNETİM` akordeon grupları kullanın.
5. **Merkezi Tema Token Denetimi ve Kalıcı `AutomationId` Standartlaştırması Yapın:** `MidnightDark` temasındaki tüm sabit renkli (`Foreground="#0F172A"`, açık gri `TextBox`/`Button`) kontrolleri 3 tema sözlüğündeki ortak `DynamicResource` anahtarlarına bağlayın ve kritik form/butonlara sabit `AutomationProperties.AutomationId` tanımlayın.

---

## 5. Etkileşimli Uçtan Uca (E2E) Senaryo Sonuç Özeti

1. **Müşteri & Arama (`Customers`):** `[MCP-TEST]` araması listeyi anında `0` satıra ve `"Müşteri Bulunamadı"` durumuna düşürdü; filtre çipleri çalıştı; `"Yeni Müşteri Ekle"` (`CustomerAddWindow`) açıldı, boş formda `"Müşteriyi Kaydet"` güvenli biçimde pasif (`isEnabled: false`) kaldı ve `Esc` ile hatasız kapandı (`0` binding/Serilog hatası).
2. **İletişim & Takvim Defteri (`Interactions -> ManagerAgenda`):** `Ctrl+N` odak dışında tepki vermedi (`P1-4`), butonla açılan çekmecede telefon girişi `CallerContextDto.MatchedPhone` binding hatası üretti (`P0-4`), boş konuyla kayıt sessizce reddedildi (`P1-5`), tam dolu `[MCP-TEST]` kaydı ise eksik `BallInCourt` sütunu nedeniyle başarısız olup `ManagerAgenda` ekranına yansımadı (`P0-3`).
3. **İş Emri & Teklif (`ServiceJobs & Quotations`):** `ServiceJobs` satırındaki `"Aç"` butonu sağ detay özetini (`İŞ ID: #1 — Keşif Talebi`) `IsBusy` kilitlenmesi olmadan açtı (`15_service_job_workspace_light.png`); `Quotations` ekranında boş tabloda aktif bırakılan `"Düzenle"` butonu (`P1-2`) çökme üretmeden sessizce boşa düştü.
4. **POS / Hızlı Satış (`DirectSales`):** Barkod girişi ve `F5` (`Temizle`) `KeyBinding` kısayolu hatasız çalıştı; ancak boş sepette `NAKİT (F8)`, `KART (F9)` ve `CARİ (F10)` butonlarının aktif kaldığı (`P1-1`) doğrulandı.
5. **Klavye & Navigasyon Kabuğu:** `Ctrl+K` (`QuickAddModal`) ve modal içi `Esc` `KeyBinding` kısayolları ile sol menü daraltma/genişletme (`16_sidebar_collapsed_light.png`, `17_quick_add_modal_light.png`) `0` use-after-dispose ve `0` çökme ile çalıştı; buna karşın `Alt+Left` kısayolunun bağlı olmadığı (`P1-3`) tespit edildi.
