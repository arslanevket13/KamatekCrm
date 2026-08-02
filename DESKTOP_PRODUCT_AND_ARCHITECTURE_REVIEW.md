# KamatekCRM Masaüstü — Ürün ve Mimari İnceleme

Tarih: 2026-08-02
Kapsam: WPF masaüstü, Application, Infrastructure, Shared ve testler. API/Web kapsam dışıdır.

## Yönetici özeti

KamatekCRM; CRM, servis yönetimi, saha operasyonu, stok, satın alma, teklif, satış, finans ve raporlamayı aynı masaüstü uygulamasında birleştiren güçlü bir ürün çekirdeğine sahip. Kapsamdaki masaüstü/Application/Infrastructure projeleri sıfır uyarı ve sıfır hata ile derleniyor; mevcut 113 test geçiyor. En büyük risk özellik eksikliği değil; büyüyen özelliklerin birkaç dev ViewModel ve servis içinde yoğunlaşması, UI ile iş mantığının yer yer birbirine karışması ve kritik süreçlerin test kapsamının hâlâ genişletilmeye ihtiyaç duymasıdır.

Önerilen ürün yönü: **“Güvenlik sistemleri firmaları için tekliften tahsilata, keşiften bakıma tek operasyon merkezi.”** Yeni özellikler bu omurgaya bağlanmalı; bağımsız ekranlar yığınına dönüşmemelidir.

## Mevcut ürün yetenekleri

- Müşteri ve cari hareket yönetimi, müşteri zaman çizelgesi, notlar ve segmentasyon
- Ürün, depo, stok sayım, transfer, seri numarası, rezervasyon ve stok hareketleri
- Arıza kaydı, servis emri, tamir, saha işi, teknisyen ve rota planlama
- Standart teklif ile kapsam ağacı kullanan proje teklifi
- Satın alma, tedarikçi, sipariş ve PDF fatura okuma
- Hızlı satış/POS, ödeme ve termal fiş
- Finans, finansal sağlık, analitik, rapor ve dışa aktarım
- Kullanıcı, rol tabanlı yetki, audit log, yedekleme ve ağ ayarları
- Global arama, bildirim, tema, yükleme durumu ve tekrar kullanılabilir WPF bileşenleri

## Öncelikli eksikler ve riskler

### P0 — Güven ve veri bütünlüğü

1. **Varsayılan yönetici hesabı — tamamlandı:** Kriptografik geçici parola ve ilk girişte zorunlu değiştirme uygulandı.
2. **Yetki denetimi — kritik yollar tamamlandı:** Silme, finans, satın alma, ayar, kullanıcı, stok, satış ve servis komutları merkezî servis denetimine alındı; yeni use-case'ler aynı sözleşmeyi kullanmalı.
3. **Stok ve finans işlemleri — Faz 1 tamamlandı:** Satış ve satın alma oluşturma/teslim alma, kısmi-tam iadeler, karantina, kasa-cari ters kayıtları ve sadakat etkileri serializable transaction ve idempotency korumasında. Eski iç içe satın alma mutasyon yolu kaldırıldı.
4. **Yedek doğrulama — kısmen tamamlandı:** Checksum, manifest, arşiv provası, işlem öncesi kurtarma noktası ve başarısızlıkta otomatik geri alma uygulandı. Zamanlanmış çevrimdışı restore provası, saklama politikası ve şifreleme kaldı.
5. **Kişisel veri — kısmen tamamlandı:** Rol bazlı maskeleme, arama/ekran/belge koruması ve erişim kaydı uygulandı. KVKK saklama, anonimleştirme/silme talebi ve ek dosya sınıflandırması kaldı.

### P1 — Mimari ve sürdürülebilirlik

1. `ServiceJobViewModel`, `StockCountViewModel`, `ProjectQuoteEditorViewModel`, `DirectSalesViewModel` ve `PdfService` büyük sınıflar olmaya devam ediyor. Servis işi, stok sayım ve proje teklif veri erişimi ayrıştırıldı; kalan form state, Excel/PDF üretimi ve pencere orkestrasyonu daha küçük feature bileşenlerine bölünmeli.
2. ViewModel’lerde `MessageBox`, global `App.ServiceProvider` ve doğrudan `AppDbContext` kullanımı diğer modüllerde devam ediyor. Satış/satın alma iade ekranları ile `ServiceJobViewModel` bu bağımlılıklardan temizlendi; kalan modüller `IDialogService`, navigation factory ve Application servislerine kademeli geçirilmeli.
3. `KamatekCRM/Migrations` ile `KamatekCrm.Infrastructure/Migrations` çift migration izi oluşturuyor. Tek migration assembly Infrastructure olmalı; eski set arşivlenmeden önce üretim veritabanı geçmişiyle karşılaştırılmalı.
4. UI thread üzerinde senkron DB/process işlemleri bulunuyor. Tüm I/O iptal edilebilir async API’ye geçirilmeli.
5. Event abonelikleri yaşam döngüsünden bağımsız. Weak subscription veya açık unsubscribe olmadan ekran tekrar açılışlarında bellek sızıntısı/çift tetikleme riski var.
6. Bağlantı kopunca kaydedilen “acil taslak” yalnızca ekran adı ve zamanı içeriyor; gerçek form verisini kurtarmıyor. Taslak sözleşmesi ve otomatik geri yükleme akışı gerekli.

### P1 — Ürün akışı boşlukları

1. **Tekliften nakde uçtan uca akış:** Keşif → teklif → müşteri onayı → iş emri → malzeme rezervasyonu → kurulum → kabul formu → fatura → tahsilat tek timeline üzerinde bağlanmalı.
2. **Bakım sözleşmeleri:** Periyodik iş emri üretimi, SLA, yenileme hatırlatma, sözleşme kârlılığı ve cihaz bazlı bakım geçmişi tamamlanmalı.
3. **RMA/garanti:** Seri numarasına göre garanti, tedarikçiye gönderim, geçici cihaz, RMA durumu ve maliyet takibi eklenmeli.
4. **İade/iptal — tamamlandı:** POS işlem geçmişi, kısmi/tam satış iadesi, ödeme dağılımı, karantina, tedarikçi iadesi, bekleyen sipariş iptali, ön onay özeti ve `ProcessReturns` yetkisi eklendi.
5. **Saha kanıtı:** Önce/sonra fotoğraf, müşteri imzası, GPS/zaman damgası, kullanılan malzeme ve dijital servis formu tek teslim paketi olmalı.
6. **Onay merkezi:** İskonto, düşük marj, stok düzeltme, satın alma ve masraf için limit bazlı onay kuyruğu eklenmeli.

## Yaratıcı ve yüksek değerli yeni özellikler

### Operasyon radarı

Tek ekranda geciken işler, SLA riski, kritik stok, tahsilat riski ve teknisyen kapasitesini gösteren “bugün neye müdahale etmeliyim?” merkezi. Her kart doğrudan düzeltici aksiyona gitmeli.

### Teklif zekâsı

Geçmiş kazanılan/kaybedilen tekliflerden kategori bazlı marj aralığı, muadil ürün, eksik kapsam ve fiyat eskimesi uyarıları. İlk sürüm kural tabanlı olabilir; yapay zekâ zorunlu değildir.

### Kurulu sistem dijital ikizi

Müşteri lokasyonu → bina/kat/oda → cihaz → seri numarası → garanti → servis geçmişi hiyerarşisi. Arızada teknisyen, sahaya gitmeden topolojiyi ve geçmiş müdahaleleri görür.

### Akıllı bakım planlayıcı

Cihaz yaşı, arıza sıklığı, garanti bitişi ve müşteri önemine göre bakım önceliği üretir; uygun teknisyen ve rota önerir.

### Kârlılık otopsisi

Teklifte öngörülen ürün/işçilik ile gerçekleşen tüketim, süre, yol ve tekrar ziyaretleri karşılaştırır. “Ciro yüksek ama zarar ettiren” iş tiplerini görünür kılar.

### Sessiz veri kalite asistanı

Yinelenen müşteri, hatalı telefon, boş vergi bilgisi, negatif stok, karşılıksız cari hareket ve sahipsiz dosya gibi sorunları arka planda tespit edip güvenli bir düzeltme kuyruğu sunar.

## Hedef mimari

Her özellik dikey bir dilim olarak düzenlenmeli:

`Feature UI → Application use-case → Domain policy → Repository/DbContext`

- WPF yalnızca görünüm, kullanıcı etkileşimi ve ekran durumunu yönetir.
- Application katmanı komut/sorgu, doğrulama, yetki ve transaction sınırını belirler.
- Domain katmanı stok, fiyat, SLA, durum geçişi ve finans kurallarını taşır.
- Infrastructure yalnızca EF, dosya, e-posta, yazıcı ve dış sistem adaptörlerini içerir.
- Okuma ekranlarında projection + `AsNoTracking`; yazmalarda kısa ömürlü context ve optimistic concurrency kullanılır.
- Büyük patlama refaktörü yerine modül modül “strangler” geçişi uygulanır.

## Uygulama yol haritası

### Faz 1 — Güvenli temel (1–2 hafta)

**Durum: tamamlandı (2026-08-02).** Son paket satış/satın alma işlem güvenliği, iadeler, finansal uzlaştırma ve SQLite rollback testlerini kapattı. `20260802010000_ReturnAndFinancialIntegrity` migrasyonu üretildi ancak hiçbir gerçek kullanıcı veritabanına uygulanmadı.

- Bootstrap admin ve ilk giriş parola değiştirme
- Transaction/idempotency kontrolleri
- Migration sahipliğini tekleştirme
- Yedek doğrulama ve sağlık ekranı
- Kritik satış/stok/finans senaryolarına entegrasyon testleri

### Faz 2 — Mimari inceltme (2–4 hafta)

**Durum: devam ediyor (2026-08-02).** İlk paket statik `AuditService` ve onun global service-locator kullanımını kaldırdı. Audit yazımı kullanıcı bağlamlı, bütünlük mühürlü ve test edilebilir `IAuditTrailService` sözleşmesine taşındı. PDF servisinin ViewModel’lerde elle oluşturulması kaldırıldı. İade ve satın alma ekranları EF entity yerine DTO projection kullanan `ITransactionReadService` ile çalışıyor; transaction ViewModel’lerinde `UnitOfWork`, `AppDbContext` ve doğrudan `MessageBox` bağımlılığı kalmadı. İkinci paket `ServiceJobViewModel` içindeki tüm EF okuma/yazma yollarını kaldırdı; servis işi çalışma alanı, arama, malzeme, geçmiş, KPI ve belge verisi `IServiceJobReadService` projection'larına taşındı. Hızlı müşteri + yeni cihaz + iş emri + rezervasyon tek transaction oldu; silme yetki, audit ve rezervasyon serbest bırakma kurallarıyla merkezileşti. Üçüncü paket stok sayımını idempotent `StockCountSession` aggregate'ına taşıdı; snapshot çakışması, depo/ürün toplam stok uzlaştırması, audit kalemleri ve eski geçmiş uyumluluğu merkezileştirildi. Dördüncü paket proje teklif fiyatlandırmasını saf Application politikasına, okumaları DTO projection'larına ve kaydetme/revizyonu idempotent serializable komut sınırına taşıdı. Beşinci paket standart teklif ekranını da merkezî fiyatlandırma, kısa ömürlü sorgu/komut servisleri, idempotent kayıt ve audit omurgasına aldı; keşiften standart teklife bağ kurdu ve sahte döviz seçimini kapattı. `StockCountViewModel`, `ProjectQuoteEditorViewModel` ve `QuotationViewModel` içinde EF veya doğrudan sistem diyaloğu kalmadı.

- Servis işi, stok sayım ve satış modüllerini use-case’lere ayırma
- ViewModel’lerden DbContext, MessageBox ve service-locator temizliği
- Async/cancellation standardı ve merkezi hata eşleme
- Navigasyon yaşam döngüsü ve event abonelik yönetimi

### Faz 3 — Operasyon bütünlüğü (3–6 hafta)

- Tekliften tahsilata birleşik süreç/timeline
- Onay merkezi, iade/iptal ve RMA
- Saha kanıt paketi ve bakım otomasyonu
- Rol bazlı dashboard ve operasyon radarı

### Faz 4 — Zekâ ve optimizasyon

- Teklif zekâsı, veri kalite asistanı
- Kârlılık otopsisi ve kapasite tahmini
- Dijital ikiz ve önleyici bakım önerileri

## Kalite kapıları

- Her kritik use-case için başarı, doğrulama, yetki ve rollback testi
- Domain/Application için anlamlı branch coverage hedefi; UI code-behind coverage hedefi değil
- Build’de sıfır hata; yeni warning kabul edilmez
- Migration CI doğrulaması ve temiz veritabanından kurulum testi
- Büyük ViewModel/servisler için yeni kod eklemeden önce ayrıştırma zorunluluğu
- Performans bütçeleri: ana ekran <2 sn, arama geri bildirimi <300 ms, uzun işlemlerde iptal ve ilerleme

## Bu incelemede uygulanan ilk iyileştirme

- Veritabanı migration/seed sorumluluğu WPF `App` sınıfından Infrastructure servisine taşındı.
- Sağlayıcıya özel çalışma zamanı `ALTER TABLE` komutları UI’dan kaldırıldı; migration tek kaynak oldu.
- Sabit PostgreSQL parola fallback’i kaldırıldı; eksik yapılandırma artık açık hata veriyor.
- Seed admin parolası düz metin yerine BCrypt hash’iyle saklanıyor.
- Parola sıfırlama akışındaki düz metin kayıt açığı kapatıldı ve güçlü parola politikası eklendi.
- Tahmin edilebilir `admin/123` kaldırıldı; ilk kurulumda kriptografik geçici parola üretiliyor.
- POS satışına istemci idempotency anahtarı, benzersiz veritabanı indeksi ve serializable transaction eklendi.
- Yinelenen ürün satırları stok kontrolünde birleştirilerek negatif stok açığı kapatıldı.

## Uygulanan ikinci kritik paket — servis işi bütünlüğü

- Servis işi yaşam döngüsü izinli/geçersiz geçişları tanımlayan merkezi durum politikasına taşındı.
- İş emri, malzeme kalemleri ve depo bazlı stok rezervasyonları tek serializable transaction sınırında kaydediliyor.
- Yetersiz stokta kısmi iş emri veya yarım rezervasyon oluşması engellendi.
- Tamamlama; rezervasyonu doğruluyor, depo ve ürün stoklarını yalnızca bir kez düşüyor, rezervasyonu kapatıyor, müşteri aktivitesi/geçmişini aynı akışta yazıyor.
- Cihaz kabul, onarım listesi, onarım takip ve saha işi ekranlarının tamamlanma yolları aynı Application komut servisine bağlandı.
- Keşiften teklife dönüşüm durum politikası, audit geçmişi ve idempotent işaretleme ile merkezileştirildi.
- Üç ekrandaki çalışmayan “parça ekle/çıkar” komut adı uyuşmazlıkları giderildi; onarım ekranlarındaki parça değişiklikleri rezervasyon-aware kayıt hattına alındı.
- Yeni servis işi formuna koşullu doğrulama, kayıt sırasında ilerleme durumu, erişilebilir adlar ve semantik görsel stiller eklendi.
- PostgreSQL bağlantı sağlayıcısı hem standart `ConnectionStrings:PostgreSQL` hem mevcut ağ ayarı şemasını okuyacak şekilde uyumlu hâle getirildi.

## Faz 2 servis işi inceltme paketi

- `ServiceJobViewModel` içinden `IDbContextFactory`, EF sorguları, doğrudan `SaveChanges`, `MessageBox` ve dosya diyalogları kaldırıldı.
- Liste/arama, müşteri-ürün-teknisyen çalışma alanı, müşteri cihaz/projeleri, iş malzemeleri, geçmiş, dashboard KPI ve PDF verisi yetki kontrollü `IServiceJobReadService` DTO projection'larına taşındı.
- Hızlı müşteri ve yeni cihaz oluşturma iş emri ile aynı serializable transaction'a alındı; sonraki doğrulama hatasında SQLite rollback testiyle kalıntı oluşmadığı doğrulandı.
- Bekleyen iş emri silme, `ManageServiceJobs` + `DeleteRecords` denetimi, aktif rezervasyonların kapatılması, soft-delete ve değişmez geçmiş kaydıyla `IServiceJobCommandService` içine taşındı. Tamamlanmış/stok tüketmiş işler silinemiyor.
- Arama büyük/küçük harfe duyarsızlaştırıldı, bitiş tarihi tüm günü kapsıyor, debounce iptal edilebilir oldu ve durum filtresindeki Türkçe metin/enum binding hatası giderildi.

## Faz 2 stok sayım bütünlüğü paketi

- Tam depo ve manuel sayım tek `IStockCountCommandService` transaction sınırında birleştirildi; `AdjustInventory` yetkisi UI'dan bağımsız doğrulanıyor.
- Her sayım benzersiz idempotency anahtarı, referans, depo, kullanıcı, UTC zaman, sayım modu ve değişmez satır snapshot'ları taşıyor. Aynı istek ikinci kez depo stoğunu değiştirmiyor.
- Ekrandaki sistem miktarı ile güncel depo miktarı farklıysa tüm sayım reddediliyor; hiçbir oturum, hareket veya kısmi stok güncellemesi oluşmuyor.
- Depo miktarı değişirken `Product.TotalStockQuantity` tüm depo satırlarının toplamından yeniden hesaplanıyor; eski drift sessizce taşınmıyor.
- Sayım geçmişi yeni oturum tablolarından DTO projection ile okunuyor; eski `COUNT-` ve `MANUAL-` stok hareketleri geriye dönük görünür kalıyor.
- Barkod ilk okutma gerçek fiziksel sayımı 1'den başlatıyor, sayılmamış satırlar manuel listede yanlışlıkla sıfır stoğa dönüşmüyor ve negatif sayım istemci ile servis katmanında reddediliyor.
- Excel dışa aktarım formatı yeniden içe alınabilir hâle getirildi; başlık/miktar kolonu algılanıyor ve tanınmayan ürün ile geçersiz miktar ayrı raporlanıyor.
- `20260802030000_StockCountIntegrity` migrasyonu üretildi ancak hiçbir kullanıcı veritabanına uygulanmadı.

## Faz 2 proje teklif bütünlüğü paketi

- `ProjectQuoteEditorViewModel` içinden `IDbContextFactory`, EF sorguları, doğrudan `SaveChanges`, `MessageBox`, `SaveFileDialog`, `Interaction.InputBox` ve elle `EmailService` oluşturma kaldırıldı.
- Müşteri/ürün çalışma alanı ve teklif detayı, yetki kontrollü `IProjectQuoteReadService` DTO projection'larına taşındı; yükleme yarışını önlemek için mevcut teklif çalışma alanının tamamlanmasını bekliyor.
- Teklif finansalları serileştirilmiş `RecursiveTotal`/`TotalPrice` alanlarına güvenmeyen `ProjectQuotePricingPolicy` ile birim fiyat, maliyet, işçilik ve miktardan yeniden hesaplanıyor. İskonto, KDV ve para yuvarlaması UI ile kayıt tarafında aynı kaynağı kullanıyor.
- Kaydetme `ManageQuotes` izni, idempotency anahtarı, serializable transaction ve audit kaydıyla merkezileştirildi. Aynı istek ikinci kez proje veya revizyon oluşturmuyor.
- Güncelleme beklenen revizyon numarasını doğruluyor; eski ekran yeni revizyonu ezemiyor. Gerçek değişiklikte veritabanındaki önceki kapsam ve finansallar arşivleniyor; yalnız genişletme/seçim gibi UI durumu değişiklikleri sahte revizyon üretmiyor.
- Kapsam kalemindeki çalışma zamanı callback'inin JSON'a yazılmaya çalışması nedeniyle undo/kayıt snapshot'larını bozabilen serileştirme hatası `JsonIgnore` ile kapatıldı.
- Fiyat yeniden hesaplama, kapsam round-trip'i, idempotency, doğru revizyon arşivi, stale update, bozuk geçmiş rollback'i ve yetkisiz erişim için yedi test eklendi; toplam test sayısı 97 oldu.
- Bu paket şema değişikliği veya migration gerektirmedi; hiçbir kullanıcı veritabanına işlem uygulanmadı.

### Teklif yaşam döngüsü genişletmesi

- `QuoteListViewModel` içindeki uzun ömürlü `AppDbContext`, senkron EF sorguları, doğrudan `SaveChanges`, `MessageBox`, `SaveFileDialog` ve service-locator kaldırıldı. Pencere oluşturma UI adaptöründe izole edildi.
- Liste ve PDF belgesi verileri `IProjectQuoteReadService` DTO projection'larından okunuyor. ContextMenu'nün kopuk DataContext'i ile XAML'de bulunup ViewModel'de olmayan onay/red komutları düzeltildi.
- Durum politikası `Taslak/Revize → Gönderildi → Onaylandı | Reddedildi | Süresi Doldu` sırasını zorunlu kılıyor. Taslak doğrudan onaylanamıyor, ret nedeni zorunlu ve tarihi geçmiş teklif onaylanamıyor.
- Gönderim geçerlilik tarihi, müşteri onayı/reddi, pipeline ve proje durumu aynı transaction içinde güncellenip audit kaydına alınıyor. Süresi geçen gönderilmiş teklifler yenilemede idempotent biçimde kapatılıyor.
- Yalnız hiç gönderilmemiş taslak silinebiliyor; gönderilmiş teklif geçmişi korunuyor. Kopyalama merkezî fiyatlandırmayla yeni numaralı bir taslak üretiyor ve aynı işlem anahtarı ikinci kopyayı oluşturmuyor.
- Onaylı teklif, müşteri/proje bağlantısı ve net/KDV/genel toplam snapshot'larıyla tek bir kurulum iş emrine dönüştürülebiliyor. Tekrar çağrı aynı iş emrini döndürüyor.
- Onaylı veya gönderilmiş teklif düzenlenirse eski onay geçersizleşiyor; yeni içerik `Revize` durumuna alınarak yeniden gönderim zorunlu oluyor.
- Liste/PDF projection'ı, geçiş sırası, idempotent onay, otomatik süre dolumu, güvenli taslak silme/kopyalama, gönderilmiş kayıt koruması, iş emri dönüşümü ve onay sonrası revizyon için dokuz test eklendi; toplam test sayısı 106 oldu.
- Bu genişletme de şema değişikliği veya migration gerektirmedi; hiçbir kullanıcı veritabanına işlem uygulanmadı.

### Standart teklif omurgası genişletmesi

- `QuotationViewModel` içinden uzun ömürlü EF context, doğrudan `SaveChanges`, `MessageBox`, Win32 dosya diyaloğu, timer ve global service-locator kaldırıldı. Ürün araması iptal edilebilir debounce kullanıyor; düzenlenebilir satırlar değişiklikleri anında merkezî fiyatlandırmaya iletiyor.
- Müşteri/ürün çalışma alanı ve belge okumaları `IStandardQuoteReadService`, kayıt ise `IStandardQuoteCommandService` sınırına taşındı. Yetki, doğrulama, seri numarası üretimi, idempotency, serializable transaction ve audit tek komut akışında uygulanıyor.
- Ürün kimliği, ad, stok kodu, birim, alış maliyeti ve KDV istemciden güvenilir veri olarak kabul edilmiyor; kayıt sırasında güncel ürün kataloğundan snapshot alınıyor. Fiyat, iskonto, vergi, maliyet, kâr ve marj UI ile servis tarafında aynı `StandardQuotePricingPolicy` sonucunu kullanıyor.
- Kur kaynağı ve kur tarihi olmadan USD/EUR etiketinin TL tutarını yabancı para gibi göstermesi engellendi; standart teklif gerçek kur altyapısı eklenene kadar yalnız TRY kabul ediyor.
- “Kaydet ve gönder” eylemi gerçek e-posta göndermediği için “Kaydet ve Gönderildi İşaretle” olarak açıklaştırıldı. Sistem terminal dışı bir gönderim yapmış gibi davranmıyor.
- Müşteri, dashboard ve ana içerik teklif açılışları `IQuotationLauncher` adaptöründe birleştirildi. `QuotationWindow` içindeki parametresiz service-locator kurucusu kaldırıldı.
- Keşif işinden açılan standart teklif kaynak iş ve müşteriyle doğrulanıyor; teklif numarası servis geçmişine aynı transaction içinde yazılıyor ve aynı keşiften ikinci standart teklif üretilmesi engelleniyor.
- Eski, düz `ProjectQuoteViewModel` DI kaydından çıkarıldı; aktif standart teklif ve kapsam ağaçlı proje teklif akışları artık ayrı aggregate'lar olarak aynı Application mimari ilkelerini kullanıyor. Eski dosyalar veri migrasyonu kararı verilmeden fiziksel olarak silinmedi.
- Fiyatlama, katalog snapshot'ı, idempotency, rollback, yetki, okuma projection'ları ve keşif bağlantısı için yedi test eklendi; toplam test sayısı 113 oldu. Masaüstü çözümü 0 hata/0 uyarı ile derlendi.
- Bu genişletme şema değişikliği veya migration gerektirmedi; hiçbir kullanıcı veritabanına işlem uygulanmadı.

## Uygulanan üçüncü kritik paket — yetki ve migration güvenliği

- UI görünürlüğünden bağımsız `ApplicationPermission`, kullanıcı bağlamı ve merkezi yetkilendirme servisi oluşturuldu.
- Servis işi, POS satışı, stok mutasyonları, satın alma teslim/onayı ve kullanıcı yönetimi servis girişlerinde yetki denetimine alındı.
- Finans, analitik, kullanıcılar, sistem logları, ayarlar ve sunucu rolü komutlarına doğrudan çağrı koruması eklendi.
- Doğrulanmamış token parametresiyle parola kontrolünü atlayan masaüstü giriş yolu tamamen kaldırıldı.
- `1234` başlangıç/sıfırlama parolaları kaldırıldı; kriptografik güçlü geçici parola ve ilk girişte zorunlu değiştirme akışı eklendi.
- Eski dört migration kimliği korunarak Infrastructure assembly'sine bağlandı; masaüstü projesindeki çift derleme kaldırıldı.
- Eksik teklif şema alanları için veri koruyucu uzlaştırma migration'ı, geçici parola durumu için ayrı migration eklendi.
- Dinamik seed tarihleri sabitlenerek sürekli “pending model changes” üreten model sürüklenmesi kapatıldı.
- Migration zinciri, model snapshot uyumu ve idempotent PostgreSQL script üretimi test kapısına alındı.

## Uygulanan dördüncü kritik paket — yedek, kişisel veri ve denetim bütünlüğü

- Birbiriyle uyumsuz düz SQL/ZIP/`pg_restore` akışları kaldırıldı; yedekleme ve geri yükleme PostgreSQL custom `.backup` biçiminde tek sözleşmeye bağlandı.
- Her yedek için sürümlü manifest, dosya boyutu ve SHA-256 özeti üretiliyor; geri yüklemeden önce hem checksum hem `pg_restore --list` arşiv provası zorunlu.
- Geri yükleme öncesi otomatik kurtarma yedeği alınıyor. Seçilen yedek yarıda kalırsa bu noktayla otomatik geri alma deneniyor; çift hata durumunda kurtarma dosyasının yolu korunarak raporlanıyor.
- Yedekleme dizini, dosya filtresi ve ayarlar ekranındaki metinler tek kaynaktan besleniyor; ayarlar ViewModel'inin servis bağımlılığı DI'a taşındı.
- Telefon, e-posta, adres, TCKN ve vergi numarası için merkezi rol bazlı kişisel veri politikası oluşturuldu. Yetkisiz kullanıcıda veriler maskelemeli ve fail-closed gösteriliyor.
- Müşteri listesi/detayı, satış müşteri araması, onarım/saha ekranları, global arama, raporlar, PDF belgeleri ve termal servis fişleri aynı maskeleme politikasına bağlandı.
- Yetkisiz kullanıcıların telefon/e-posta üzerinden müşteri araması engellendi; kimlik ve iletişim alanlarının maskeli değerle yanlışlıkla kaydedilmesi önlendi.
- Kişisel veri detay görüntüleme ve müşteri belge/rapor üretimi denetim kaydına alındı; log içeriğine gerçek kişisel veri yazılmıyor.
- Yeni denetim kayıtları kültürden bağımsız SHA-256 bütünlük mührüyle saklanıyor. Uygulama katmanı güncelleme/silmeyi reddediyor, PostgreSQL tetikleyicisi tabloyu append-only yapıyor.
- Sistem kayıtları ekranına doğrulanmış, eski ve şüpheli kayıt sayılarını gösteren bütünlük özeti eklendi.
- Yedek manifesti, kişisel veri maskeleme, audit mühürleme/değişmezlik ve migration zinciri için yeni testler eklendi; toplam test sayısı 63'e çıktı.
