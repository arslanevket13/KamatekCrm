using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Wpf.Ui.Appearance;
using Wpf.Ui.Markup;
using KamatekCrm.Settings;

namespace KamatekCrm.Services
{
    /// <summary>
    /// Tema yönetim servisi - Dinamik tema geçişleri (PremiumLight, MidnightDark, Glassmorphism).
    /// Çalışma zamanında yalnızca aktif Theme.*.xaml sözlüğünü (ve WPF-UI temel tema tonunu) değiştirir;
    /// DesignTokens, UxFoundation, Styles, ComponentStyles ve FixedScrollBar sözlüklerinin sırasına dokunmaz.
    /// </summary>
    public static class ThemeService
    {
        public static event EventHandler<string>? ThemeChanged;

        public static string CurrentThemeName { get; private set; } = string.Empty;

        public static readonly string[] AvailableThemes = { "PremiumLight", "MidnightDark", "Glassmorphism" };

        /// <summary>
        /// Aktif tema sözlüğünden Brush kaynağını çözümler; bulunamazsa yedek fırçayı döndürür.
        /// ViewModel katmanında hardcoded hex string yerine tema duyarlı fırça döndürmek için kullanılır.
        /// </summary>
        public static Brush ResolveBrush(string resourceKey, Brush? fallback = null)
        {
            if (Application.Current?.TryFindResource(resourceKey) is Brush brush)
            {
                return brush;
            }

            return fallback ?? Brushes.Gray;
        }

        public static void Initialize()
        {
            string savedTheme = Properties.Settings.Default.ThemePreference;
            if (string.IsNullOrWhiteSpace(savedTheme) || !AvailableThemes.Contains(savedTheme))
            {
                savedTheme = AppSettings.CurrentTheme;
            }
            if (string.IsNullOrWhiteSpace(savedTheme) || !AvailableThemes.Contains(savedTheme))
            {
                savedTheme = "PremiumLight";
            }

            ChangeTheme(savedTheme);
        }

        /// <summary>
        /// Çalışma zamanında (runtime) yalnızca aktif tema sözlüğünü değiştirir.
        /// </summary>
        /// <param name="themeName">PremiumLight, MidnightDark, Glassmorphism</param>
        public static void ChangeTheme(string themeName)
        {
            try
            {
                if (!AvailableThemes.Contains(themeName))
                {
                    themeName = "PremiumLight";
                }

                var app = Application.Current;
                if (app == null) return;

                var dictionaries = app.Resources.MergedDictionaries;

                var currentThemeDict = dictionaries.FirstOrDefault(d =>
                    d.Source != null &&
                    d.Source.OriginalString.Contains("Resources/Themes/Theme.", StringComparison.OrdinalIgnoreCase));

                if (CurrentThemeName == themeName &&
                    currentThemeDict != null &&
                    currentThemeDict.Source != null &&
                    currentThemeDict.Source.OriginalString.EndsWith($"Theme.{themeName}.xaml", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                AppSettings.CurrentTheme = themeName;
                Properties.Settings.Default.ThemePreference = themeName;
                Properties.Settings.Default.Save();

                CurrentThemeName = themeName;

                var wpfUiTheme = themeName switch
                {
                    "MidnightDark" => ApplicationTheme.Dark,
                    "Glassmorphism" => ApplicationTheme.Dark,
                    _ => ApplicationTheme.Light
                };

                // WPF-UI'ın mevcut ThemesDictionary örneğini yerinde güncelle (sıralamayı bozmadan)
                var wpfUiThemeDict = dictionaries.OfType<ThemesDictionary>().FirstOrDefault();
                if (wpfUiThemeDict != null)
                {
                    try
                    {
                        wpfUiThemeDict.Theme = wpfUiTheme;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[Sıfır-Hata Koruma] WPF UI ThemesDictionary update failed: {ex.Message}");
                    }
                }

                // Yalnızca aktif Theme.*.xaml sözlüğünü yerinde değiştir
                var newThemeUri = new Uri($"pack://application:,,,/KamatekCrm;component/Resources/Themes/Theme.{themeName}.xaml", UriKind.Absolute);
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
                    // Fallback: Styles/DesignTokens veya UxFoundation öncesine ekle
                    var afterThemeDict = dictionaries.FirstOrDefault(d =>
                        d.Source != null &&
                        (d.Source.OriginalString.Contains("Styles/DesignTokens.xaml", StringComparison.OrdinalIgnoreCase) ||
                         d.Source.OriginalString.Contains("UxFoundation.xaml", StringComparison.OrdinalIgnoreCase)));
                    int insertIndex = afterThemeDict != null ? dictionaries.IndexOf(afterThemeDict) : dictionaries.Count;
                    dictionaries.Insert(Math.Max(0, insertIndex), newThemeDict);
                }

                ThemeChanged?.Invoke(null, themeName);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Sıfır-Hata Koruma] Tema değiştirme hatası: {ex.Message}");
            }
        }
    }
}
