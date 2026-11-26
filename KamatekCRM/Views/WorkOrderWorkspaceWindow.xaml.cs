using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Microsoft.Win32;
using KamatekCrm.ViewModels;

namespace KamatekCrm.Views
{
    /// <summary>
    /// İş Emri Çalışma Alanı: 3 kolonlu satır içi kokpit (Sol Süreç Adımları | Orta Satır İçi Editör | Sağ Akıllı Asistan).
    /// </summary>
    public partial class WorkOrderWorkspaceWindow : Window
    {
        private readonly WorkOrderWorkspaceViewModel _viewModel;

        public WorkOrderWorkspaceWindow(WorkOrderWorkspaceViewModel viewModel)
        {
            InitializeComponent();
            KamatekCrm.Helpers.WindowControlHelper.SetupWindowControls(this);

            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            _viewModel.OwnerWindow = this;
            DataContext = _viewModel;

            _viewModel.RequestClose += OnCloseRequested;
            _viewModel.RequestBrowsePhotos += OnRequestBrowsePhotos;
            Closed += OnWindowClosed;
        }

        private IReadOnlyList<string>? OnRequestBrowsePhotos()
        {
            var dlg = new OpenFileDialog
            {
                Title = "Keşif Fotoğrafı / Belge Seç",
                Filter = "Görseller ve Belgeler|*.jpg;*.jpeg;*.png;*.webp;*.pdf|Tüm Dosyalar|*.*",
                Multiselect = true
            };
            return dlg.ShowDialog(this) == true ? dlg.FileNames : null;
        }

        private void OnPhotoAreaDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop) && e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
            {
                _viewModel.AddDroppedPhotoPaths(files.Where(f => !string.IsNullOrWhiteSpace(f)));
            }
        }

        private void OnCloseRequested()
        {
            try { DialogResult = false; }
            catch (Exception catchEx)
            {
                Serilog.Log.Debug(catchEx, "İşlem sırasında beklenen/ikincil hata yakalandı: {Source}", "WorkOrderWorkspaceWindow.xaml.cs");
            }
            Close();
        }

        private void OnWindowClosed(object? sender, EventArgs e)
        {
            _viewModel.RequestClose -= OnCloseRequested;
            _viewModel.RequestBrowsePhotos -= OnRequestBrowsePhotos;
            Closed -= OnWindowClosed;
        }
    }
}
