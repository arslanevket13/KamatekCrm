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
            catch
            {
                // Ignore if visual tree cannot be created yet
            }

            AttachTemplateControls(window);
        }

        private static void AttachCommandBindings(Window window)
        {
            if (window.CommandBindings.OfType<CommandBinding>().Any(b => b.Command == SystemCommands.CloseWindowCommand))
                return;

            window.CommandBindings.Add(new CommandBinding(SystemCommands.CloseWindowCommand, (s, e) =>
            {
                if (s is Window w)
                {
                    SystemCommands.CloseWindow(w);
                }
            }));

            window.CommandBindings.Add(new CommandBinding(SystemCommands.MinimizeWindowCommand, (s, e) =>
            {
                if (s is Window w)
                {
                    SystemCommands.MinimizeWindow(w);
                }
            }));

            window.CommandBindings.Add(new CommandBinding(SystemCommands.MaximizeWindowCommand, (s, e) =>
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
            }));

            window.CommandBindings.Add(new CommandBinding(SystemCommands.RestoreWindowCommand, (s, e) =>
            {
                if (s is Window w)
                {
                    SystemCommands.RestoreWindow(w);
                }
            }));
        }

        private static void AttachTemplateControls(Window window)
        {
            if (window.Template == null) return;

            if (window.Template.FindName("PART_CloseButton", window) is Button closeBtn)
            {
                closeBtn.Click -= CloseBtn_Click;
                closeBtn.Click += CloseBtn_Click;
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
                    minBtn.Click -= MinBtn_Click;
                    minBtn.Click += MinBtn_Click;
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
                    maxBtn.Click -= MaxBtn_Click;
                    maxBtn.Click += MaxBtn_Click;

                    window.StateChanged -= Window_StateChanged;
                    window.StateChanged += Window_StateChanged;
                    UpdateMaximizeIcon(window, maxBtn);
                }
            }

            void CloseBtn_Click(object? sender, RoutedEventArgs e)
            {
                try
                {
                    window.Close();
                }
                catch
                {
                    SystemCommands.CloseWindow(window);
                }
            }

            void MinBtn_Click(object? sender, RoutedEventArgs e)
            {
                window.WindowState = WindowState.Minimized;
            }

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
                btn.ToolTip = (win.WindowState == WindowState.Maximized) ? "Normal Boyut" : "Ekranı Kapla";
            }
        }
    }
}
