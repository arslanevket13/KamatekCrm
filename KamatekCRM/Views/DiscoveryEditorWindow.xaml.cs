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
            KamatekCrm.Helpers.WindowControlHelper.SetupWindowControls(this);

            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            _viewModel.OwnerWindow = this;
            DataContext = _viewModel;

            _viewModel.RequestClose += OnCloseRequested;
            _viewModel.RequestCloseWithSuccess += OnCloseWithSuccess;
            Closed += OnWindowClosed;
        }

        private void OnCloseRequested()
        {
            try { DialogResult = false; } catch (Exception catchEx) { Serilog.Log.Debug(catchEx, "İşlem sırasında beklenen/ikincil hata yakalandı: {Source}", "DiscoveryEditorWindow.xaml.cs"); }
            Close();
        }

        private void OnCloseWithSuccess()
        {
            try { DialogResult = true; } catch (Exception catchEx) { Serilog.Log.Debug(catchEx, "İşlem sırasında beklenen/ikincil hata yakalandı: {Source}", "DiscoveryEditorWindow.xaml.cs"); }
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
