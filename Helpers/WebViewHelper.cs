using Microsoft.Web.WebView2.Wpf;
using System;
using System.Windows;

namespace KamatekCrm.Helpers
{
    /// <summary>
    /// Bu sınıf, HTML kodunu direkt WebView2'ye bağlamamızı sağlar.
    /// WebView2'nin başlatılmasını bekler ve güvenli şekilde navigasyon yapar.
    /// </summary>
    public static class WebViewHelper
    {
        public static readonly DependencyProperty HtmlContentProperty =
            DependencyProperty.RegisterAttached(
                "HtmlContent",
                typeof(string),
                typeof(WebViewHelper),
                new PropertyMetadata(null, OnHtmlContentChanged));

        public static string GetHtmlContent(DependencyObject obj)
        {
            return (string)obj.GetValue(HtmlContentProperty);
        }

        public static void SetHtmlContent(DependencyObject obj, string value)
        {
            obj.SetValue(HtmlContentProperty, value);
        }

        private static async void OnHtmlContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is WebView2 webView && e.NewValue is string html && !string.IsNullOrEmpty(html))
            {
                try
                {
                    // WebView2 henüz başlatılmamışsa bekle
                    if (webView.CoreWebView2 == null)
                    {
                        await webView.EnsureCoreWebView2Async();
                    }

                    // Başlatma tamamlandıktan sonra HTML'e git
                    webView.NavigateToString(html);
                }
                catch (ObjectDisposedException)
                {
                    // Pencere kapanırken WebView2 dispose edilmiş olabilir - yoksay
                }
                catch (InvalidOperationException)
                {
                    // WebView2 henüz hazır değil veya kapanıyor - yoksay
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"WebViewHelper Error: {ex.Message}");
                }
            }
        }
    }
}