using System;
using System.Windows;
using System.Windows.Controls;

namespace KamatekCrm.Helpers
{
    public static class WindowControlHelper
    {
        public static void SetupWindowControls(Window window)
        {
            if (window == null || window.Template == null) return;

            if (window.Template.FindName("PART_CloseButton", window) is Button closeBtn)
            {
                closeBtn.Click -= CloseBtn_Click;
                closeBtn.Click += CloseBtn_Click;
            }

            if (window.Template.FindName("PART_MinimizeButton", window) is Button minBtn)
            {
                minBtn.Click -= MinBtn_Click;
                minBtn.Click += MinBtn_Click;
            }

            if (window.Template.FindName("PART_MaximizeButton", window) is Button maxBtn)
            {
                maxBtn.Click -= MaxBtn_Click;
                maxBtn.Click += MaxBtn_Click;
                
                window.StateChanged -= Window_StateChanged;
                window.StateChanged += Window_StateChanged;
                UpdateMaximizeIcon(window, maxBtn);
            }

            void CloseBtn_Click(object? sender, RoutedEventArgs e) => window.Close();
            void MinBtn_Click(object? sender, RoutedEventArgs e) => window.WindowState = WindowState.Minimized;
            void MaxBtn_Click(object? sender, RoutedEventArgs e)
            {
                window.WindowState = (window.WindowState == WindowState.Maximized) 
                    ? WindowState.Normal 
                    : WindowState.Maximized;
            }

            void Window_StateChanged(object? sender, EventArgs e)
            {
                if (window.Template?.FindName("PART_MaximizeButton", window) is Button btn)
                {
                    UpdateMaximizeIcon(window, btn);
                }
            }

            void UpdateMaximizeIcon(Window win, Button btn)
            {
                btn.Content = (win.WindowState == WindowState.Maximized) ? "🗗" : "🗖";
            }
        }
    }
}
