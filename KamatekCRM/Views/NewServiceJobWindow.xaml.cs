using System;
using System.Windows;
using KamatekCrm.ViewModels;

namespace KamatekCrm.Views
{
    /// <summary>
    /// NewServiceJobWindow.xaml için code-behind
    /// </summary>
    public partial class NewServiceJobWindow : Window
    {
        private readonly ServiceJobViewModel _viewModel;

        public NewServiceJobWindow(ServiceJobViewModel vm)
        {
            InitializeComponent();

            _viewModel = vm ?? throw new ArgumentNullException(nameof(vm));
            DataContext = _viewModel;

            _viewModel.CancelRequested += OnCancelRequested;
            _viewModel.SaveCompleted += OnSaveCompleted;
            Closed += OnWindowClosed;
        }

        private void OnCancelRequested()
        {
            try { DialogResult = false; } catch { }
            Close();
        }

        private void OnSaveCompleted()
        {
            try { DialogResult = true; } catch { }
            Close();
        }

        private void OnWindowClosed(object? sender, EventArgs e)
        {
            _viewModel.CancelRequested -= OnCancelRequested;
            _viewModel.SaveCompleted -= OnSaveCompleted;
            Closed -= OnWindowClosed;
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            KamatekCrm.Helpers.WindowControlHelper.SetupWindowControls(this);
        }
    }
}
