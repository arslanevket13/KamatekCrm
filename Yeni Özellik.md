# KAMATEK CRM — MÜŞTERİ İLETİŞİM VE TALEP MERKEZİ

Sen, mevcut KamatekCRM kod tabanı üzerinde çalışan kıdemli bir C#/.NET yazılım mimarı ve IDE Agentısın.

Görevin; işletmede telefonla arayan müşterilerin fiyat, keşif, servis durumu, geri aranma, yöneticiyle görüşme, şikâyet, ödeme ve benzeri taleplerinin kağıt yerine sistem üzerinden kaydedilmesini ve takip edilmesini sağlayacak bir **Müşteri İletişim ve Talep Merkezi** geliştirmektir.

Bu modül yalnızca dijital not defteri olmayacaktır. Her müşteri görüşmesi:

- Bir müşteri veya arayan kişiyle ilişkilendirilmeli,
- Bir talep türüne sahip olmalı,
- Gerekirse sorumlu personele atanmalı,
- Takip tarihi içermeli,
- İlgili teklif, servis, keşif, iş emri veya finans kaydına bağlanabilmeli,
- Durumu ve sonucu takip edilebilmelidir.

---

# 1. ZORUNLU ÇALIŞMA KURALLARI

## 1.1. Önce repository incelemesi yap

Kod yazmadan önce:

1. Solution ve proje yapısını incele.
2. Kullanılan .NET sürümünü doğrula.
3. WPF, ASP.NET, API ve diğer projeleri belirle.
4. MVVM altyapısını ve kullanılan command sistemini bul.
5. Dependency Injection kayıtlarını incele.
6. Entity Framework veya kullanılan veri erişim yöntemini doğrula.
7. PostgreSQL entegrasyonunu doğrula.
8. Mevcut müşteri, kullanıcı, teklif, servis, iş emri, keşif, görev ve bildirim modellerini bul.
9. Mevcut navigation, dialog, launcher ve notification servislerini incele.
10. Mevcut audit ve durum geçmişi altyapısını araştır.
11. Mevcut test projelerini ve test kalıplarını incele.
12. Çalışma alanındaki commit edilmemiş değişiklikleri kontrol et.

Repository içinde doğrulanmayan hiçbir sınıf, tablo, servis, enum, endpoint, ViewModel veya mimari bileşeni varmış gibi kabul etme.

## 1.2. Halüsinasyon önleme

- Dosya yolunu doğrulamadan dosya adı verme.
- Bir sembolü kullanmadan önce repository genelinde ara.
- Var olmayan servisleri mevcutmuş gibi çağırma.
- Veri tabanı alanlarını entity, mapping ve migration dosyalarından doğrula.
- İş kurallarını kendiliğinden uydurma.
- Talep türlerini doğrudan sabit kod içine gömmeden önce mevcut konfigürasyon yaklaşımını incele.
- Mevcut projede uygun servis varsa paralel bir servis oluşturma.
- Belirsiz bir noktada en küçük, geri alınabilir ve mevcut davranışı koruyan çözümü seç.
- Teknik kararları görev sonu raporunda gerekçelendir.

## 1.3. Kod kalitesi

Üretilecek kod:

- Derlenebilir olmalı.
- Placeholder veya sahte implementasyon içermemeli.
- Üretim kodunda boş `TODO` bırakmamalı.
- Mevcut naming convention’a uymalı.
- Nullability kurallarına uygun olmalı.
- Dependency Injection kullanmalı.
- ViewModel içinde doğrudan veri tabanı erişimi yapmamalı.
- ViewModel içinde doğrudan `Window`, `MessageBox`, dosya sistemi veya `App.ServiceProvider` kullanılmamalı.
- Teknik exception ayrıntılarını kullanıcıya göstermemeli.
- Async I/O işlemlerinde `async/await` kullanmalı.
- Uzun işlemlerde `CancellationToken` desteklemeli.
- Çift tıklama ve paralel kayıt risklerine karşı korunmalı.
- Hassas müşteri verilerini loglara yazmamalı.
- Gereksiz NuGet paketi eklememeli.
- Kapsam dışı dosyalarda toplu formatlama yapmamalı.

## 1.4. Git güvenliği

- Kullanıcının mevcut değişikliklerini silme.
- `git reset --hard` kullanma.
- Zorlayıcı checkout işlemi yapma.
- İlgisiz dosyaları değiştirme.
- Migration geçmişini yeniden yazma.
- Veri kaybına neden olacak migration üretme.

---

# 2. İŞ HEDEFİ

İşletmedeki asistan şu an müşteri aramalarını kağıtlara not etmekte ve daha sonra patrona veya ilgili personele aktarmaktadır.

Yeni modül aşağıdaki sorunları çözmelidir:

- Kağıt notların kaybolması
- Müşteriye geri dönüşün unutulması
- Aynı müşterinin tekrar tekrar araması
- Talebin hangi personele aktarıldığının bilinmemesi
- Patronun ilgilenmesi gereken konuların karışması
- Fiyat ve keşif taleplerinin satış fırsatına dönüşmemesi
- Servis durumunu soran müşteriye doğru bilgi verilememesi
- Görüşme sonucunun takip edilememesi
- Personelin geciken görevlerinin görünmemesi
- Gün sonunda patrona düzenli özet verilememesi

Ana iş kuralı:

> Hiçbir müşteri talebi yalnızca serbest metin not olarak kalmamalıdır. Her kayıt ya görüşme sırasında sonuçlandırılmalı ya da sahibi, son tarihi ve durumu bulunan takip edilebilir bir işe dönüşmelidir.

---

# 3. MODÜL ADI

Yeni modülün kullanıcı arayüzündeki önerilen adı:

## Müşteri İletişim ve Talep Merkezi

Kod tarafındaki isimlendirmeyi mevcut projenin İngilizce veya Türkçe naming convention’ına göre belirle.

Aşağıdaki isimler yalnız kavramsal örnektir:

- Customer Interaction
- Customer Request
- Follow-up Task
- Manager Agenda
- Interaction History
- Interaction Type

Bu isimleri repository yapısını incelemeden doğrudan kullanma.

---

# 4. UYGULAMA YAKLAŞIMI

Bu işi tek seferde bütün özellikleri yazarak gerçekleştirme.

Aşağıdaki aşamaları sırayla uygula:

1. Mevcut yapı ve uyumluluk analizi
2. Domain ve veri modeli tasarımı
3. Migration ve veri erişim katmanı
4. Hızlı görüşme kayıt ekranı
5. Telefon numarasından müşteri bulma
6. Takip ve görev oluşturma
7. Bugünün aramaları ekranı
8. Müşteri timeline entegrasyonu
9. Yönetici Gündemi
10. Teklif, servis ve keşif bağlantıları
11. Hatırlatma ve gecikme kuralları
12. Günlük yönetici özeti
13. Test, performans ve güvenlik doğrulaması

Her aşama sonunda build ve test çalıştır.

Bir aşama doğrulanmadan sonraki aşamaya geçme.

---

# 5. AŞAMA 1 — MEVCUT SİSTEM ANALİZİ

İlk aşamada üretim kodunu değiştirme. Yalnızca analiz yap.

## Bulunması gereken yapılar

### Müşteri yönetimi

- Müşteri entity’si
- Firma entity’si
- İlgili kişi veya contact entity’si
- Telefon alanları
- Alternatif telefonlar
- Müşteri arama servisi
- Müşteri detay ekranı
- Müşteri timeline veya geçmiş sistemi

### Kullanıcı ve organizasyon

- Kullanıcı entity’si
- Rol ve yetki sistemi
- Personel listesi
- Şube veya organizasyon yapısı
- Aktif/pasif kullanıcı yönetimi

### Teklif sistemi

- Standart teklif
- Proje teklifi
- Teklif oluşturma servisi
- Teklif detay ekranı
- Teklif durumları
- Müşteri-teklif ilişkisi

### Servis sistemi

- Servis talebi
- İş emri
- Servis durumu
- Teknisyen
- Planlama
- Servis geçmişi

### Keşif sistemi

- Keşif talebi
- Rezervasyon
- Takvim
- Sorumlu personel
- Keşiften teklife dönüşüm

### Görev ve bildirim sistemi

- Mevcut görev entity’si
- Hatırlatma sistemi
- Notification service
- Dashboard
- Kullanıcıya atama
- Gecikme takibi

### Teknik altyapı

- DI
- Navigation
- Dialog
- Error handling
- Audit
- Logging
- Async command
- Cancellation
- Draft/autosave
- Encryption

## Analiz çıktısı

Aşağıdaki başlıklarla rapor oluştur:

### Gerçek dosya ve semboller

Her ilgili yapı için:

- Dosya yolu
- Sınıf veya sembol
- Görevle ilişkisi

### Yeniden kullanılabilecek yapılar

- Müşteri arama
- Görev
- Notification
- Audit
- Timeline
- Navigation
- User selection
- Status history

### Eksik yapılar

Gerçekten bulunmayan bileşenleri belirt.

### Riskler

- Duplicate customer
- Duplicate interaction
- Yetki açığı
- UI thread bloklama
- N+1 sorgu
- Hassas veri
- Eski mimari bağımlılıkları
- Service locator
- Memory leak

### Önerilen hedef mimari

Mevcut mimariye en az müdahale eden çözümü belirt.

Analiz tamamlandıktan sonra üretim koduna geç.

---

# 6. AŞAMA 2 — DOMAIN VE VERİ MODELİ

Mevcut modellere uyumlu bir müşteri iletişim kaydı oluştur.

Aşağıdaki alanlar kavramsal gereksinimdir. Gerçek isimleri mevcut naming convention’a göre belirle.

## 6.1. Müşteri görüşme kaydı

Görüşme kaydı aşağıdaki bilgileri desteklemelidir:

- Benzersiz kimlik
- Müşteri kimliği
- İlgili kişi kimliği
- Arayan kişinin adı
- Arayan telefon numarası
- İletişim kanalı
- Talep türü
- Konu
- Kısa özet
- Detaylı not
- Öncelik
- Durum
- Görüşme zamanı
- Kaydı oluşturan kullanıcı
- Sorumlu kullanıcı
- Takip gerekli mi?
- Takip tarihi
- Yönetici ilgisi gerekli mi?
- Tamamlanma zamanı
- Sonuç notu
- İlgili kayıt türü
- İlgili kayıt kimliği
- Oluşturma zamanı
- Güncelleme zamanı
- Concurrency bilgisi
- Arşiv veya silinme durumu

## 6.2. İletişim kanalları

Mevcut sisteme uygun biçimde desteklenebilecek kanallar:

- Telefon
- Yüz yüze
- E-posta
- WhatsApp
- Web
- Diğer

İlk sürümde yalnız gerçek ihtiyaç olan kanalları etkinleştir.

## 6.3. Talep türleri

Talep türlerini doğrudan enum içine gömmek yerine mevcut projedeki konfigürasyon yaklaşımını değerlendir.

Yönetilebilir talep türleri desteklenmeli:

- Fiyat talebi
- Teklif talebi
- Ürün bilgisi
- Stok bilgisi
- Keşif talebi
- Keşif durumu
- Servis talebi
- Servis durumu
- Teknik destek
- Randevu
- Şikâyet
- Fatura
- Ödeme
- Tahsilat
- İade
- Patronla görüşme
- Geri aranma
- İş birliği
- Tedarikçi görüşmesi
- Diğer

Talep türü aşağıdaki varsayılan davranışları tanımlayabilmelidir:

- Varsayılan sorumlu rol
- Varsayılan öncelik
- Takip gerekli mi?
- Varsayılan takip süresi
- Yöneticiye iletilsin mi?
- Hangi ek alanlar gösterilsin?
- Hangi hedef kayda dönüştürülebilir?

## 6.4. Durumlar

En az şu iş durumları desteklenmeli:

- Yeni
- Görüldü
- Atandı
- İşlemde
- Müşteriden bilgi bekleniyor
- Yönetici bekleniyor
- Planlandı
- Tamamlandı
- İptal edildi
- Gecikti

Durum geçişleri merkezi bir servis veya domain kuralıyla yönetilmeli.

UI içinde serbestçe durum değiştirilmemeli.

## 6.5. Öncelik

- Düşük
- Normal
- Yüksek
- Kritik

Yalnız renge bağlı gösterim yapma. Metin etiketi de kullan.

## 6.6. Durum ve atama geçmişi

Her önemli değişiklik audit edilmelidir:

- Önceki durum
- Yeni durum
- Önceki sorumlu
- Yeni sorumlu
- Değişikliği yapan kullanıcı
- Değişiklik zamanı
- Gerekçe
- Varsa sonuç notu

Mevcut audit altyapısı varsa onu kullan.

## 6.7. Veri tabanı kuralları

- Uygun foreign key’ler tanımla.
- Sık kullanılan filtreler için ölçülü index ekle.
- Telefon numarası aramasını destekle.
- Sorumlu, durum, takip tarihi ve oluşturma tarihi sorgularını optimize et.
- Soft delete yaklaşımı varsa kullan.
- Mevcut verileri bozma.
- Migration geri alınabilir olmalı.
- Migration build’den önce incelenmeli.
- Üretim verisini silen migration oluşturma.

---

# 7. AŞAMA 3 — HIZLI GÖRÜŞME KAYIT EKRANI

Asistanın telefon görüşmesi sırasında en fazla birkaç adımda kayıt oluşturabileceği bir WPF ekranı geliştir.

## 7.1. Ekran gereksinimleri

Ekranda şunlar bulunmalı:

- Telefon numarası
- Müşteri
- Arayan kişi
- Talep türü
- Konu
- Kısa görüşme özeti
- Detaylı not
- Öncelik
- Sorumlu personel
- Takip gerekli mi?
- Geri dönüş tarihi ve saati
- Yönetici ilgisi gerekli mi?
- İlgili mevcut kayıt
- Müşteriye verilen bilgi
- Kaydet
- Kaydet ve görev oluştur
- Kaydet ve ilgili kayda dönüştür
- İptal

## 7.2. Hızlı seçim butonları

Talep türleri için hızlı buton veya seçim yapısı oluştur:

- Fiyat istedi
- Keşif istedi
- Servis durumunu sordu
- Teklif durumunu sordu
- Patronla görüşmek istiyor
- Geri aranacak
- Şikâyet bildirdi
- Ödeme hakkında aradı
- Randevu istedi
- Teknik destek istedi
- Diğer

Butonlar talep türü konfigürasyonundan üretilebiliyorsa sabit kod yazma.

## 7.3. Kullanılabilirlik

- Ekran hızlı açılmalı.
- Klavye ile kullanılabilmeli.
- Tab sırası mantıklı olmalı.
- Telefon alanı ilk odaklanan alan olmalı.
- Enter ve klavye kısayolları mevcut UI standardına uygun kullanılmalı.
- Kayıt sırasında UI donmamalı.
- Kaydetme devam ederken buton ikinci kez çalışmamalı.
- Başarılı kayıt sonrası uygun başarı bildirimi gösterilmeli.
- Hata durumunda teknik exception gösterilmemeli.
- Kullanıcının yazdığı veri hata durumunda kaybolmamalı.

## 7.4. Global erişim

Mevcut navigasyon yapısı uygunsa bu ekran:

- Ana menüden
- Dashboard’dan
- Müşteri ekranından
- Mümkünse global klavye kısayolundan

açılabilmeli.

ViewModel doğrudan pencere oluşturmamalı.

---

# 8. AŞAMA 4 — TELEFON NUMARASINDAN MÜŞTERİ BULMA

Telefon numarası girildiğinde müşteri araması yapılmalı.

## Gereksinimler

1. Telefon numarasını normalize et.
2. Türkiye numara formatlarını destekle:
   - `05xx`
   - `5xx`
   - `+90 5xx`
   - Boşluklu
   - Parantezli
   - Tireli

3. Uluslararası numaraları bozma.
4. Ham numarayı gerekiyorsa ayrıca koru.
5. Arama şu kaynaklarda yapılmalı:
   - Müşteri ana telefonu
   - Alternatif telefonlar
   - Firma yetkilileri
   - İlgili kişiler
   - Önceki görüşme kayıtları

6. Arama debounce ile yapılmalı.
7. Her yeni arama önceki sorguyu iptal etmeli.
8. Eski sorgu sonucu yeni sonucu ezmemeli.
9. Bütün müşterileri belleğe yükleyerek arama yapma.
10. N+1 sorgu oluşturma.

## Eşleşme sonuçları

### Tek eşleşme

- Müşteriyi otomatik seç.
- Son iletişimleri göster.
- Açık teklifleri göster.
- Açık servis kayıtlarını göster.
- Açık takip görevlerini göster.

### Birden fazla eşleşme

Kullanıcıdan doğru kaydı seçmesini iste.

### Eşleşme yok

Şu seçenekleri göster:

- Yeni müşteri adayı oluştur
- Yalnız arayan kişi olarak kaydet
- Mevcut müşteriye manuel bağla
- Müşteriyi daha sonra eşleştir

Tam müşteri kartı oluşturmayı görüşme kaydı için zorunlu tutma.

---

# 9. AŞAMA 5 — GÖRÜŞME SONUCU VE DÖNÜŞÜM

Her görüşme aşağıdaki sonuçlardan biriyle tamamlanabilmeli:

- Bilgi verildi ve kapandı
- Geri arama görevi oluşturuldu
- Teklif talebi oluşturuldu
- Keşif talebi oluşturuldu
- Servis talebi oluşturuldu
- Mevcut teklif kaydına bağlandı
- Mevcut servis kaydına bağlandı
- Mevcut iş emrine bağlandı
- Yöneticiye iletildi
- Satış personeline atandı
- Teknik servise atandı
- Finans birimine atandı
- Müşteriden bilgi bekleniyor
- Sonuç bekleniyor
- İptal edildi

Dönüşüm işlemleri merkezi application service üzerinden yapılmalı.

ViewModel içinde iş kuralı oluşturma.

---

# 10. AŞAMA 6 — TAKİP VE GÖREV SİSTEMİ

Repository içinde mevcut görev altyapısı varsa onu genişlet. Aynı amaca hizmet eden ikinci bir görev sistemi oluşturma.

## Görev bilgileri

- Başlık
- Açıklama
- Müşteri
- Kaynak görüşme
- Sorumlu kullanıcı
- Oluşturan kullanıcı
- Öncelik
- Durum
- Son tarih
- Hatırlatma tarihi
- Tamamlanma zamanı
- Sonuç notu

## İş kuralları

1. “Takip gerekli” seçildiyse sorumlu ve son tarih zorunlu olmalı.
2. Atanmamış takip kaydı oluşturulmamalı veya açıkça ortak havuza düşmeli.
3. Görev tamamlandığında görüşme kaydı güncellenmeli.
4. Görüşme tamamlandığında açık görev varsa kullanıcı uyarılmalı.
5. Aynı görüşme için yanlışlıkla duplicate görev oluşturulması engellenmeli.
6. Kritik görevler daha görünür olmalı.
7. Görev sahibi değişiklikleri geçmişe yazılmalı.
8. Tamamlanmış görev tekrar açılabiliyorsa gerekçe istenmeli.

---

# 11. AŞAMA 7 — BUGÜNÜN ARAMALARI EKRANI

Asistanın gün içinde kaydettiği bütün görüşmeleri görebileceği bir ekran oluştur.

## Liste alanları

- Görüşme zamanı
- Telefon
- Müşteri
- Arayan kişi
- Talep türü
- Konu
- Öncelik
- Durum
- Sorumlu
- Takip zamanı
- İlgili kayıt
- Yönetici ilgisi
- Son güncelleme

## Filtreler

- Tarih
- Talep türü
- Durum
- Öncelik
- Sorumlu
- Müşteri
- Yönetici ilgisi
- Takip gerekenler
- Gecikenler
- Tamamlananlar
- Atanmamışlar

## İşlemler

- Kaydı aç
- Düzenle
- Sorumlu ata
- Görev oluştur
- Tamamla
- Yöneticiye ilet
- Teklife dönüştür
- Keşfe dönüştür
- Servise dönüştür
- Müşteri timeline’ını aç

Filtreleme ve sayfalama mümkün olduğunca veri tabanında yapılmalı.

---

# 12. AŞAMA 8 — MÜŞTERİ TIMELINE ENTEGRASYONU

Müşteri detay ekranındaki mevcut geçmiş veya timeline sistemini incele.

Uygun yapı varsa yeni görüşmeleri aynı timeline’a ekle.

Timeline içinde gösterilebilecek kayıtlar:

- Telefon görüşmesi
- Görüşme notu
- Görev
- Teklif
- Keşif
- Servis
- İş emri
- Şikâyet
- Yönetici notu
- Fatura veya ödeme bağlantısı

## Timeline görüşme kartı

Kartta en az şunlar gösterilmeli:

- Görüşme tarihi
- Talep türü
- Kısa özet
- Kaydı oluşturan
- Sorumlu
- Durum
- Takip tarihi
- İlgili kayıt bağlantısı
- Sonuç

Kullanıcının yetkisi olmayan finansal veya yönetim bilgilerini timeline’da gösterme.

---

# 13. AŞAMA 9 — TEKRAR ARAYAN MÜŞTERİ UYARISI

Aynı müşteri veya telefon numarası kısa süre içinde tekrar aradığında sistem uyarı göstermeli.

Örnek:

> Bu telefon numarasından son 3 gün içinde 4 görüşme kaydedildi. Açık 2 takip görevi bulunuyor.

## Kurallar

- Süre ve tekrar sayısı konfigüre edilebilir olmalı.
- Tekrar arama tek başına kritik durum sayılmamalı.
- Açık ve gecikmiş takipler özellikle gösterilmeli.
- Aynı konu birden çok kişi tarafından kaydedilmişse kullanıcı uyarılmalı.
- Uyarı kullanıcıyı kayıt oluşturmaktan engellememeli.
- Büyük veri setlerinde her tuş vuruşunda ağır sorgu çalıştırma.

---

# 14. AŞAMA 10 — YÖNETİCİ GÜNDEMİ

Patron veya yetkili yöneticiler için ayrı bir Yönetici Gündemi ekranı oluştur.

## Yöneticiye düşecek kayıtlar

- “Yönetici ilgisi gerekli” işaretli görüşmeler
- Patronla görüşmek isteyen müşteriler
- Kritik şikâyetler
- Özel fiyat veya indirim talepleri
- Büyük potansiyel işler
- Gecikmiş önemli görevler
- Çok kez arayan müşteriler
- Sonuçlandırılmamış kritik servisler
- Asistan tarafından manuel iletilen kayıtlar

## Yönetici işlemleri

- Okudum
- Kendime ata
- Başka personele ata
- Asistana geri gönder
- Müşteri arandı
- Daha sonra hatırlat
- Not ekle
- Sonuçlandır
- Teklif veya onay sürecine yönlendir

## Yetki

- Yalnız yetkili roller Yönetici Gündemi’ni görebilmeli.
- Yöneticiye özel notlar diğer kullanıcılara otomatik açılmamalı.
- Yetki kontrolü yalnız UI’da yapılmamalı.
- Servis ve veri sorgularında da uygulanmalı.

---

# 15. AŞAMA 11 — TEKLİF ENTEGRASYONU

Fiyat veya teklif talebi içeren görüşme, mevcut teklif oluşturma akışına bağlanabilmeli.

## Gereksinimler

1. Yeni teklif oluşturulurken müşteri bilgisi aktarılmalı.
2. Görüşme özeti teklif açıklamasına uygun biçimde taşınabilmeli.
3. Kaynak görüşme ile teklif arasında kalıcı bağlantı kurulmalı.
4. Görüşmede ürün bilgisi varsa doğrulanmış ürünlerle eşleştirilmeli.
5. Serbest metinden hayalî ürün oluşturma.
6. Teklif oluşturulduğunda görüşme durumunu uygun biçimde güncelle.
7. Aynı görüşmeden yanlışlıkla birden fazla teklif oluşturulmasını kontrol et.
8. Birden fazla teklif iş gereği mümkünse bunları görünür biçimde listele.
9. Teklif silinse bile görüşme geçmişini kaybetme.
10. Teklif Merkezi içinde kaynak görüşmeye erişim sağla.

---

# 16. AŞAMA 12 — KEŞİF ENTEGRASYONU

Keşif isteyen müşteri için keşif talebi veya mevcut planlama modülüne kayıt oluşturulabilmeli.

## Keşif alanları

- Müşteri
- Adres
- İlgili kişi
- Telefon
- Talep edilen hizmet
- Tercih edilen tarih
- Alternatif tarih
- Sorumlu personel
- Özel not
- Öncelik
- Kaynak görüşme

## Kurallar

- Müşteri adresini otomatik seçmeden önce kullanıcıya doğrulat.
- Keşif tarihini mevcut takvimde çakışma kontrolü yapmadan kesinleştirme.
- Aynı görüşmeden duplicate keşif oluşturmamaya dikkat et.
- Keşif tamamlanınca görüşme ve görev durumu güncellenebilmeli.
- Keşiften proje teklifine geçişte kaynak bağlantısı korunmalı.

---

# 17. AŞAMA 13 — SERVİS VE İŞ EMRİ ENTEGRASYONU

Servis durumunu soran müşteriye mevcut servis kayıtları gösterilebilmeli.

## Gereksinimler

1. Müşterinin açık servis kayıtlarını getir.
2. İş emri durumlarını göster.
3. Son işlem tarihini göster.
4. Atanan teknisyeni yalnız yetki uygunsa göster.
5. Beklenen parça bilgisi mevcutsa göster.
6. Müşteriye verilen cevabı görüşme kaydına yaz.
7. Yeni arıza bildirimi mevcut servis talebi akışına dönüştürülebilmeli.
8. Tekrar arıza durumunu önceki servisle ilişkilendirebil.
9. Aynı arıza için duplicate açık servis kaydı riskini bildir.
10. Servis kaydı oluşturulmadan sahte iş emri kimliği üretme.

---

# 18. AŞAMA 14 — GECİKME VE ESKALASYON

Takip tarihi geçen açık kayıtlar gecikmiş olarak işaretlenmeli.

## Örnek eskalasyon kuralları

- Normal kayıt: 24 saat gecikirse uyar.
- Yüksek öncelik: 4 saat gecikirse yöneticiye ilet.
- Kritik kayıt: son tarih geçince doğrudan yönetici gündemine düşür.
- Patronla görüşme talebi: tanımlı süre içinde görülmezse tekrar bildir.

Bu süreleri doğrudan kod içine gömme. Konfigüre edilebilir yapı oluştur veya mevcut ayar sistemini kullan.

## Kurallar

- Aynı gecikme için sürekli tekrar eden bildirim üretme.
- Bildirim geçmişini sakla.
- Tamamlanan kayda gecikme bildirimi gönderme.
- Kullanıcının çalışma saatleri ve mevcut takvim altyapısını değerlendir.
- Scheduler veya background service varsa onu kullan.
- Yoksa mimariye uygun ve sade çözüm öner.
- WPF uygulaması kapalıyken çalışması gereken kurallarda masaüstü timer’ına güvenme.

---

# 19. AŞAMA 15 — GÜNLÜK YÖNETİCİ ÖZETİ

Yönetici için günlük iletişim özeti oluştur.

## Özet metrikleri

- Toplam görüşme
- Yeni fiyat talebi
- Yeni teklif talebi
- Keşif talebi
- Servis durum sorgusu
- Yeni arıza bildirimi
- Patronla görüşme talebi
- Şikâyet
- Aynı görüşmede sonuçlanan
- Takibe alınan
- Geciken
- Atanmamış
- Kritik açık kayıt

## Yönetici dikkatine bölümü

Şu kayıtları öne çıkar:

- Kritik şikâyetler
- Birden fazla kez arayan müşteriler
- Büyük potansiyel işler
- Özel fiyat talepleri
- Gecikmiş keşifler
- Gecikmiş servisler
- Patronun geri araması gerekenler
- Atanmamış talepler

Özette sayıların hangi sorgu ve kurala göre hesaplandığı açıklanabilir olmalı.

Aynı metriği farklı ekranlarda farklı şekilde hesaplama.

---

# 20. TASLAK VE VERİ KAYBI KORUMASI

Görüşme kaydı sırasında uygulama kapanırsa veya bağlantı kesilirse asistanın notu kaybolmamalı.

## Gereksinimler

- Form içeriğini kontrollü autosave ile sakla.
- Her tuş vuruşunda disk veya veri tabanı yazma.
- Debounce kullan.
- UI thread’i bloklama.
- Taslağı kullanıcıya bağlı sakla.
- Hassas veriyi düz metin olarak saklama.
- Mevcut şifreleme altyapısını kullan.
- Uygulama yeniden açıldığında taslağı geri yükleme seçeneği sun.
- Başarılı kayıt sonrası taslağı temizle.
- Başka kullanıcıya ait taslağı gösterme.
- Bozuk taslağı kontrollü hata ile ele al.
- Form versiyonunu sakla.

Mevcut kurtarılabilir taslak altyapısı varsa onu genişlet.

---

# 21. BİLDİRİM VE HATA YÖNETİMİ

Mevcut merkezî hata ve bildirim altyapısı varsa onu kullan.

## Kullanıcı bildirimleri

- Kayıt başarıyla oluşturuldu.
- Görev atandı.
- Görüşme yöneticiye iletildi.
- Teklif oluşturuldu.
- Keşif planlandı.
- Servis kaydı oluşturuldu.
- Takip tarihi geçmiş.
- Kayıt başka kullanıcı tarafından değiştirilmiş.

## Teknik kurallar

- PostgreSQL exception mesajını doğrudan gösterme.
- Stack trace gösterme.
- Connection string gösterme.
- Validation hatalarını alan bazlı göster.
- Concurrency hatasında kullanıcıya kaydın değiştiğini bildir.
- Cancellation durumunu hata olarak gösterme.
- Her kritik hata için correlation ID üret veya mevcut sistemdeki karşılığını kullan.

---

# 22. ASYNC VE CONCURRENCY

## Async

- Veri tabanı sorgularında async API kullan.
- CancellationToken geçir.
- `.Result` ve `.Wait()` kullanma.
- `async void` yalnız UI event handler için kullanılabilir.
- DbContext üzerinde eş zamanlı işlem başlatma.
- View kapandıktan sonra eski sorgunun UI’yı güncellemesini engelle.

## Çift kayıt koruması

Aşağıdaki durumlara karşı koruma oluştur:

- Kaydet butonuna çift tıklama
- Aynı görüşmenin iki kez kaydedilmesi
- Aynı görüşmeden iki görev oluşması
- Aynı görüşmeden iki teklif oluşması
- Aynı görüşmeden iki keşif oluşması
- Aynı arıza için duplicate servis açılması

UI seviyesindeki busy kontrolünü tek güvenlik mekanizması olarak kullanma.

Kritik işlemlerde servis veya veri tabanı seviyesinde idempotency ve concurrency kontrolü uygula.

---

# 23. YETKİ VE KVKK

## Yetki

Kullanıcı yalnız yetkisi olan bilgileri görebilmeli.

### Asistan

- Görüşme oluşturabilir.
- Müşteri bulabilir.
- Görev atayabilir.
- Açık iş durumunu görebilir.
- Yöneticiye kayıt iletebilir.

### Satış

- Kendisine atanan fiyat ve teklif taleplerini görebilir.
- Teklife dönüştürebilir.
- Sonuç ekleyebilir.

### Teknik servis

- Kendisine atanan servis taleplerini görebilir.
- Servis veya iş emrine dönüştürebilir.
- Durum güncelleyebilir.

### Finans

- Ödeme, fatura ve tahsilat konularını görebilir.
- Yöneticiye özel notları yetkisi yoksa göremez.

### Yönetici

- Yönetici Gündemi’ni görebilir.
- Kritik kayıtları görebilir.
- Personel atayabilir.
- Sonuçlandırabilir.

## KVKK

- Gereksiz kişisel veri toplama.
- Telefon numaralarını loglarda maskele.
- Hassas görüşme notlarını yetkisiz kullanıcılardan koru.
- Silme ve saklama politikalarına mevcut KVKK mimarisi varsa uy.
- Ses kaydı özelliğini bu ilk geliştirme kapsamında ekleme.
- WhatsApp veya santral entegrasyonunu ilk MVP kapsamında ekleme.

---

# 24. MVP KAPSAMI

İlk sürümde mutlaka tamamlanması gereken özellikler:

1. Görüşme veri modeli
2. Migration
3. Hızlı görüşme kayıt ekranı
4. Telefon numarasından müşteri arama
5. Talep türü
6. Kısa ve detaylı not
7. Öncelik
8. Sorumlu atama
9. Takip tarihi
10. Görev oluşturma
11. Bugünün aramaları
12. Müşteri timeline entegrasyonu
13. Yönetici ilgisi işareti
14. Yönetici Gündemi
15. Durum geçmişi
16. Audit
17. Geciken görev uyarısı
18. Teklif, keşif veya servis kaydına bağlantı
19. Çift tıklama koruması
20. Taslak koruması
21. Build ve testler

## MVP dışında bırakılacaklar

İlk sürümde aşağıdakileri geliştirme:

- Telefon santrali entegrasyonu
- Otomatik çağrı kaydı
- Ses kaydını yazıya çevirme
- Duygu analizi
- Yapay zekâ ile otomatik karar verme
- WhatsApp entegrasyonu
- E-posta entegrasyonu
- Gelişmiş konuşma analizi
- Otomatik müşteri puanlama
- Haricî bildirim servisleri

Bu özellikler için yalnız genişletilebilir mimari bırak.

---

# 25. TEST SENARYOLARI

## Müşteri arama testleri

- Kayıtlı telefon numarası
- Alternatif telefon numarası
- Firma yetkilisi telefonu
- Farklı telefon formatları
- Birden fazla eşleşme
- Eşleşmeyen numara
- İptal edilen arama
- Eski sorgunun yeni sonucu ezmemesi

## Görüşme kayıt testleri

- Minimum zorunlu alanlarla kayıt
- Müşterisiz arayan kişi kaydı
- Takip gerektirmeyen görüşme
- Takip gerektiren görüşme
- Eksik sorumlu
- Eksik takip tarihi
- Yönetici ilgisi
- Kritik öncelik
- Duplicate kayıt
- Concurrency çakışması

## Görev testleri

- Görev oluşturma
- Görev atama
- Görev tamamlama
- Sorumlu değiştirme
- Son tarih geçmesi
- Duplicate görev
- Tamamlanmış görevin yeniden açılması
- Yetkisiz görev görüntüleme

## Entegrasyon testleri

- Görüşmeden teklif oluşturma
- Görüşmeden keşif oluşturma
- Görüşmeden servis oluşturma
- Mevcut teklife bağlama
- Mevcut servis kaydına bağlama
- Müşteri timeline gösterimi
- Yönetici Gündemi
- Kaynak kayıt silinmişken görüşme geçmişi

## UI testleri

- Hızlı kayıt ekranı açılması
- Klavye navigasyonu
- Busy state
- Çift tıklama
- Hata sonrası form verisinin korunması
- Başarılı kayıttan sonra temizleme
- View kapanırken cancellation
- Tekrar arayan müşteri uyarısı

## Güvenlik testleri

- Yetkisiz yönetici gündemi erişimi
- Yetkisiz finansal bilgi erişimi
- Başka kullanıcıya ait taslak
- Telefon numarasının loglarda görünmemesi
- Teknik exception’ın kullanıcıya sızmaması

---

# 26. PERFORMANS KRİTERLERİ

- Telefon araması bütün müşteri tablosunu belleğe yüklememeli.
- Liste sorgularında sayfalama kullanılmalı.
- Filtreleme mümkün olduğunca veri tabanında yapılmalı.
- N+1 sorgular önlenmeli.
- Telefon normalizasyonu index kullanımını tamamen engellememeli.
- Timeline sorgusu bütün müşteri geçmişini kontrolsüz yüklememeli.
- Dashboard metrikleri ayrı ayrı ağır sorgular üretmemeli.
- Arama debounce kullanılmalı.
- Cancellation desteklenmeli.
- UI thread uzun işlemlerle bloklanmamalı.

---

# 27. KABUL KRİTERLERİ

Görev aşağıdaki koşullar sağlandığında tamamlanmış kabul edilir:

1. Asistan yeni görüşmeyi hızlı biçimde kaydedebiliyor.
2. Telefon numarasıyla mevcut müşteri bulunabiliyor.
3. Eşleşmeyen numara için müşteri oluşturmadan kayıt yapılabiliyor.
4. Talep türü ve öncelik seçilebiliyor.
5. Görüşme ilgili personele atanabiliyor.
6. Takip tarihi belirlenebiliyor.
7. Takip görevi oluşturulabiliyor.
8. Bugünün görüşmeleri listelenebiliyor.
9. Geciken görüşmeler görülebiliyor.
10. Yöneticiye iletilen kayıtlar Yönetici Gündemi’nde görünüyor.
11. Görüşme müşteri timeline’ında görünüyor.
12. Görüşme teklif, keşif veya servis kaydına bağlanabiliyor.
13. Durum ve atama geçmişi tutuluyor.
14. Yetki kontrolleri servis katmanında uygulanıyor.
15. Çift kayıt koruması çalışıyor.
16. Bağlantı veya uygulama kapanması durumunda taslak kaybolmuyor.
17. Migration veri kaybı oluşturmuyor.
18. Solution başarılı derleniyor.
19. Otomatik testler başarılı çalışıyor.
20. Mevcut müşteri, teklif ve servis ekranları bozulmuyor.

---

# 28. ZORUNLU GÖREV SONU RAPORU

Çalışma sonunda aşağıdaki formatta rapor ver:

## İncelenen gerçek dosyalar

Her dosya için:

- Dosya yolu
- İlgili sınıf veya sembol
- Görevle ilişkisi

## Tespit edilen mevcut mimari

- Müşteri yapısı
- Görev yapısı
- Timeline yapısı
- Teklif entegrasyonu
- Servis entegrasyonu
- Navigation
- Notification
- Audit
- Error handling
- Draft sistemi

## Mimari kararlar

Her önemli karar için:

- Seçilen yaklaşım
- Gerekçe
- Değerlendirilen alternatifler
- Mevcut sistemle uyumluluk

## Yapılan değişiklikler

Dosya bazında:

- Eklenen dosyalar
- Değiştirilen dosyalar
- Eklenen sınıflar
- Eklenen DI kayıtları
- Eklenen navigation kayıtları
- Eklenen migration

## Veri tabanı

- Yeni tablolar
- Yeni kolonlar
- Foreign key’ler
- Index’ler
- Migration adı
- Veri kaybı riski
- Geri alma yöntemi

## Testler

- Eklenen testler
- Çalıştırılan test komutları
- Başarılı testler
- Başarısız testler
- Manuel test senaryoları

## Build sonucu

- Çalıştırılan komut
- Başarılı veya başarısız
- Yeni warning oluştu mu?
- Mevcut warning’ler değişti mi?

## Kalan riskler

- Tamamlanmayan noktalar
- Teknik borçlar
- Kapsam dışında bırakılan özellikler
- Sonraki geliştirme önerileri

## Sonuç

Açıkça belirt:

- MVP tamamlandı mı?
- Hangi kabul kriterleri sağlandı?
- Hangi kabul kriterleri sağlanmadı?
- Program mevcut işlevleriyle çalışmaya devam ediyor mu?

---

# 29. UYGULAMA SIRASI

Çalışmayı şu sırada yürüt:

1. Repository analizi
2. Uyumluluk raporu
3. Veri modeli
4. Migration
5. Repository/query/application servisleri
6. Hızlı görüşme kayıt ViewModel’i
7. Hızlı görüşme kayıt View’i
8. Telefon arama
9. Görev bağlantısı
10. Bugünün aramaları
11. Timeline
12. Yönetici Gündemi
13. Teklif entegrasyonu
14. Keşif entegrasyonu
15. Servis entegrasyonu
16. Gecikme kuralları
17. Taslak koruması
18. Yetki kontrolleri
19. Otomatik testler
20. Build
21. Manuel doğrulama
22. Görev sonu raporu

Her adımda mevcut çalışan davranışı koru.

Bu prompt kapsamında doğrulanmamış büyük mimari yeniden yazım yapma.

Görevi tamamladıktan sonra kendiliğinden yeni modüller geliştirmeye geçme.