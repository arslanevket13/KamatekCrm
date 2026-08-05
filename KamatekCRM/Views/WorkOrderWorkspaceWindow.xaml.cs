using System;
using System.Windows;
using KamatekCrm.ViewModels;

namespace KamatekCrm.Views
{
    /// <summary>
    /// İş Emri Çalışma Alanı: bir iş dosyasının tamamını süreç göstergesi ve sekmelerle sunar.
    /// </summary>
    public partial class WorkOrderWorkspaceWindow : Window
    {
        private readonly WorkOrderWorkspaceViewModel _viewModel;

        public WorkOrderWorkspaceWindow(WorkOrderWorkspaceViewModel viewModel)
        {
            InitializeComponent();

            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            _viewModel.OwnerWindow = this;
            DataContext = _viewModel;

            _viewModel.RequestClose += OnCloseRequested;
            Closed += OnWindowClosed;
        }

        private void OnCloseRequested()
        {
            try { DialogResult = false; } catch { }
            Close();
        }

        private void OnWindowClosed(object? sender, EventArgs e)
        {
            _viewModel.RequestClose -= OnCloseRequested;
            Closed -= OnWindowClosed;
        }
    }
}
