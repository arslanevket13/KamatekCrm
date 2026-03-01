using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Input;

namespace KamatekCrm.Components
{
    /// <summary>
    /// Debounced search box with clear button, placeholder, and focus ring.
    /// Fires SearchCommand after a configurable delay (default 300ms) to avoid excessive queries.
    /// Usage: <km:KmSearchBox Placeholder="Ürün ara..." SearchCommand="{Binding PerformSearch}" DebounceMs="400"/>
    /// </summary>
    public class KmSearchBox : Control
    {
        private DispatcherTimer? _debounceTimer;

        static KmSearchBox()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(KmSearchBox),
                new FrameworkPropertyMetadata(typeof(KmSearchBox)));
        }

        public KmSearchBox()
        {
            ClearCommand = new RelayCommand(ExecuteClear);
        }

        #region Dependency Properties

        public static readonly DependencyProperty SearchTextProperty =
            DependencyProperty.Register(nameof(SearchText), typeof(string), typeof(KmSearchBox),
                new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSearchTextChanged));

        public static readonly DependencyProperty PlaceholderProperty =
            DependencyProperty.Register(nameof(Placeholder), typeof(string), typeof(KmSearchBox),
                new PropertyMetadata("Ara..."));

        public static readonly DependencyProperty SearchCommandProperty =
            DependencyProperty.Register(nameof(SearchCommand), typeof(ICommand), typeof(KmSearchBox));

        public static readonly DependencyProperty DebounceMsProperty =
            DependencyProperty.Register(nameof(DebounceMs), typeof(int), typeof(KmSearchBox),
                new PropertyMetadata(300));

        public static readonly DependencyProperty HasTextProperty =
            DependencyProperty.Register(nameof(HasText), typeof(bool), typeof(KmSearchBox),
                new PropertyMetadata(false));

        public static readonly DependencyProperty ClearCommandProperty =
            DependencyProperty.Register(nameof(ClearCommand), typeof(ICommand), typeof(KmSearchBox));

        #endregion

        #region Properties

        public string SearchText
        {
            get => (string)GetValue(SearchTextProperty);
            set => SetValue(SearchTextProperty, value);
        }

        public string Placeholder
        {
            get => (string)GetValue(PlaceholderProperty);
            set => SetValue(PlaceholderProperty, value);
        }

        public ICommand SearchCommand
        {
            get => (ICommand)GetValue(SearchCommandProperty);
            set => SetValue(SearchCommandProperty, value);
        }

        public int DebounceMs
        {
            get => (int)GetValue(DebounceMsProperty);
            set => SetValue(DebounceMsProperty, value);
        }

        public bool HasText
        {
            get => (bool)GetValue(HasTextProperty);
            set => SetValue(HasTextProperty, value);
        }

        public ICommand ClearCommand
        {
            get => (ICommand)GetValue(ClearCommandProperty);
            set => SetValue(ClearCommandProperty, value);
        }

        #endregion

        private static void OnSearchTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is KmSearchBox searchBox)
            {
                searchBox.HasText = !string.IsNullOrEmpty((string)e.NewValue);
                searchBox.RestartDebounce();
            }
        }

        private void RestartDebounce()
        {
            _debounceTimer?.Stop();
            _debounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(DebounceMs)
            };
            _debounceTimer.Tick += (s, e) =>
            {
                _debounceTimer?.Stop();
                SearchCommand?.Execute(SearchText);
            };
            _debounceTimer.Start();
        }

        private void ExecuteClear()
        {
            SearchText = "";
            SearchCommand?.Execute("");
        }
    }
}
