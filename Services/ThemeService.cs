using System;
using System.Windows;

namespace KamatekCrm.Services
{
    /// <summary>
    /// Tema yönetim servisi - Dark/Light mode geçişi
    /// </summary>
    public static class ThemeService
    {
        private const string ThemeSettingKey = "AppTheme";
        private const string LightThemePath = "Resources/Themes/LightTheme.xaml";
        private const string DarkThemePath = "Resources/Themes/DarkTheme.xaml";

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
            // Kayıtlı tema tercihini oku
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

                // Mevcut tema dictionary'sini bul ve kaldır
                ResourceDictionary? oldTheme = null;
                foreach (var dict in app.Resources.MergedDictionaries)
                {
                    if (dict.Source != null && 
                        (dict.Source.OriginalString.Contains("LightTheme") || 
                         dict.Source.OriginalString.Contains("DarkTheme")))
                    {
                        oldTheme = dict;
                        break;
                    }
                }

                if (oldTheme != null)
                {
                    app.Resources.MergedDictionaries.Remove(oldTheme);
                }

                // Yeni temayı yükle
                var themePath = darkMode ? DarkThemePath : LightThemePath;
                var newTheme = new ResourceDictionary
                {
                    Source = new Uri(themePath, UriKind.Relative)
                };
                
                // Temayı en başa ekle (öncelik için)
                app.Resources.MergedDictionaries.Insert(0, newTheme);

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
