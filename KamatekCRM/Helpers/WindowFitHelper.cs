using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace KamatekCrm.Helpers
{
    /// <summary>
    /// Pencereyi açıldığı monitörün çalışma alanına sığdırır.
    ///
    /// Neden: pencerelerin tasarım ölçüleri (1180–1450 px) 1366×768 @%125 ölçekli
    /// dizüstülerde (~1093×614 DIP) ve 1024×768 ekranlarda çalışma alanını aşıyor;
    /// pencere ekran dışına taşıyor ve alt/kenar düğmelerine erişilemiyordu.
    ///
    /// Davranış: yalnızca pencere çalışma alanından büyükse küçültür ve ekran içine
    /// ortalar. Büyük ekranlarda tasarım ölçüsü aynen korunur. MinWidth/MinHeight
    /// sınırlarına dokunmaz. Hiçbir pencereye XAML değişikliği gerekmez.
    /// </summary>
    public static class WindowFitHelper
    {
        /// <summary>Çalışma alanı kenarlarında bırakılan güvenli boşluk (DIP).</summary>
        private const double EdgeMargin = 12;

        private const uint MonitorDefaultToNearest = 2;

        private static bool _isRegistered;

        /// <summary>
        /// Tüm pencereler için sığdırma davranışını bir kez kaydeder.
        /// App başlangıcında çağrılmalıdır.
        /// </summary>
        public static void Setup()
        {
            if (_isRegistered) return;
            _isRegistered = true;

            EventManager.RegisterClassHandler(
                typeof(Window),
                FrameworkElement.LoadedEvent,
                new RoutedEventHandler(OnWindowLoaded));
        }

        private static void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is not Window window) return;

            // Yalnızca normal durumdaki pencereler; maximize/minimize kullanıcı tercihidir.
            if (window.WindowState != WindowState.Normal) return;

            // Otomatik boyutlandırılan pencerelerde genişlik/yükseklik ataması
            // SizeToContent davranışını bozacağı için dokunulmaz.
            if (window.SizeToContent != SizeToContent.Manual) return;

            Fit(window);
        }

        private static void Fit(Window window)
        {
            try
            {
                var area = GetWorkArea(window);
                if (area.Width <= 0 || area.Height <= 0) return;

                var maxWidth = Math.Max(window.MinWidth, area.Width - EdgeMargin * 2);
                var maxHeight = Math.Max(window.MinHeight, area.Height - EdgeMargin * 2);

                var width = double.IsNaN(window.Width) ? window.ActualWidth : window.Width;
                var height = double.IsNaN(window.Height) ? window.ActualHeight : window.Height;
                if (width <= 0 || height <= 0) return;

                var targetWidth = Math.Min(width, maxWidth);
                var targetHeight = Math.Min(height, maxHeight);

                if (Math.Abs(targetWidth - width) > 0.5 || Math.Abs(targetHeight - height) > 0.5)
                {
                    window.Width = targetWidth;
                    window.Height = targetHeight;
                }

                // Pencere (küçültme sonrası) çalışma alanının dışında kalıyorsa ortala.
                var right = window.Left + targetWidth;
                var bottom = window.Top + targetHeight;
                if (window.Left < area.Left || window.Top < area.Top ||
                    right > area.Right || bottom > area.Bottom)
                {
                    window.Left = area.Left + Math.Max(0, (area.Width - targetWidth) / 2);
                    window.Top = area.Top + Math.Max(0, (area.Height - targetHeight) / 2);
                }
            }
            catch
            {
                // Sığdırma başarısız olursa pencere mevcut konumunda kalır; uygulama akışı bozulmaz.
            }
        }

        /// <summary>
        /// Pencerenin bulunduğu monitörün çalışma alanını DIP cinsinden döndürür.
        /// Win32 erişimi başarısız olursa birincil ekrana düşer.
        /// </summary>
        private static Rect GetWorkArea(Window window)
        {
            try
            {
                var handle = new WindowInteropHelper(window).Handle;
                if (handle != IntPtr.Zero)
                {
                    var monitor = MonitorFromWindow(handle, MonitorDefaultToNearest);
                    if (monitor != IntPtr.Zero)
                    {
                        var info = new MonitorInfo { cbSize = Marshal.SizeOf<MonitorInfo>() };
                        if (GetMonitorInfo(monitor, ref info))
                        {
                            // rcWork fiziksel piksel; WPF ölçüleri DIP olduğu için dönüştürülür.
                            var toDip = PresentationSource.FromVisual(window)
                                ?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;

                            var topLeft = toDip.Transform(new Point(info.rcWork.Left, info.rcWork.Top));
                            var bottomRight = toDip.Transform(new Point(info.rcWork.Right, info.rcWork.Bottom));
                            return new Rect(topLeft, bottomRight);
                        }
                    }
                }
            }
            catch
            {
                // Aşağıdaki birincil ekran yedeğine düşülür.
            }

            return SystemParameters.WorkArea;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MonitorInfo
        {
            public int cbSize;
            public NativeRect rcMonitor;
            public NativeRect rcWork;
            public uint dwFlags;
        }
    }
}
