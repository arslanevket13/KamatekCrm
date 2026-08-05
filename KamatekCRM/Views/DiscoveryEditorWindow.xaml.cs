using System;
using System.Windows;
using KamatekCrm.ViewModels;

namespace KamatekCrm.Views
{
    /// <summary>
    /// Keşif V2 editörü: teknik rapor, tahmini malzemeler, çoklu keşif ziyaretleri
    /// ve fotoğraf/belge ekleri tek ekranda düzenlenir.
    /// </summary>
    public partial class DiscoveryEditorWindow : Window
    {
        private readonly DiscoveryEditorViewModel _viewModel;

        public DiscoveryEditorWindow(DiscoveryEditorViewModel viewModel)
        {
            InitializeComponent();

            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            _viewModel.OwnerWindow = this;
            DataContext = _viewModel;

            _viewModel.RequestClose += OnCloseRequested;
            _viewModel.RequestCloseWithSuccess += OnCloseWithSuccess;
            Closed += OnWindowClosed;
        }

        private void OnCloseRequested()
        {
            try { DialogResult = false; } catch { }
            Close();
        }

        private void OnCloseWithSuccess()
        {
            try { DialogResult = true; } catch { }
            Close();
        }

        private void OnWindowClosed(object? sender, EventArgs e)
        {
            _viewModel.RequestClose -= OnCloseRequested;
            _viewModel.RequestCloseWithSuccess -= OnCloseWithSuccess;
            Closed -= OnWindowClosed;
        }
    }
}
