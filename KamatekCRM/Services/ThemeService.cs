using System;
using System.Linq;
using System.Windows;
using Wpf.Ui.Appearance;
using KamatekCrm.Settings;

namespace KamatekCrm.Services
{
    /// <summary>
    /// Tema yönetim servisi - Dinamik tema geçişleri (PremiumLight, MidnightDark, Glassmorphism)
    /// </summary>
    public static class ThemeService
    {
        public static event EventHandler<string>? ThemeChanged;

        public static string CurrentTheme => AppSettings.CurrentTheme;

        /// <summary>
        /// Uygulamayı başlatırken ayarlanmış son temayı yükle
        /// </summary>
        public static void Initialize()
        {
            ChangeTheme(CurrentTheme);
            
            // WPF-UI için dark mode uyumluluğu
            var isDark = CurrentTheme == "MidnightDark";
            ApplicationThemeManager.Apply(isDark ? ApplicationTheme.Dark : ApplicationTheme.Light);
        }

        /// <summary>
        /// Çalışma zamanında (runtime) temayı değiştirir. 
        /// Hata fırlatmaması için tam güvenlikli kontroller içerir.
        /// </summary>
        /// <param name="themeName">PremiumLight, MidnightDark, Glassmorphism</param>
        public static void ChangeTheme(string themeName)
        {
            try
            {
                var app = Application.Current;
                if (app == null) return;

                // Geçerli temayı kaydet
                AppSettings.CurrentTheme = themeName;
                
                // 1. Yeni tema sözlüğünün kaynağını hazırla
                var newThemeUri = new Uri($"Resources/Themes/Theme.{themeName}.xaml", UriKind.Relative);
                
                // 2. Uygulamanın birleştirilmiş sözlüklerini (MergedDictionaries) al
                var dictionaries = app.Resources.MergedDictionaries;
                
                // 3. Mevcut aktif temayı bul
                var currentThemeDict = dictionaries.FirstOrDefault(d => 
                    d.Source != null && 
                    (d.Source.OriginalString.Contains("Resources/Themes/Theme.") || 
                     d.Source.OriginalString.Contains("Resources/Themes/LightTheme.xaml") ||
                     d.Source.OriginalString.Contains("Resources/Themes/DarkTheme.xaml")));

                // Aynı temaya geçilmek isteniyorsa işlemi atla
                if (currentThemeDict != null && currentThemeDict.Source.OriginalString.EndsWith($"Theme.{themeName}.xaml"))
                {
                    return;
                }

                // 4. Yeni sözlüğü oluştur ve ekle
                var newThemeDict = new ResourceDictionary { Source = newThemeUri };
                
                // Kesin geçiş güvenliği: Önce ekle, sonra eskisini sil (flicker veya Dictionary KeyNotFound crash riskini önlemek için)
                dictionaries.Add(newThemeDict);
                
                if (currentThemeDict != null)
                {
                    dictionaries.Remove(currentThemeDict);
                }

                // 5. WPF-UI tema senkronizasyonu (Gölge, Scrollbar ve Popup kontrollerinin karanlık durumu için)
                var isDark = themeName == "MidnightDark";
                ApplicationThemeManager.Apply(isDark ? ApplicationTheme.Dark : ApplicationTheme.Light);

                // Olayı tetikle
                ThemeChanged?.Invoke(null, themeName);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Sıfır-Hata Koruma] Tema değiştirme hatası: {ex.Message}");
            }
        }
    }
}
