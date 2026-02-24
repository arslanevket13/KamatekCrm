using System;
using System.Windows;
using Wpf.Ui.Appearance;

namespace KamatekCrm.Services
{
    /// <summary>
    /// Tema yönetim servisi - WPF-UI entegrasyonlu basit versiyon
    /// </summary>
    public static class ThemeService
    {
        public static event EventHandler<bool>? ThemeChanged;
        
        private static bool _isDarkMode = false;
        public static bool IsDarkMode 
        { 
            get => _isDarkMode;
            private set
            {
                if (_isDarkMode != value)
                {
                    _isDarkMode = value;
                    ThemeChanged?.Invoke(null, value);
                }
            }
        }

        /// <summary>
        /// Uygulamayı başlatırken tema yükle
        /// </summary>
        public static void Initialize()
        {
            var savedTheme = Properties.Settings.Default.IsDarkMode;
            ApplyTheme(savedTheme);
        }

        /// <summary>
        /// Temayı değiştir (toggle)
        /// </summary>
        public static void ToggleTheme()
        {
            ApplyTheme(!IsDarkMode);
        }

        /// <summary>
        /// Belirtilen temayı uygula
        /// </summary>
        public static void ApplyTheme(bool darkMode)
        {
            try
            {
                var app = System.Windows.Application.Current;
                if (app == null) return;

                // WPF-UI Theme uygula - otomatik olarak tüm kontrolleri günceller
                var wpfUiTheme = darkMode ? ApplicationTheme.Dark : ApplicationTheme.Light;
                ApplicationThemeManager.Apply(wpfUiTheme);

                IsDarkMode = darkMode;

                // Tercihi kaydet
                Properties.Settings.Default.IsDarkMode = darkMode;
                Properties.Settings.Default.Save();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Tema değiştirme hatası: {ex.Message}");
            }
        }
    }
}
