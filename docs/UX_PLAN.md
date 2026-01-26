# UX/UI Modernizasyon Planı

Kullanıcı deneyimini "90'lar hantallığından" kurtarıp modern, klavye odaklı ve hızlı tepki veren bir yapıya geçirmek için aşağıdaki adımlar uygulanacaktır.

## 1. Klavye Hakimiyeti (Keyboard Supremacy)
Hedef: Formların mouse kullanmadan doldurulup kaydedilebilmesi.
- **DirectSalesWindow.xaml** ve **NewServiceJobWindow.xaml** (ve varsa diğer dialoglar):
  - Kaydet butonlarına `IsDefault="True"` (Enter tuşu).
  - İptal butonlarına `IsCancel="True"` (Esc tuşu).
  - TextBox ve ComboBox elemanlarına mantıklı `TabIndex` sıralaması.

## 2. Hata Geçirmez Girişler (Idiot-Proof Inputs)
Hedef: Sayısal alanlara harf girişini engellemek ve hataları görselleştirmek.
- **Styles.xaml**:
  - `NumericTextBoxStyle` veya Behavior eklenecek (`PreviewTextInput` event listener ile).
  - Validation hatalarında TextBox çerçevesini kırmızı yapan stil (`ErrorTemplate`).

## 3. Akışkan Izgaralar (Fluid Grids)
Hedef: Listeler üzerinde hızlı işlem (Çift Tıkla -> Detay, Del -> Sil).
- **CustomersView.xaml**, **ProductsView.xaml**, **FieldJobListView.xaml**:
  - DataGrid `InputBindings`:
    - `MouseBinding Gesture="LeftDoubleClick" Command="{Binding EditCommand}"`
    - `KeyBinding Key="Delete" Command="{Binding DeleteCommand}"`

## 4. Anlık Geri Bildirim (Instant Feedback) (Opsiyonel/Sonraki Adım)
- İşlemler sırasında `IsBusy` göstergesi ve işlem sonunda `ToastNotification` entegrasyonu (Mevcut Toast yapısı güçlendirilecek).

## Uygulama Sırası
1. **Styles.xaml**: Global stillerin güncellenmesi (Numeric Text, Error Template).
2. **CustomersView.xaml & ProductsView.xaml**: DataGrid event binding'leri.
3. **DirectSalesWindow.xaml & NewServiceJobWindow.xaml**: Dialog pencerelerinin klavye optimizasyonu.
4. **Build & Verify**.

---
**Not:** WPF'te `LeftDoubleClick` için `MouseBinding` kullanırken `InputBindings` DataGrid seviyesinde mi yoksa `ItemContainerStyle` içinde mi olmalı kontrol edilecek. Genellikle `RowStyle` içinde tanımlanması gerekir.
