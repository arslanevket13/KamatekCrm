using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using KamatekCrm.ApplicationCore.DTOs.ServiceJobs;
using KamatekCrm.ApplicationCore.Interfaces;
using KamatekCrm.Shared.Services;
using KamatekCrm.Services;
using KamatekCrm.Views;

namespace KamatekCrm.ViewModels
{
    /// <summary>Tahmini keşif malzemesi satırı — düzenlenebilir ad, miktar, not.</summary>
    public sealed class DiscoveryMaterialRow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public int? SourceId { get; }
        private int? _productId;
        public int? ProductId { get => _productId; set { _productId = value; OnChanged(); } }

        private string _productName;
        public string ProductName { get => _productName; set { _productName = value; OnChanged(); } }

        private int _quantity;
        public int Quantity { get => _quantity; set { _quantity = Math.Max(0, value); OnChanged(); } }

        private string? _notes;
        public string? Notes { get => _notes; set { _notes = value; OnChanged(); } }

        public DiscoveryMaterialRow(DiscoveryMaterialDto material)
            : this(material.Id, material.ProductId, material.ProductName, material.Quantity, material.Notes)
        {
        }

        public DiscoveryMaterialRow(int? sourceId, int? productId, string productName, int quantity, string? notes)
        {
            SourceId = sourceId;
            ProductId = productId;
            _productName = productName;
            _quantity = quantity;
            _notes = notes;
        }

        private void OnChanged() => PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(string.Empty));
    }

    /// <summary>Keşif ziyareti satırı — tarih, teknisyen, not.</summary>
    public sealed class DiscoveryVisitRow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public int? SourceId { get; }

        private DateTime _visitDate;
        public DateTime VisitDate { get => _visitDate; set { _visitDate = value; OnChanged(); } }

        private string? _technicianName;
        public string? TechnicianName { get => _technicianName; set { _technicianName = value; OnChanged(); } }

        private string? _notes;
        public string? Notes { get => _notes; set { _notes = value; OnChanged(); } }

        public DiscoveryVisitRow(DiscoveryVisitDto visit)
            : this(visit.Id, visit.VisitDate, visit.TechnicianName, visit.Notes)
        {
        }

        public DiscoveryVisitRow(int? sourceId, DateTime visitDate, string? technicianName, string? notes)
        {
            SourceId = sourceId;
            _visitDate = visitDate;
            _technicianName = technicianName;
            _notes = notes;
        }

        private void OnChanged() => PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(string.Empty));
    }

    /// <summary>
    /// Keşif V2 editörü: teknik rapor (notlar, çözüm, işçilik, teknisyen), tahmini malzemeler,
    /// çoklu keşif ziyaretleri ve fotoğraf/belge ekleri tek ekranda düzenlenir.
    /// "Keşifi Tamamla" doğrulamadan (rapor + not + malzeme/ziyaret) sonra durumu DiscoveryCompleted yapar.
    /// </summary>
    public partial class DiscoveryEditorViewModel : ViewModelBase
    {
        private readonly int _jobId;
        private readonly IServiceJobReadService _readService;
        private readonly IServiceJobCommandService _commandService;
        private readonly IDialogService _dialogService;
        private readonly IToastService _toastService;

        public event Action? RequestClose;
        public event Action? RequestCloseWithSuccess;

        /// <summary>Pencere açılışında code-behind tarafından atanır; içeriden açılan seçicilerin sahibi olur.</summary>
        public System.Windows.Window? OwnerWindow { get; set; }

        public ObservableCollection<DiscoveryMaterialRow> Materials { get; } = new();
        public ObservableCollection<DiscoveryVisitRow> Visits { get; } = new();
        public ObservableCollection<string> Photos { get; } = new();

        // ── Teknik rapor ──
        private string _technicalNotes = string.Empty;
        public string TechnicalNotes { get => _technicalNotes; set { SetProperty(ref _technicalNotes, value); RefreshCompletionCheck(); } }

        private string _recommendedSolution = string.Empty;
        public string RecommendedSolution { get => _recommendedSolution; set { SetProperty(ref _recommendedSolution, value); RefreshCompletionCheck(); } }

        private double _estimatedLaborHours;
        public double EstimatedLaborHours { get => _estimatedLaborHours; set => SetProperty(ref _estimatedLaborHours, Math.Max(0, value)); }

        private string _technicianName = string.Empty;
        public string TechnicianName { get => _technicianName; set => SetProperty(ref _technicianName, value); }

        // ── Başlık bilgisi ──
        public string HeaderTitle { get; private set; } = "Keşif Raporu";
        public string HeaderSubtitle { get; private set; } = string.Empty;

        // ── Tamamlama doğrulama göstergeleri ──
        public bool HasReport { get; private set; }
        public bool HasTechnicalNotes { get; private set; }
        public bool HasMaterials { get; private set; }
        public bool HasVisits { get; private set; }
        public bool IsReadyToComplete { get; private set; }
        public string CompletionCheckSummary { get; private set; } = string.Empty;
        public string CompletionError { get; private set; } = string.Empty;

        public DiscoveryEditorViewModel(
            int jobId,
            IServiceJobReadService readService,
            IServiceJobCommandService commandService,
            IDialogService dialogService,
            IToastService toastService)
        {
            _jobId = jobId;
            _readService = readService;
            _commandService = commandService;
            _dialogService = dialogService;
            _toastService = toastService;

            Materials.CollectionChanged += (_, _) => RefreshCompletionCheck();
            Visits.CollectionChanged += (_, _) =>
            {
                RefreshCompletionCheck();
                OnPropertyChanged(nameof(HasNoVisits));
            };
            Photos.CollectionChanged += (_, _) => OnPropertyChanged(nameof(PhotoSummary));
        }

        public string PhotoSummary => Photos.Count == 0
            ? "Fotoğraf eklenmedi"
            : $"{Photos.Count} fotoğraf/belge";

        public bool HasNoVisits => Visits.Count == 0;

        /// <summary>Dosya bir resim mi (PDF gibi görüntülenemeyen belgelerde simge gösterilir)?</summary>
        public static bool IsImageFile(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            var ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
            return ext is ".jpg" or ".jpeg" or ".png" or ".bmp" or ".webp" or ".gif";
        }

        public async Task<bool> InitializeAsync()
        {
            var workflow = await _readService.GetWorkOrderWorkflowAsync(_jobId);
            if (workflow.IsFailure || workflow.Value is null)
            {
                _toastService.ShowError(workflow.Error ?? "İş emri verileri yüklenemedi.");
                return false;
            }

            var document = await _readService.GetDocumentAsync(_jobId);
            if (document.IsFailure || document.Value is null)
            {
                _toastService.ShowError(document.Error);
                return false;
            }

            HeaderTitle = $"Keşif Raporu — {document.Value.CustomerName}";
            HeaderSubtitle = $"İş #{_jobId:D6} • {document.Value.Description}";

            var discovery = workflow.Value.Discovery;
            HasReport = discovery is not null;

            if (discovery is not null)
            {
                TechnicalNotes = discovery.TechnicalNotes ?? string.Empty;
                RecommendedSolution = discovery.RecommendedSolution ?? string.Empty;
                EstimatedLaborHours = discovery.EstimatedLaborHours;
                TechnicianName = discovery.TechnicianName ?? string.Empty;

                foreach (var material in discovery.Materials)
                {
                    Materials.Add(new DiscoveryMaterialRow(material));
                }
                foreach (var photo in discovery.PhotoPaths)
                {
                    Photos.Add(photo);
                }
            }

            if (workflow.Value.Visits is not null)
            {
                foreach (var visit in workflow.Value.Visits.OrderBy(v => v.VisitDate))
                {
                    Visits.Add(new DiscoveryVisitRow(visit));
                }
            }

            OnPropertyChanged(nameof(HeaderTitle));
            OnPropertyChanged(nameof(HeaderSubtitle));
            OnPropertyChanged(nameof(PhotoSummary));
            RefreshCompletionCheck();
            return true;
        }

        private void RefreshCompletionCheck()
        {
            HasTechnicalNotes = !string.IsNullOrWhiteSpace(TechnicalNotes) ||
                                !string.IsNullOrWhiteSpace(RecommendedSolution);
            HasMaterials = Materials.Count > 0;
            HasVisits = Visits.Count > 0;

            // Henüz kaydedilmemiş yeni keşifte de rapor "oluşacak" kabul edilir:
            // kullanıcı içerik girdiyse tamamlama kilitli kalmaz (önce kaydedip açmaya gerek yok).
            HasReport = HasReport || HasTechnicalNotes || HasMaterials || HasVisits || Photos.Count > 0;
            IsReadyToComplete = HasReport && HasTechnicalNotes && (HasMaterials || HasVisits);

            CompletionCheckSummary = IsReadyToComplete
                ? "✅ Keşif tamamlanmaya hazır."
                : "Tamamlama için gerekenler:";

            var missing = new System.Collections.Generic.List<string>();
            if (!HasReport) missing.Add("keşif raporu oluşturulmalı");
            if (!HasTechnicalNotes) missing.Add("teknik tespit notu veya önerilen çözüm girilmeli");
            if (!HasMaterials && !HasVisits) missing.Add("en az bir malzeme veya ziyaret kaydı girilmeli");

            CompletionError = string.Join(" • ", missing);

            OnPropertyChanged(nameof(HasReport));
            OnPropertyChanged(nameof(HasTechnicalNotes));
            OnPropertyChanged(nameof(HasMaterials));
            OnPropertyChanged(nameof(HasVisits));
            OnPropertyChanged(nameof(IsReadyToComplete));
            OnPropertyChanged(nameof(CompletionCheckSummary));
            OnPropertyChanged(nameof(CompletionError));
            CompleteCommand.NotifyCanExecuteChanged();
            SaveCommand.NotifyCanExecuteChanged();
        }

        // ── Malzeme yönetimi ──

        [RelayCommand]
        private async Task AddMaterialFromStock()
        {
            var picker = new ProductPickerWindow(async term =>
            {
                var result = await _readService.SearchProductsAsync(term);
                return result.IsSuccess && result.Value is not null ? result.Value : [];
            });
            picker.Owner = OwnerWindow ?? Application.Current?.MainWindow;
            if (picker.ShowDialog() != true || picker.SelectedProduct is null) return;

            var product = picker.SelectedProduct;
            Materials.Add(new DiscoveryMaterialRow(null, product.Id, product.ProductName, 1, null));
            _toastService.ShowInfo($"'{product.ProductName}' tahmini malzemelere eklendi (stok: {product.StockQuantity}).");
        }

        [RelayCommand]
        private async Task AddCustomMaterial()
        {
            string? name = await _dialogService.ShowInputAsync("Tahmini malzeme adı:", "Özel Malzeme Ekle");
            if (string.IsNullOrWhiteSpace(name)) return;
            Materials.Add(new DiscoveryMaterialRow(null, null, name.Trim(), 1, null));
        }

        [RelayCommand]
        private void RemoveMaterial(DiscoveryMaterialRow? row)
        {
            if (row is not null) Materials.Remove(row);
        }

        // ── Ziyaret yönetimi ──

        [RelayCommand]
        private void AddVisit()
        {
            Visits.Add(new DiscoveryVisitRow(null, DateTime.Now, TechnicianName, string.Empty));
            _toastService.ShowInfo("Yeni keşif ziyareti eklendi; tarih, teknisyen ve notu güncelleyin.");
        }

        [RelayCommand]
        private void RemoveVisit(DiscoveryVisitRow? row)
        {
            if (row is not null) Visits.Remove(row);
        }

        // ── Fotoğraf / belge ──

        [RelayCommand]
        private async Task AddPhotos()
        {
            var files = await _dialogService.ShowOpenFilesDialogAsync(
                "Keşif Fotoğrafı / Belge Ekle",
                "Görseller ve PDF (*.jpg;*.jpeg;*.png;*.bmp;*.webp;*.pdf)|*.jpg;*.jpeg;*.png;*.bmp;*.webp;*.pdf");
            if (files is null || files.Count == 0) return;

            foreach (var file in files)
            {
                if (File.Exists(file) && !Photos.Contains(file)) Photos.Add(file);
            }
            _toastService.ShowInfo($"{files.Count} dosya eklendi.");
        }

        [RelayCommand]
        private void RemovePhoto(string? path)
        {
            if (!string.IsNullOrWhiteSpace(path) && Photos.Contains(path)) Photos.Remove(path);
        }

        // ── Kaydet / Tamamla ──

        private bool CanSave() => !string.IsNullOrWhiteSpace(TechnicalNotes) ||
                                  !string.IsNullOrWhiteSpace(RecommendedSolution) ||
                                  Materials.Count > 0 || Visits.Count > 0 || Photos.Count > 0;

        [RelayCommand(CanExecute = nameof(CanSave))]
        private async Task Save()
        {
            // Boş adı olan malzeme satırları kaydedilmez (servis tarafında da guard var).
            var validMaterials = Materials
                .Where(m => !string.IsNullOrWhiteSpace(m.ProductName))
                .ToList();
            var request = new SaveDiscoveryRequest(
                _jobId,
                TechnicalNotes,
                RecommendedSolution,
                EstimatedLaborHours,
                TechnicianName,
                Photos.ToList(),
                validMaterials.Select(m => new DiscoveryMaterialInput(
                    m.SourceId, m.ProductId, m.ProductName, m.Quantity, m.Notes)).ToList(),
                Visits.Select(v => new DiscoveryVisitInput(
                    v.SourceId, v.VisitDate, v.TechnicianName, v.Notes, [])).ToList(),
                App.CurrentUser?.Username ?? "Sistem");

            var result = await _commandService.SaveDiscoveryAsync(request);
            if (result.IsFailure)
            {
                _toastService.ShowError(result.Error);
                return;
            }

            _toastService.ShowSuccess("Keşif kaydı kaydedildi.");
            RequestCloseWithSuccess?.Invoke();
        }

        [RelayCommand(CanExecute = nameof(CanComplete))]
        private async Task Complete()
        {
            var result = await _commandService.CompleteDiscoveryAsync(
                _jobId,
                App.CurrentUser?.Username ?? "Sistem");
            if (result.IsFailure)
            {
                _toastService.ShowError(result.Error);
                RefreshCompletionCheck();
                return;
            }

            _toastService.ShowSuccess("Keşif tamamlandı. Artık teklife dönüştürebilirsiniz.");
            RequestCloseWithSuccess?.Invoke();
        }

        private bool CanComplete() => IsReadyToComplete;

        [RelayCommand]
        private void Cancel() => RequestClose?.Invoke();
    }
}
