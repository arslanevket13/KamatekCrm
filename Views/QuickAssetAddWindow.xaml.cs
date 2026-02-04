using System.Windows;
using KamatekCrm.Data;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;

namespace KamatekCrm.Views
{
    /// <summary>
    /// Hızlı cihaz ekleme penceresi
    /// </summary>
    public partial class QuickAssetAddWindow : Window
    {
        private readonly int _customerId;

        /// <summary>
        /// Oluşturulan cihaz (Kaydetme başarılı ise)
        /// </summary>
        public CustomerAsset? CreatedAsset { get; private set; }

        public QuickAssetAddWindow(int customerId)
        {
            InitializeComponent();
            _customerId = customerId;
            CategoryCombo.SelectedIndex = 0;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // Validasyon
            if (CategoryCombo.SelectedItem == null)
            {
                MessageBox.Show("Lütfen kategori seçin.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var brand = BrandCombo.Text?.Trim();
            if (string.IsNullOrEmpty(brand))
            {
                MessageBox.Show("Lütfen marka girin.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var model = ModelBox.Text?.Trim();
            if (string.IsNullOrEmpty(model))
            {
                MessageBox.Show("Lütfen model girin.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Kategori al
            var categoryItem = (System.Windows.Controls.ComboBoxItem)CategoryCombo.SelectedItem;
            var categoryTag = categoryItem.Tag?.ToString() ?? "CCTV";
            var category = categoryTag switch
            {
                "CCTV" => JobCategory.CCTV,
                "VideoIntercom" => JobCategory.VideoIntercom,
                "FireAlarm" => JobCategory.FireAlarm,
                "BurglarAlarm" => JobCategory.BurglarAlarm,
                "SmartHome" => JobCategory.SmartHome,
                "AccessControl" => JobCategory.AccessControl,
                "SatelliteSystem" => JobCategory.SatelliteSystem,
                "FiberOptic" => JobCategory.FiberOptic,
                _ => JobCategory.CCTV
            };

            try
            {
                using var context = new AppDbContext();

                var asset = new CustomerAsset
                {
                    CustomerId = _customerId,
                    Category = category,
                    Brand = brand,
                    Model = model,
                    SerialNumber = SerialBox.Text?.Trim(),
                    Location = LocationBox.Text?.Trim(),
                    Status = AssetStatus.Active,
                    CreatedDate = System.DateTime.Now
                };

                context.CustomerAssets.Add(asset);
                context.SaveChanges();

                CreatedAsset = asset;
                DialogResult = true;
                Close();
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Kayıt hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
