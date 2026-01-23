using System.ComponentModel;
using KamatekCrm.Enums;

namespace KamatekCrm.Models
{
    /// <summary>
    /// Kategori çoklu seçimi için wrapper sınıfı
    /// CheckBox binding için kullanılır
    /// </summary>
    public class CategorySelectItem : INotifyPropertyChanged
    {
        private bool _isSelected;

        /// <summary>
        /// Kategori enum değeri
        /// </summary>
        public JobCategory Category { get; set; }

        /// <summary>
        /// Kategori adı (UI için)
        /// </summary>
        public string DisplayName => Category switch
        {
            JobCategory.CCTV => "📹 Güvenlik Kamera",
            JobCategory.VideoIntercom => "📞 Görüntülü Diafon",
            JobCategory.FireAlarm => "🔥 Yangın Alarm",
            JobCategory.BurglarAlarm => "🚨 Hırsız Alarm",
            JobCategory.SmartHome => "🏠 Akıllı Ev",
            JobCategory.AccessControl => "🔐 Kartlı Geçiş (PDKS)",
            JobCategory.SatelliteSystem => "📡 Uydu Sistemleri",
            JobCategory.FiberOptic => "🔌 Fiber Optik",
            _ => Category.ToString()
        };

        /// <summary>
        /// Seçili mi?
        /// </summary>
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
