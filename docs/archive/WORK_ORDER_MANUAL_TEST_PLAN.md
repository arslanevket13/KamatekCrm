# KAMATEK CRM — İŞ EMİRLERİ MODÜLÜ MANUEL TEST PLANI VE SENARYOLARI

**Tarih:** 04 Ağustos 2026  
**Modül:** İş Emirleri (Service Jobs)  
**Hedef:** Kullanıcı arayüzünde yapılan tüm UI, MVVM, ESC tuşu, sağ tık seçimi ve SLA filtresi düzeltmelerinin manuel doğrulanması.

---

## MANUEL TEST SENARYOLARI

### Senaryo 1: ESC Tuşu ve İptal Butonu ile Pencere Kapatma
1. **Adım:** İş Emirleri ana ekranında "+ Yeni İş Emri" butonuna tıklayın.
2. **Adım:** Açılan pencerede klavyedeki `ESC` tuşuna basın.
3. **Beklenen Sonuç:** Pencere sorunsuz şekilde kapanmalı, arka planda crash/exception oluşmamalıdır.
4. **Adım:** Yeniden "+ Yeni İş Emri" butonuna tıklayıp sağ alttaki "İptal" butonuna tıklayın.
5. **Beklenen Sonuç:** Pencere sorunsuz şekilde kapanmalıdır.

---

### Senaryo 2: Kaydet Butonu Pasif Kalma Nedeni (ToolTip Kontrolü)
1. **Adım:** "+ Yeni İş Emri" penceresini açın.
2. **Adım:** Müşteri seçmeden fareyi pasif olan "Kaydet" butonunun üzerine getirin.
3. **Beklenen Sonuç:** "Lütfen bir müşteri seçin veya hızlı müşteri ekleyin." ToolTip'i görünmelidir.
4. **Adım:** Müşteri seçin ancak İş Açıklaması alanını boş bırakıp Kaydet butonuna gelin.
5. **Beklenen Sonuç:** "İş açıklaması zorunludur." ToolTip'i görüntülenmelidir.
6. **Adım:** Tüm zorunlu alanlar doldurulduğunda Kaydet butonu aktifleşmeli ve tıklanabilir olmalıdır.

---

### Senaryo 3: DataGrid Sağ Tık (ContextMenu) Satır Seçim Doğrulaması
1. **Adım:** İş Emirleri DataGrid listesinde, seçili OLMAYAN herhangi bir iş emri satırının üzerine gelin.
2. **Adım:** Fare ile doğrudan **sağ tıklayın**.
3. **Beklenen Sonuç:** Tıklanan satır anında mavi/seçili (Selected) olmalı ve açılan ContextMenu'deki işlemler (Durum Değiştir, Detay Gör, Sil vb.) tam olarak o satırdaki iş emri için çalışmalıdır.

---

### Senaryo 4: SLA Breached (SLA Aşan) KPI Kartı ve Durum Filtreleme
1. **Adım:** İş Emirleri ekranının üstündeki KPI kartlarından "SLA Aşan İşler" kartına tıklayın.
2. **Beklenen Sonuç:** 
   - Durum Filtresi Otomatik Olarak "SLA Aşan" konumuna geçmelidir.
   - Listede yalnızca teslim/SLA süresi dolmuş ancak henüz Tamamlanmamış veya İptal Edilmemiş iş emirleri listelenmelidir.

---

### Senaryo 5: Keşif İş Emri RadioButton Seçim Düzeltmesi
1. **Adım:** Yeni İş Emri penceresini açın.
2. **Adım:** İş Tipi bölümünde "Servis İş Emri" ve "Yalnızca Keşif" radyo butonları arasında geçiş yapın.
3. **Beklenen Sonuç:** Seçimler ViewModel `IsDiscoveryOnly` durumunu doğru şekilde güncellemeli, adım 3 (Malzeme) keşif modunda otomatik gizlenip 4. adıma geçiş sağlamalıdır.

---

### Senaryo 6: İş Emri Düzenleme (Edit) Pencere İzolasyonu
1. **Adım:** DataGrid üzerindeki bir iş emrine çift tıklayın veya ContextMenu'den "Düzenle"yi seçin.
2. **Adım:** Açılan düzenleme penceresinde değişiklik yapıp İptal'e tıklayın.
3. **Beklenen Sonuç:** Ana listedeki iş emri verileri bozulmamalı; düzenleme işlemi ancak "Kaydet" denildiğinde ana listeye yansımalıdır.
