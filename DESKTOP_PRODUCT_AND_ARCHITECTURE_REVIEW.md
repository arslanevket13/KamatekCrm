# KamatekCRM Masaüstü — Ürün ve Mimari İnceleme

Tarih: 2026-08-01  
Kapsam: WPF masaüstü, Application, Infrastructure, Shared ve testler. API/Web kapsam dışıdır.

## Yönetici özeti

KamatekCRM; CRM, servis yönetimi, saha operasyonu, stok, satın alma, teklif, satış, finans ve raporlamayı aynı masaüstü uygulamasında birleştiren güçlü bir ürün çekirdeğine sahip. Kapsamdaki masaüstü/Application/Infrastructure projeleri sıfır uyarı ve sıfır hata ile derleniyor; mevcut 63 test geçiyor. En büyük risk özellik eksikliği değil; büyüyen özelliklerin birkaç dev ViewModel ve servis içinde yoğunlaşması, UI ile iş mantığının yer yer birbirine karışması ve kritik süreçlerin test kapsamının hâlâ genişletilmeye ihtiyaç duymasıdır.

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
3. **Stok ve finans işlemleri — kısmen tamamlandı:** Satış ve servis rezervasyon/tamamlama akışları transaction ve idempotency korumasında. İade, ters kayıt ve bazı eski stok yolları sonraki pakette ele alınmalı.
4. **Yedek doğrulama — kısmen tamamlandı:** Checksum, manifest, arşiv provası, işlem öncesi kurtarma noktası ve başarısızlıkta otomatik geri alma uygulandı. Zamanlanmış çevrimdışı restore provası, saklama politikası ve şifreleme kaldı.
5. **Kişisel veri — kısmen tamamlandı:** Rol bazlı maskeleme, arama/ekran/belge koruması ve erişim kaydı uygulandı. KVKK saklama, anonimleştirme/silme talebi ve ek dosya sınıflandırması kaldı.

### P1 — Mimari ve sürdürülebilirlik

1. `ServiceJobViewModel` 2200+, `StockCountViewModel`, `ProjectQuoteEditorViewModel`, `DirectSalesViewModel` ve `PdfService` 1000+ satır. Her biri use-case/feature dilimlerine ayrılmalı.
2. ViewModel’lerde `MessageBox`, global `App.ServiceProvider` ve doğrudan `AppDbContext` kullanımı devam ediyor. `IDialogService`, navigation factory ve Application servislerine kademeli geçiş yapılmalı.
3. `KamatekCRM/Migrations` ile `KamatekCrm.Infrastructure/Migrations` çift migration izi oluşturuyor. Tek migration assembly Infrastructure olmalı; eski set arşivlenmeden önce üretim veritabanı geçmişiyle karşılaştırılmalı.
4. UI thread üzerinde senkron DB/process işlemleri bulunuyor. Tüm I/O iptal edilebilir async API’ye geçirilmeli.
5. Event abonelikleri yaşam döngüsünden bağımsız. Weak subscription veya açık unsubscribe olmadan ekran tekrar açılışlarında bellek sızıntısı/çift tetikleme riski var.
6. Bağlantı kopunca kaydedilen “acil taslak” yalnızca ekran adı ve zamanı içeriyor; gerçek form verisini kurtarmıyor. Taslak sözleşmesi ve otomatik geri yükleme akışı gerekli.

### P1 — Ürün akışı boşlukları

1. **Tekliften nakde uçtan uca akış:** Keşif → teklif → müşteri onayı → iş emri → malzeme rezervasyonu → kurulum → kabul formu → fatura → tahsilat tek timeline üzerinde bağlanmalı.
2. **Bakım sözleşmeleri:** Periyodik iş emri üretimi, SLA, yenileme hatırlatma, sözleşme kârlılığı ve cihaz bazlı bakım geçmişi tamamlanmalı.
3. **RMA/garanti:** Seri numarasına göre garanti, tedarikçiye gönderim, geçici cihaz, RMA durumu ve maliyet takibi eklenmeli.
4. **İade/iptal:** POS ve satın alma için kısmi iade, iptal nedeni, stok/finans ters kayıtları ve yetkili onayı gerekli.
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

- Bootstrap admin ve ilk giriş parola değiştirme
- Transaction/idempotency kontrolleri
- Migration sahipliğini tekleştirme
- Yedek doğrulama ve sağlık ekranı
- Kritik satış/stok/finans senaryolarına entegrasyon testleri

### Faz 2 — Mimari inceltme (2–4 hafta)

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
