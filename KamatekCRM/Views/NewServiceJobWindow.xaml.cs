using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using KamatekCrm.ViewModels;

namespace KamatekCrm.Views
{
    /// <summary>
    /// NewServiceJobWindow.xaml için code-behind
    /// </summary>
    public partial class NewServiceJobWindow : Window
    {
        private readonly ServiceJobViewModel _viewModel;

        // Kaydedilmemiş değişiklik koruması (yalnızca görünüm katmanı)
        private bool _isDirty;
        private bool _isSaving;
        private bool _closeConfirmed;

        public NewServiceJobWindow(ServiceJobViewModel vm)
        {
            InitializeComponent();

            _viewModel = vm ?? throw new ArgumentNullException(nameof(vm));
            DataContext = _viewModel;

            _viewModel.CancelRequested += OnCancelRequested;
            _viewModel.SaveCompleted += OnSaveCompleted;
            Closed += OnWindowClosed;
            Closing += OnWindowClosing;

            // Kullanıcı girdisini izle. Yalnızca odak o kontroldeyken işaretlenir;
            // böylece başlangıçta binding ile gelen veriler formu "kirli" yapmaz.
            AddHandler(TextBox.TextChangedEvent, new TextChangedEventHandler(OnUserTextChanged), true);
            AddHandler(Selector.SelectionChangedEvent, new SelectionChangedEventHandler(OnUserSelectionChanged), true);
            AddHandler(ToggleButton.CheckedEvent, new RoutedEventHandler(OnUserToggled), true);
            AddHandler(ToggleButton.UncheckedEvent, new RoutedEventHandler(OnUserToggled), true);
        }

        private void OnUserTextChanged(object sender, TextChangedEventArgs e)
        {
            if (e.OriginalSource is TextBox textBox && textBox.IsKeyboardFocusWithin)
            {
                _isDirty = true;
            }
        }

        private void OnUserSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.OriginalSource is Selector selector && selector.IsKeyboardFocusWithin)
            {
                _isDirty = true;
            }
        }

        private void OnUserToggled(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is UIElement element && element.IsKeyboardFocusWithin)
            {
                _isDirty = true;
            }
        }

        private void OnCancelRequested()
        {
            if (!ConfirmDiscardIfDirty())
            {
                return;
            }

            _closeConfirmed = true;
            TrySetDialogResult(false);
            Close();
        }

        private void OnSaveCompleted()
        {
            _isSaving = true;
            _closeConfirmed = true;
            TrySetDialogResult(true);
            Close();
        }

        private void OnWindowClosing(object? sender, CancelEventArgs e)
        {
            if (_isSaving || _closeConfirmed)
            {
                return;
            }

            if (!ConfirmDiscardIfDirty())
            {
                e.Cancel = true;
            }
        }

        /// <summary>
        /// Kaydedilmemiş değişiklik varsa kullanıcıya sorar.
        /// Devam edilebilir mi? (true = kapatılabilir)
        /// </summary>
        private bool ConfirmDiscardIfDirty()
        {
            if (!_isDirty)
            {
                return true;
            }

            var answer = MessageBox.Show(
                this,
                "Kaydedilmemiş değişiklikler var. Formu kapatmak istediğinize emin misiniz?",
                "Kaydedilmemiş Değişiklikler",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);

            return answer == MessageBoxResult.Yes;
        }

        /// <summary>
        /// Pencere modal olarak açılmadıysa DialogResult ataması hata verir; yutulur.
        /// </summary>
        private void TrySetDialogResult(bool value)
        {
            try { DialogResult = value; } catch { }
        }

        private void OnWindowClosed(object? sender, EventArgs e)
        {
            _viewModel.CancelRequested -= OnCancelRequested;
            _viewModel.SaveCompleted -= OnSaveCompleted;
            Closed -= OnWindowClosed;
            Closing -= OnWindowClosing;
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            KamatekCrm.Helpers.WindowControlHelper.SetupWindowControls(this);
        }
    }
}
