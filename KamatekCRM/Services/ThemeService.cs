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
        public static string CurrentThemeName { get; private set; } = "PremiumLight";

        // List of all valid themes
        public static readonly string[] AvailableThemes = { "PremiumLight", "MidnightDark", "Glassmorphism" };

        /// <summary>
        /// Uygulamayı başlatırken ayarlanmış son temayı yükle
        /// </summary>
        public static void Initialize()
        {
            ChangeTheme(AppSettings.CurrentTheme);
            
            // WPF-UI için dark mode uyumluluğu - This is now handled by ChangeTheme
            // var isDark = CurrentTheme == "MidnightDark";
            // ApplicationThemeManager.Apply(isDark ? ApplicationTheme.Dark : ApplicationTheme.Light);
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
                    // Assuming Log is a static class available in the project
                    // If not, this line will cause a compilation error and needs to be adapted
                    // For example, System.Diagnostics.Debug.WriteLine($"ThemeService: Invalid theme '{themeName}' requested. Falling back to PremiumLight.");
                    // Log.Warning("ThemeService: Invalid theme '{ThemeName}' requested. Falling back to PremiumLight.", themeName);
                    themeName = "PremiumLight";
                }

                if (CurrentThemeName == themeName) return;

                // Assuming Log is a static class available in the project
                // Log.Information("ThemeService: Switching theme to {ThemeName}", themeName);

                var app = Application.Current;
                if (app == null) return;

                // Geçerli temayı kaydet
                AppSettings.CurrentTheme = themeName;
                CurrentThemeName = themeName; // Update the internal current theme name
                
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
                    // Remove auto-injected Wpf.Ui dictionaries to prevent our dynamic styles from being overridden.
                    // PurgeWpfUiDictionaries(App.Current.Resources); // This method is not defined in the provided context.
                }
                catch (Exception ex)
                {
                    // Assuming Log is a static class available in the project
                    // Log.Warning(ex, "ThemeService: WPF UI Theme Manager failed to apply theme.");
                    System.Diagnostics.Debug.WriteLine($"[Sıfır-Hata Koruma] WPF UI Theme Manager failed to apply theme: {ex.Message}");
                }

                // 5. WPF-UI tema senkronizasyonu (Gölge, Scrollbar ve Popup kontrollerinin karanlık durumu için)
                // This block is replaced by the new WPF UI Theme logic above
                // var isDark = themeName == "MidnightDark";
                // ApplicationThemeManager.Apply(isDark ? ApplicationTheme.Dark : ApplicationTheme.Light);

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
