# Kamatek CRM - Mimari Denetim Raporu (Network & Veritabanı)

## Yönetici Özeti (Executive Summary)
Bu rapor, Kamatek CRM projesinin veritabanı bağlantı sağlığı ve yerel ağdaki (LAN) istemci/sunucu otomatik keşif (Zero-Configuration) mekanizmasını incelemektedir. Sistem genel olarak modern WPF standartlarına (IDbContextFactory kullanımı, asenkron işlemler) uygun tasarlanmış olsa da, Ağ Keşif (Network Discovery) servisinde çok kritik mantıksal hatalar ve yapılandırma ihlalleri tespit edilmiştir. 

---

## 1. Veritabanı Mimarisi Sağlığı (Strengths & Vulnerabilities)

### Güçlü Yönler (Strengths):
- **WPF için Doğru DbContext Yönetimi:** `CustomerViewModel` gibi servislerde `IDbContextFactory` kullanımı mükemmel bir tercih. Bu sayede Entity Framework'ün WPF uygulamalarında sıkça yarattığı "Memory Leak" ve "Aynı DbContext üzerinden eşzamanlı işlem yapma" (State Bleeding) hatalarının önüne geçilmiş.
- **Sağlıklı Asenkron Yapı (Async/Await):** Veritabanı sorguları (`ToListAsync`, `CountAsync`) UI thread'ini bloklamayacak şekilde tamamen asenkron tasarlanmış.
- **Bağlantı Testi ve Thread Safety:** `DatabaseConnectionProvider` içindeki `TestConnectionAsync` metodu, UI donmalarını engellemek için `Task.Run` ile arka plana atılmış. Bağlantı dizesinde `Pooling=true;MinPoolSize=1;MaxPoolSize=100` ile havuzlama aktif edilmiş, test bağlantılarında ise `Pooling=false` ve 2 saniyelik katı bir zaman aşımı (Timeout) kullanılarak ölü bağlantıların birikmesi önlenmiş. `ReaderWriterLockSlim` ile thread-safety kusursuz sağlanmış.

### Zayıf Yönler / Güvenlik Açıkları (Vulnerabilities):
- **Split-Brain (Çift Yapılandırma) Riski:** `DatabaseConnectionProvider`, bağlantı dizesini dinamik üretiyor ancak `AppDbContext.OnConfiguring` metodunda hala `AppSettings.PostgreSqlConnectionString` sabitine bir "fallback" (geri dönüş) bırakılmış. Eğer DI (Dependency Injection) konteynerinde `IDbContextFactory` yapılandırılırken provider kullanılmazsa, sistem provider'ı ezip `appsettings.json`'daki sabit dizeyi kullanabilir.

---

## 2. Ağ Keşif Mekanizması (Network Discovery Flow & Robustness)

### Sunucu/İstemci Akışı (Flow):
Servis (`NetworkDiscoveryService`) "Zero-Configuration" hedefiyle tasarlanmış.
1. **Önbellek (Cache) Kontrolü:** Önce kaydedilmiş son başarılı IP test ediliyor.
2. **Localhost Kontrolü (Sunucu Rolü):** Makinedeki `127.0.0.1` adresinde veritabanı var mı bakılıyor. Varsa sistem kendini "Ana Sunucu" ilan edip 3 saniyede bir UDP (`54321` portundan) ağa `KAMATEK_DISCOVERY_PING` yayını (Broadcast) yapmaya başlıyor.
3. **Dinleme (İstemci Rolü):** Eğer localhost'ta veritabanı yoksa, sistem UDP yayınlarını dinlemeye başlıyor. 3 saniye içinde yayın yakalarsa o IP'yi Ana Sunucu olarak kabul edip bağlanıyor.
4. **Manuel Fallback:** Hiçbir şey bulunamazsa `DatabaseConnectionLostEvent` fırlatılarak kullanıcıya manuel IP girme sihirbazı gösteriliyor.

### Sağlamlık ve Hatalar (Robustness):
Ağ keşif servisi kağıt üzerinde güzel görünse de pratikte çok ciddi sorunlara gebedir (Bkz: Kritik Risk Değerlendirmesi).

---

## 3. Kritik Risk Değerlendirmesi (Critical Risk Assessment)

Bu bölümde sistemin çökmesine veya veri bütünlüğünün bozulmasına yol açabilecek "Ölümcül (Fatal)" riskler listelenmiştir:

1. **[FATAL] `IsMainServer` Bayrağının (Flag) İhlal Edilmesi:**
   - `appsettings.json` içinde yer alan `"IsMainServer": false` ayarı `NetworkDiscoveryService` tarafından **tamamen göz ardı edilmektedir.**
   - Kod, Ana Sunucu olup olmadığını `127.0.0.1` bağlantı testiyle anlamaya çalışıyor. **Risk:** Ağdaki rastgele bir istemci bilgisayarda, başka bir yazılım için kurulmuş ve aynı şifreye/kullanıcıya sahip yerel bir PostgreSQL varsa, o istemci kendini yanlışlıkla "Ana Sunucu" sanacaktır. Kendi boş veritabanına bağlanacak ve ağa UDP yayını yaparak diğer istemcileri kendine çekecek, ağı tamamen çökertecektir (Rogue Server Attack).

2. **[FATAL] Race Condition (Zamanlama Uyuşmazlığı) Nedeniyle UDP Paket Kaybı:**
   - Sunucu 3 saniyede bir UDP yayını yapıyor (`Task.Delay(3000)`).
   - İstemci ise UDP yayınlarını sadece 3 saniye dinliyor (`TimeSpan.FromSeconds(3)`).
   - **Risk:** Eğer istemci dinlemeye başladığı anda sunucu yayınını saniyenin onda biri kadar önce yapmışsa, istemcinin 3 saniyelik dinleme süresi, sunucunun bir sonraki yayını gelmeden hemen önce bitecektir (Timeout). Bu durumda istemci aynı ağda olmasına rağmen sunucuyu "bulamadı" diyerek hata verecektir.

3. **[HIGH] Konfigürasyon Dosyasının (appsettings.json) Yok Sayılması:**
   - Port (`54321`) ve Timeout süreleri `NetworkDiscoveryService.cs` içinde hard-code (sabit kod) olarak yazılmış (`const int BROADCAST_PORT = 54321`). `appsettings.json` içerisindeki `NetworkDiscovery` bloğundaki Port ve Timeout ayarlarının kodda hiçbir karşılığı yoktur.

---

## 4. Minimal Aksiyon Alınabilir Tavsiyeler (Minimal Actionable Advice)

Sistemi bozmadan ve büyük bir Refactor yapmadan stabiliteyi sağlamak için şu küçük düzeltmeler yapılmalıdır:

1. **UDP Dinleme Süresini Uzatın:**
   `NetworkDiscoveryService.cs` içindeki dinleme süresini sunucunun yayın frekansından daha uzun olacak şekilde ayarlayın (Örn: `TimeSpan.FromSeconds(5)`). Böylece paket kaçırma (Race Condition) sorunu çözülür.

2. **IsMainServer Bayrağını Zorunlu Kılın:**
   `NetworkDiscoveryService.cs` içindeki "Localhost Kontrolü" bloğuna, sadece `appsettings.json`'daki `IsMainServer == true` ise girecek şekilde bir `if` koşulu ekleyin. Bu, yanlış bilgisayarların kendini sunucu ilan etmesini kesin olarak engeller.

3. **Hard-coded Değerleri Kaldırın:**
   `const int BROADCAST_PORT` yerine, `IOptions<NetworkDiscoverySettings>` veya `AppSettings` üzerinden `appsettings.json`'daki değerleri okuyun. Müşterinin ağında 54321 portu doluysa programı değiştirmeden portu güncelleyebilmelisiniz.
