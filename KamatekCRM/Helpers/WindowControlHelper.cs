using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace KamatekCrm.Helpers
{
    public static class WindowControlHelper
    {
        public static readonly DependencyProperty EnableWindowControlsProperty =
            DependencyProperty.RegisterAttached(
                "EnableWindowControls",
                typeof(bool),
                typeof(WindowControlHelper),
                new PropertyMetadata(false, OnEnableWindowControlsChanged));

        public static bool GetEnableWindowControls(DependencyObject obj) =>
            (bool)obj.GetValue(EnableWindowControlsProperty);

        public static void SetEnableWindowControls(DependencyObject obj, bool value) =>
            obj.SetValue(EnableWindowControlsProperty, value);

        private static void OnEnableWindowControlsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Window window && (bool)e.NewValue)
            {
                SetupWindowControls(window);
            }
        }

        public static void SetupWindowControls(Window window)
        {
            if (window == null) return;

            AttachCommandBindings(window);

            if (!window.IsLoaded)
            {
                RoutedEventHandler? loadedHandler = null;
                loadedHandler = (s, e) =>
                {
                    window.Loaded -= loadedHandler;
                    AttachTemplateControls(window);
                };
                window.Loaded += loadedHandler;
            }

            try
            {
                window.ApplyTemplate();
            }
            catch (Exception catchEx) { Serilog.Log.Debug(catchEx, "İşlem sırasında beklenen/ikincil hata yakalandı: {Source}", "WindowControlHelper.cs"); }

            AttachTemplateControls(window);
        }

        private static void AttachCommandBindings(Window window)
        {
            if (window.CommandBindings.OfType<CommandBinding>().Any(b => b.Command == SystemCommands.CloseWindowCommand))
                return;

            window.CommandBindings.Add(new CommandBinding(
                SystemCommands.CloseWindowCommand,
                (s, e) =>
                {
                    if (s is Window w)
                    {
                        SystemCommands.CloseWindow(w);
                    }
                },
                (s, e) => e.CanExecute = true));

            window.CommandBindings.Add(new CommandBinding(
                SystemCommands.MinimizeWindowCommand,
                (s, e) =>
                {
                    if (s is Window w)
                    {
                        SystemCommands.MinimizeWindow(w);
                    }
                },
                (s, e) => e.CanExecute = s is Window w && w.ResizeMode != ResizeMode.NoResize));

            window.CommandBindings.Add(new CommandBinding(
                SystemCommands.MaximizeWindowCommand,
                (s, e) =>
                {
                    if (s is Window w)
                    {
                        if (w.WindowState == WindowState.Maximized)
                        {
                            SystemCommands.RestoreWindow(w);
                        }
                        else
                        {
                            SystemCommands.MaximizeWindow(w);
                        }
                    }
                },
                (s, e) => e.CanExecute = s is Window w &&
                    (w.ResizeMode == ResizeMode.CanResize || w.ResizeMode == ResizeMode.CanResizeWithGrip)));

            window.CommandBindings.Add(new CommandBinding(
                SystemCommands.RestoreWindowCommand,
                (s, e) =>
                {
                    if (s is Window w)
                    {
                        SystemCommands.RestoreWindow(w);
                    }
                },
                (s, e) => e.CanExecute = true));
        }

        private static void AttachTemplateControls(Window window)
        {
            if (window.Template == null) return;

            if (window.Template.FindName("PART_CloseButton", window) is Button closeBtn && closeBtn.Command == null)
            {
                closeBtn.Click -= OnCloseButtonClick;
                closeBtn.Click += OnCloseButtonClick;
            }

            if (window.Template.FindName("PART_MinimizeButton", window) is Button minBtn)
            {
                if (window.ResizeMode == ResizeMode.NoResize)
                {
                    minBtn.Visibility = Visibility.Collapsed;
                }
                else
                {
                    minBtn.Visibility = Visibility.Visible;
                    if (minBtn.Command == null)
                    {
                        minBtn.Click -= OnMinimizeButtonClick;
                        minBtn.Click += OnMinimizeButtonClick;
                    }
                }
            }

            if (window.Template.FindName("PART_MaximizeButton", window) is Button maxBtn)
            {
                if (window.ResizeMode == ResizeMode.NoResize || window.ResizeMode == ResizeMode.CanMinimize)
                {
                    maxBtn.Visibility = Visibility.Collapsed;
                }
                else
                {
                    maxBtn.Visibility = Visibility.Visible;
                    if (maxBtn.Command == null)
                    {
                        maxBtn.Click -= OnMaximizeButtonClick;
                        maxBtn.Click += OnMaximizeButtonClick;
                    }

                    window.StateChanged -= OnWindowStateChanged;
                    window.StateChanged += OnWindowStateChanged;
                    UpdateMaximizeIcon(window, maxBtn);
                }
            }
        }

        private static void OnCloseButtonClick(object? sender, RoutedEventArgs e)
        {
            if (sender is DependencyObject d && Window.GetWindow(d) is Window window)
            {
                try
                {
                    window.Close();
                }
                catch (Exception ex)
                {
                    Serilog.Log.Debug(ex, "Pencere kapatılırken SystemCommands geri dönüşü kullanıldı.");
                    SystemCommands.CloseWindow(window);
                }
            }
        }

        private static void OnMinimizeButtonClick(object? sender, RoutedEventArgs e)
        {
            if (sender is DependencyObject d && Window.GetWindow(d) is Window window)
            {
                window.WindowState = WindowState.Minimized;
            }
        }

        private static void OnMaximizeButtonClick(object? sender, RoutedEventArgs e)
        {
            if (sender is DependencyObject d && Window.GetWindow(d) is Window window)
            {
                window.WindowState = window.WindowState == WindowState.Maximized
                    ? WindowState.Normal
                    : WindowState.Maximized;
            }
        }

        private static void OnWindowStateChanged(object? sender, EventArgs e)
        {
            if (sender is Window window && window.Template?.FindName("PART_MaximizeButton", window) is Button btn)
            {
                UpdateMaximizeIcon(window, btn);
            }
        }

        private static void UpdateMaximizeIcon(Window win, Button btn)
        {
            btn.Content = win.WindowState == WindowState.Maximized ? "❐" : "☐";
            btn.ToolTip = win.WindowState == WindowState.Maximized ? "Normal Boyut" : "Ekranı Kapla";
        }
    }
}
