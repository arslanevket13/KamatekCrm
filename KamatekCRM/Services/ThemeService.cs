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

        // Current loaded theme token
        public static string CurrentThemeName { get; private set; } = string.Empty;

        // List of all valid themes
        public static readonly string[] AvailableThemes = { "PremiumLight", "MidnightDark", "Glassmorphism" };

        /// <summary>
        /// Uygulamayı başlatırken ayarlanmış son temayı yükle
        /// ve WPF-UI'ın enjekte ettiği stilleri ezecek şekilde
        /// kendi ScrollBar/ScrollViewer stillerimizi en sona ekle.
        /// </summary>
        public static void Initialize()
        {
            string savedTheme = Properties.Settings.Default.ThemePreference;
            if (string.IsNullOrEmpty(savedTheme)) savedTheme = "PremiumLight";
            
            ChangeTheme(savedTheme);
            
            var app = Application.Current;
            if (app != null)
            {
                ReapplyCustomStyles(app);
            }
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
                // Fallback to PremiumLight if theme is invalid
                if (!AvailableThemes.Contains(themeName))
                {
                    themeName = "PremiumLight";
                }

                var app = Application.Current;
                if (app == null) return;

                // 2. Uygulamanın birleştirilmiş sözlüklerini (MergedDictionaries) al
                var dictionaries = app.Resources.MergedDictionaries;
                
                // 3. Mevcut aktif temayı bul
                var currentThemeDict = dictionaries.FirstOrDefault(d => 
                    d.Source != null && 
                    (d.Source.OriginalString.Contains("Resources/Themes/Theme.") || 
                     d.Source.OriginalString.Contains("Resources/Themes/LightTheme.xaml") ||
                     d.Source.OriginalString.Contains("Resources/Themes/DarkTheme.xaml")));

                // Aynı temaya geçilmek isteniyorsa ve sözlük zaten yüklüyse işlemi atla
                if (CurrentThemeName == themeName &&
                    currentThemeDict != null &&
                    currentThemeDict.Source.OriginalString.EndsWith($"Theme.{themeName}.xaml", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                // Geçerli temayı kaydet
                AppSettings.CurrentTheme = themeName;
                Properties.Settings.Default.ThemePreference = themeName;
                Properties.Settings.Default.Save();
                
                CurrentThemeName = themeName;
                
                // 1. Yeni tema sözlüğünün kaynağını hazırla
                var newThemeUri = new Uri($"Resources/Themes/Theme.{themeName}.xaml", UriKind.Relative);
                
                // 4. Yeni sözlüğü oluştur ve mevcut temanın konumuna (veya başa) ekle
                var newThemeDict = new ResourceDictionary { Source = newThemeUri };
                
                if (currentThemeDict != null)
                {
                    int index = dictionaries.IndexOf(currentThemeDict);
                    if (index >= 0)
                    {
                        dictionaries.Insert(index, newThemeDict);
                        dictionaries.Remove(currentThemeDict);
                    }
                    else
                    {
                        dictionaries.Add(newThemeDict);
                        dictionaries.Remove(currentThemeDict);
                    }
                }
                else
                {
                    dictionaries.Add(newThemeDict);
                }

                // Ensure WPF UI Theme follows our logic
                // Both MidnightDark and Glassmorphism are visually Dark themes
                var wpfUiTheme = themeName switch
                {
                    "MidnightDark" => ApplicationTheme.Dark,
                    "Glassmorphism" => ApplicationTheme.Dark,
                    _ => ApplicationTheme.Light
                };
                
                try
                {
                    ApplicationThemeManager.Apply(wpfUiTheme);
                    
                    // WPF-UI'ın enjekte ettiği ScrollBar/ScrollViewer stillerini temizle
                    // ve kendi FixedScrollBar stillerimizi yeniden yükle
                    ReapplyCustomStyles(app);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Sıfır-Hata Koruma] WPF UI Theme Manager failed to apply theme: {ex.Message}");
                }

                // Olayı tetikle
                ThemeChanged?.Invoke(null, themeName);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Sıfır-Hata Koruma] Tema değiştirme hatası: {ex.Message}");
            }
        }

        /// <summary>
        /// WPF-UI'ın ApplicationThemeManager.Apply() çağrısından sonra enjekte ettiği
        /// ScrollBar ve ScrollViewer stillerini temizler ve kendi FixedScrollBar.xaml
        /// stillerimizi MergedDictionaries'in sonuna yeniden ekler.
        /// Bu sayede "son eklenen kazanır" kuralıyla kendi stillerimiz öncelik alır.
        /// </summary>
        private static void ReapplyCustomStyles(System.Windows.Application app)
        {
            try
            {
                var dictionaries = app.Resources.MergedDictionaries;
                
                // Kendi FixedScrollBar sözlüğümüzü bul
                var fixedScrollBarUri = new Uri("Resources/FixedScrollBar.xaml", UriKind.Relative);
                var existingScrollBar = dictionaries.FirstOrDefault(d =>
                    d.Source != null && d.Source.OriginalString.Contains("FixedScrollBar.xaml"));
                
                if (existingScrollBar != null)
                {
                    // Mevcut sözlüğü kaldır
                    dictionaries.Remove(existingScrollBar);
                }
                
                // Yeniden oluştur ve EN SONA ekle (WPF-UI'ın enjekte ettiklerinden sonra)
                var freshScrollBarDict = new ResourceDictionary { Source = fixedScrollBarUri };
                dictionaries.Add(freshScrollBarDict);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Sıfır-Hata Koruma] ReapplyCustomStyles hatası: {ex.Message}");
            }
        }
    }
}
