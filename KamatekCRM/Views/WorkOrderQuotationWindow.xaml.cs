using System;
using System.Windows;
using KamatekCrm.ViewModels;

namespace KamatekCrm.Views
{
    /// <summary>
    /// İş emri fiyat teklifi düzenleme penceresi.
    /// </summary>
    public partial class WorkOrderQuotationWindow : Window
    {
        private readonly WorkOrderQuotationViewModel _viewModel;

        public WorkOrderQuotationWindow(WorkOrderQuotationViewModel viewModel)
        {
            InitializeComponent();

            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            DataContext = _viewModel;

            _viewModel.RequestClose += OnCancelRequested;
            _viewModel.RequestCloseWithSuccess += OnSaveCompleted;
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
            _viewModel.RequestClose -= OnCancelRequested;
            _viewModel.RequestCloseWithSuccess -= OnSaveCompleted;
            Closed -= OnWindowClosed;
        }
    }
}
