using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
    /// <summary>Montaj malzemesi satırı — düzenlenebilir ad, miktar (kesirli), birim fiyat, not.</summary>
    public sealed class InstallationMaterialRow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public int? SourceId { get; }
        private int? _productId;
        public int? ProductId { get => _productId; set { _productId = value; OnChanged(); } }

        private string _productName;
        public string ProductName { get => _productName; set { _productName = value; OnChanged(); } }

        private decimal _quantity;
        public decimal Quantity { get => _quantity; set { _quantity = Math.Max(0m, value); OnChanged(); } }

        private decimal _unitPrice;
        public decimal UnitPrice { get => _unitPrice; set { _unitPrice = Math.Max(0m, value); OnChanged(); } }

        private string? _notes;
        public string? Notes { get => _notes; set { _notes = value; OnChanged(); } }

        public InstallationMaterialRow(InstallationMaterialDto material)
            : this(material.Id, material.ProductId, material.ProductName, material.Quantity, material.UnitPrice, material.Notes)
        {
        }

        public InstallationMaterialRow(int? sourceId, int? productId, string productName, decimal quantity, decimal unitPrice, string? notes)
        {
            SourceId = sourceId;
            ProductId = productId;
            _productName = productName;
            _quantity = quantity;
            _unitPrice = unitPrice;
            _notes = notes;
        }

        private void OnChanged() => PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(string.Empty));
    }

    /// <summary>Montaj görev satırı — başlık, açıklama ve tamamlanma durumu.</summary>
    public sealed class InstallationTaskRow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public int? SourceId { get; }
        private string _title;
        public string Title { get => _title; set { _title = value; OnChanged(); } }

        private string? _description;
        public string? Description { get => _description; set { _description = value; OnChanged(); } }

        private bool _isCompleted;
        public bool IsCompleted { get => _isCompleted; set { _isCompleted = value; OnChanged(); } }

        public InstallationTaskRow(InstallationTaskDto task)
            : this(task.Id, task.Title, task.Description, task.IsCompleted)
        {
        }

        public InstallationTaskRow(int? sourceId, string title, string? description, bool isCompleted)
        {
            SourceId = sourceId;
            _title = title;
            _description = description;
            _isCompleted = isCompleted;
        }

        private void OnChanged() => PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(string.Empty));
    }

    /// <summary>
    /// Montaj V2 editörü: montaj emri başlığı (teknisyen, tarih, not, işçilik saati),
    /// malzemeler (stoktan ekle + rezervasyon), görev listesi ve tamamlama formu
    /// (teslim notu, tamamlayan, fiili işçilik saati) tek ekranda düzenlenir.
    /// "Montajı Tamamla" doğrulamadan (malzeme + işçilik saati) sonra durumu InstallationCompleted yapar.
    /// </summary>
    public partial class InstallationEditorViewModel : ViewModelBase
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

        public ObservableCollection<InstallationMaterialRow> Materials { get; } = new();
        public ObservableCollection<InstallationTaskRow> Tasks { get; } = new();

        // ── Montaj başlığı ──
        private string _technicianName = string.Empty;
        public string TechnicianName { get => _technicianName; set => SetProperty(ref _technicianName, value); }

        private DateTime? _installationDate;
        public DateTime? InstallationDate { get => _installationDate; set => SetProperty(ref _installationDate, value); }

        private string _notes = string.Empty;
        public string Notes { get => _notes; set => SetProperty(ref _notes, value); }

        private decimal _laborHours;
        /// <summary>Planlanan işçilik saati (montaj başlığında).</summary>
        public decimal LaborHours { get => _laborHours; set { SetProperty(ref _laborHours, Math.Max(0m, value)); RefreshCompletionCheck(); } }

        // ── Tamamlama formu ──
        private string _deliveryNote = string.Empty;
        public string DeliveryNote { get => _deliveryNote; set => SetProperty(ref _deliveryNote, value); }

        private string _completionTechnician = string.Empty;
        public string CompletionTechnician { get => _completionTechnician; set => SetProperty(ref _completionTechnician, value); }

        private decimal _completionLaborHours;
        /// <summary>Fiili işçilik saati (tamamlama formunda).</summary>
        public decimal CompletionLaborHours { get => _completionLaborHours; set { SetProperty(ref _completionLaborHours, Math.Max(0m, value)); RefreshCompletionCheck(); } }

        private string _customerSignature = string.Empty;
        public string CustomerSignature { get => _customerSignature; set => SetProperty(ref _customerSignature, value); }

        // ── Başlık bilgisi ──
        public string HeaderTitle { get; private set; } = "Montaj Emri";
        public string HeaderSubtitle { get; private set; } = string.Empty;

        // ── Tamamlama doğrulama göstergeleri ──
        public bool HasMaterials { get; private set; }
        public bool HasLaborHours { get; private set; }
        public bool IsCompleted { get; private set; }
        public bool IsReadyToComplete { get; private set; }
        public string CompletionCheckSummary { get; private set; } = string.Empty;
        public string CompletionError { get; private set; } = string.Empty;

        public InstallationEditorViewModel(
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
            Tasks.CollectionChanged += (_, _) => RefreshCompletionCheck();
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

            HeaderTitle = $"Montaj Emri — {document.Value.CustomerName}";
            HeaderSubtitle = $"İş #{_jobId:D6} • {document.Value.Description}";

            var installation = workflow.Value.Installation;
            if (installation is null)
            {
                _toastService.ShowError("Montaj emri bulunamadı; önce montajı planlayın.");
                return false;
            }

            TechnicianName = installation.TechnicianName ?? string.Empty;
            InstallationDate = installation.InstallationDate;
            Notes = installation.Notes ?? string.Empty;
            LaborHours = installation.LaborHours;
            IsCompleted = installation.CompletedAt is not null;

            foreach (var material in installation.Materials)
            {
                Materials.Add(new InstallationMaterialRow(material));
            }
            foreach (var task in installation.Tasks)
            {
                Tasks.Add(new InstallationTaskRow(task));
            }

            if (IsCompleted)
            {
                DeliveryNote = installation.DeliveryNote ?? string.Empty;
                CompletionTechnician = installation.CompletionTechnician ?? string.Empty;
                CompletionLaborHours = installation.LaborHours;
                CustomerSignature = installation.CustomerSignature ?? string.Empty;
            }

            OnPropertyChanged(nameof(HeaderTitle));
            OnPropertyChanged(nameof(HeaderSubtitle));
            OnPropertyChanged(nameof(IsCompleted));
            RefreshCompletionCheck();
            return true;
        }

        private void RefreshCompletionCheck()
        {
            HasMaterials = Materials.Count > 0;
            HasLaborHours = CompletionLaborHours > 0m || LaborHours > 0m;
            IsReadyToComplete = !IsCompleted && HasMaterials && HasLaborHours;

            CompletionCheckSummary = IsCompleted
                ? "✅ Montaj tamamlandı — kayıt görüntüleme modunda."
                : IsReadyToComplete
                    ? "✅ Montaj tamamlanmaya hazır."
                    : "Tamamlama için gerekenler:";

            var missing = new System.Collections.Generic.List<string>();
            if (!HasMaterials) missing.Add("en az bir malzeme girilmeli");
            if (!HasLaborHours) missing.Add("işçilik saati girilmeli");
            CompletionError = string.Join(" • ", missing);

            OnPropertyChanged(nameof(HasMaterials));
            OnPropertyChanged(nameof(HasLaborHours));
            OnPropertyChanged(nameof(IsReadyToComplete));
            OnPropertyChanged(nameof(CompletionCheckSummary));
            OnPropertyChanged(nameof(CompletionError));
            SaveCommand.NotifyCanExecuteChanged();
            CompleteCommand.NotifyCanExecuteChanged();
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
            Materials.Add(new InstallationMaterialRow(null, product.Id, product.ProductName, 1m, product.SalePrice, null));
            _toastService.ShowInfo($"'{product.ProductName}' montaj malzemelerine eklendi (stok: {product.StockQuantity}).");
        }

        [RelayCommand]
        private async Task AddCustomMaterial()
        {
            string? name = await _dialogService.ShowInputAsync("Montaj malzemesi adı:", "Özel Malzeme Ekle");
            if (string.IsNullOrWhiteSpace(name)) return;
            Materials.Add(new InstallationMaterialRow(null, null, name.Trim(), 1m, 0m, null));
        }

        [RelayCommand]
        private void RemoveMaterial(InstallationMaterialRow? row)
        {
            if (row is not null) Materials.Remove(row);
        }

        // ── Görev yönetimi ──

        [RelayCommand]
        private void AddTask()
        {
            Tasks.Add(new InstallationTaskRow(null, "Yeni görev", string.Empty, false));
            _toastService.ShowInfo("Yeni montaj görevi eklendi; başlığı düzenleyin.");
        }

        [RelayCommand]
        private void RemoveTask(InstallationTaskRow? row)
        {
            if (row is not null) Tasks.Remove(row);
        }

        // ── Kaydet / Tamamla ──

        private bool CanSave() => Materials.Count > 0 || Tasks.Count > 0 ||
                                  !string.IsNullOrWhiteSpace(Notes) ||
                                  !string.IsNullOrWhiteSpace(TechnicianName) ||
                                  LaborHours > 0m;

        [RelayCommand(CanExecute = nameof(CanSave))]
        private async Task Save()
        {
            var validMaterials = Materials
                .Where(m => !string.IsNullOrWhiteSpace(m.ProductName))
                .ToList();
            var validTasks = Tasks.Where(t => !string.IsNullOrWhiteSpace(t.Title)).ToList();

            var request = new SaveInstallationRequest(
                _jobId,
                null,
                TechnicianName,
                InstallationDate,
                Notes,
                LaborHours,
                validMaterials.Select(m => new InstallationMaterialInput(
                    m.SourceId, m.ProductId, m.ProductName, m.Quantity, m.UnitPrice, m.Notes)).ToList(),
                validTasks.Select(t => new InstallationTaskInput(
                    t.SourceId, t.Title, t.Description, t.IsCompleted)).ToList(),
                App.CurrentUser?.Username ?? "Sistem");

            var result = await _commandService.SaveInstallationAsync(request);
            if (result.IsFailure)
            {
                _toastService.ShowError(result.Error);
                return;
            }

            _toastService.ShowSuccess("Montaj emri kaydedildi; stok rezervasyonları güncellendi.");
            RequestCloseWithSuccess?.Invoke();
        }

        [RelayCommand(CanExecute = nameof(CanComplete))]
        private async Task Complete()
        {
            var request = new CompleteInstallationRequest(
                _jobId,
                DeliveryNote,
                string.IsNullOrWhiteSpace(CompletionTechnician) ? TechnicianName : CompletionTechnician,
                string.IsNullOrWhiteSpace(CustomerSignature) ? null : CustomerSignature,
                CompletionLaborHours > 0m ? CompletionLaborHours : LaborHours,
                App.CurrentUser?.Username ?? "Sistem");

            var result = await _commandService.CompleteInstallationAsync(request);
            if (result.IsFailure)
            {
                _toastService.ShowError(result.Error);
                RefreshCompletionCheck();
                return;
            }

            _toastService.ShowSuccess("Montaj tamamlandı; stok tüketimi yapıldı.");
            RequestCloseWithSuccess?.Invoke();
        }

        private bool CanComplete() => IsReadyToComplete;

        [RelayCommand]
        private void Cancel() => RequestClose?.Invoke();
    }
}
