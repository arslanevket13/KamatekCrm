using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using KamatekCrm.Commands;
using KamatekCrm.Data;
using KamatekCrm.Enums;
using KamatekCrm.Models;
using Microsoft.EntityFrameworkCore;

namespace KamatekCrm.ViewModels
{
    /// <summary>
    /// Cihaz Kabul & Tamir Listesi ViewModel
    /// </summary>
    public class RepairListViewModel : ViewModelBase
    {
        private readonly AppDbContext _context;
        private string _searchText = string.Empty;
        private RepairStatus? _selectedStatus;
        private DateTime? _startDate;
        private DateTime? _endDate;

        public ObservableCollection<RepairJobDisplayItem> AllRepairJobs { get; set; }
        public ICollectionView FilteredRepairJobs { get; private set; }

        public ObservableCollection<RepairStatusOption> StatusOptions { get; set; }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                    FilteredRepairJobs?.Refresh();
            }
        }

        public RepairStatus? SelectedStatus
        {
            get => _selectedStatus;
            set
            {
                if (SetProperty(ref _selectedStatus, value))
                    FilteredRepairJobs?.Refresh();
            }
        }

        public DateTime? StartDate
        {
            get => _startDate;
            set
            {
                if (SetProperty(ref _startDate, value))
                    FilteredRepairJobs?.Refresh();
            }
        }

        public DateTime? EndDate
        {
            get => _endDate;
            set
            {
                if (SetProperty(ref _endDate, value))
                    FilteredRepairJobs?.Refresh();
            }
        }

        public int TotalCount => FilteredRepairJobs?.Cast<object>().Count() ?? 0;

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        // Commands
        public ICommand RefreshCommand { get; }
        public ICommand ClearFiltersCommand { get; }
        public ICommand PrintTicketCommand { get; }
        public ICommand CreateNewRepairCommand { get; }

        // ===== DETAY & İŞLEM PROPERTIES =====
        // ===== DETAY & İŞLEM PROPERTIES =====
        private RepairJobDisplayItem? _selectedDisplayItem;
        public RepairJobDisplayItem? SelectedDisplayItem
        {
            get => _selectedDisplayItem;
            set
            {
                if (SetProperty(ref _selectedDisplayItem, value))
                {
                    if (value != null)
                    {
                        LoadFullJob(value.Id);
                    }
                    else
                    {
                        SelectedJob = null;
                    }
                }
            }
        }

        private ServiceJob? _selectedJob;
        public ServiceJob? SelectedJob
        {
            get => _selectedJob;
            set
            {
                if (SetProperty(ref _selectedJob, value))
                {
                    OnPropertyChanged(nameof(IsJobSelected));
                    NewNoteText = string.Empty;
                }
            }
        }
        
        private void LoadFullJob(int id)
        {
             var job = _context.ServiceJobs.Include(j => j.Customer).FirstOrDefault(j => j.Id == id);
             if (job != null)
             {
                 SelectedJob = job;
                 LoadHistory(job.Id);
                 LoadJobItems(job.Id);
             }
        }

        public bool IsJobSelected => SelectedJob != null;

        public ObservableCollection<ServiceJobHistory> JobHistory { get; set; } = new ObservableCollection<ServiceJobHistory>();
        public ObservableCollection<ServiceJobItem> CurrentJobItems { get; set; } = new ObservableCollection<ServiceJobItem>();
        public ObservableCollection<Product> Products { get; set; } = new ObservableCollection<Product>();

        // Yeni Not
        private string _newNoteText = string.Empty;
        public string NewNoteText { get => _newNoteText; set => SetProperty(ref _newNoteText, value); }

        // Parça Ekleme
        private Product? _selectedProductToAdd;
        public Product? SelectedProductToAdd 
        { 
            get => _selectedProductToAdd; 
            set { if (SetProperty(ref _selectedProductToAdd, value) && value != null) UnitPriceToAdd = value.SalePrice; } 
        }
        private int _quantityToAdd = 1;
        public int QuantityToAdd { get => _quantityToAdd; set => SetProperty(ref _quantityToAdd, value); }
        private decimal _unitPriceToAdd;
        public decimal UnitPriceToAdd { get => _unitPriceToAdd; set => SetProperty(ref _unitPriceToAdd, value); }

        // Maliyet
        public decimal MaterialTotal => CurrentJobItems.Sum(x => x.UnitPrice * x.QuantityUsed);
        public decimal GrandTotal => (SelectedJob == null) ? 0 : MaterialTotal + SelectedJob.LaborCost - SelectedJob.DiscountAmount;

        // ===== WORKFLOW COMMANDS =====
        public ICommand UpdateStatusCommand { get; }
        public ICommand AddNoteCommand { get; }
        public ICommand AddItemToJobCommand { get; }
        public ICommand RemoveItemFromJobCommand { get; }
        public ICommand CompleteJobCommand { get; }
        public ICommand AddPhotoCommand { get; }
        public ICommand OpenPhotoCommand { get; }

        public RepairListViewModel()
        {
            _context = new AppDbContext();
            AllRepairJobs = new ObservableCollection<RepairJobDisplayItem>();
            StatusOptions = new ObservableCollection<RepairStatusOption>();

            InitializeStatusOptions();

            // Navigation Commands
            RefreshCommand = new RelayCommand(async _ => await LoadRepairJobsAsync());
            ClearFiltersCommand = new RelayCommand(_ => ClearFilters());
            CreateNewRepairCommand = new RelayCommand(ExecuteCreateNewRepair);
            
            // Action Commands
            UpdateStatusCommand = new RelayCommand<RepairStatus?>(UpdateStatus);
            AddNoteCommand = new RelayCommand(AddNote);
            AddItemToJobCommand = new RelayCommand(AddItemToJob);
            RemoveItemFromJobCommand = new RelayCommand(RemoveItemFromJob);
            CompleteJobCommand = new RelayCommand(CompleteJob);
            AddPhotoCommand = new RelayCommand(AddPhoto);
            OpenPhotoCommand = new RelayCommand<string>(OpenPhoto);
            PrintTicketCommand = new RelayCommand(ExecutePrintTicket);

            // CollectionView
            FilteredRepairJobs = CollectionViewSource.GetDefaultView(AllRepairJobs);
            FilteredRepairJobs.Filter = FilterRepairJobs;
            FilteredRepairJobs.CollectionChanged += (s, e) => OnPropertyChanged(nameof(TotalCount));

            // Load Initial Data
            _ = LoadRepairJobsAsync();
            LoadProducts();
        }

        // ... (InitializeStatusOptions & LoadRepairJobsAsync - Existing)

        private void LoadProducts()
        {
            Products.Clear();
            var products = _context.Products.OrderBy(p => p.ProductName).ToList();
            foreach (var p in products) Products.Add(p);
        }

        private void LoadHistory(int jobId)
        {
            JobHistory.Clear();
            var history = _context.ServiceJobHistories.Where(h => h.ServiceJobId == jobId).OrderByDescending(h => h.Date).ToList();
            foreach(var h in history) JobHistory.Add(h);
        }

        private void LoadJobItems(int jobId)
        {
            CurrentJobItems.Clear();
            var items = _context.ServiceJobItems.Include(i => i.Product).Where(i => i.ServiceJobId == jobId).ToList();
            foreach(var item in items) CurrentJobItems.Add(item);
            NotifyCostChanged();
        }

        private void NotifyCostChanged()
        {
            OnPropertyChanged(nameof(MaterialTotal));
            OnPropertyChanged(nameof(GrandTotal));
        }

        // ===== WORKFLOW ACTIONS =====

        private async void UpdateStatus(RepairStatus? newStatus)
        {
            if (SelectedJob == null || newStatus == null) return;
            
            var oldStatus = SelectedJob.RepairStatus;
            SelectedJob.RepairStatus = newStatus.Value;

            // Mapping ServiceJob.Status
            if (newStatus == RepairStatus.Delivered) SelectedJob.Status = JobStatus.Completed;
            else if (newStatus == RepairStatus.Unrepairable) SelectedJob.Status = JobStatus.Cancelled;
            else SelectedJob.Status = JobStatus.InProgress;

            _context.ServiceJobs.Update(SelectedJob);
            
            // Add History
            var history = new ServiceJobHistory
            {
                ServiceJobId = SelectedJob.Id,
                Date = DateTime.Now,
                StatusChange = newStatus.Value,
                TechnicianNote = !string.IsNullOrWhiteSpace(NewNoteText) ? NewNoteText : $"Durum değişikliği: {oldStatus} -> {newStatus}",
                UserId = "Technician"
            };
            _context.ServiceJobHistories.Add(history);
            await _context.SaveChangesAsync();

            // Refresh UI
            NewNoteText = string.Empty;
            LoadHistory(SelectedJob.Id);
            
            // Update List Item (UI Sync)
            var listItem = AllRepairJobs.FirstOrDefault(x => x.Id == SelectedJob.Id);
            if (listItem != null) 
            {
                listItem.RepairStatus = newStatus.Value;
                // Force Refresh to update colors
                FilteredRepairJobs.Refresh();
            }
        }

        private void AddNote(object? param)
        {
            if (SelectedJob == null || string.IsNullOrWhiteSpace(NewNoteText)) return;
            UpdateStatus(SelectedJob.RepairStatus); // Log note with current status
        }

        private void AddItemToJob(object? param)
        {
            if (SelectedJob == null || SelectedProductToAdd == null) return;

            var newItem = new ServiceJobItem
            {
                ServiceJobId = SelectedJob.Id,
                ProductId = SelectedProductToAdd.Id,
                Product = SelectedProductToAdd,
                QuantityUsed = QuantityToAdd,
                UnitPrice = UnitPriceToAdd,
                UnitCost = SelectedProductToAdd.PurchasePrice
            };
            _context.ServiceJobItems.Add(newItem);
            _context.SaveChanges();
            
            CurrentJobItems.Add(newItem);
            NotifyCostChanged();
            
            SelectedProductToAdd = null;
            QuantityToAdd = 1;
        }

        private void RemoveItemFromJob(object? param)
        {
            if (param is ServiceJobItem item)
            {
                _context.ServiceJobItems.Remove(item);
                _context.SaveChanges();
                CurrentJobItems.Remove(item);
                NotifyCostChanged();
            }
        }

        private void CompleteJob(object? param)
        {
            // Stock deduction logic + Mark as Delivered
             foreach(var item in CurrentJobItems)
             {
                 var product = _context.Products.Find(item.ProductId);
                 if (product != null) product.TotalStockQuantity -= item.QuantityUsed;
             }
             _context.SaveChanges();
             UpdateStatus(RepairStatus.Delivered);
        }

        // ===== PHOTO MANAGEMENT =====
        
        private void AddPhoto(object? param)
        {
            if (SelectedJob == null) return;

            var openFileDialog = new Microsoft.Win32.OpenFileDialog();
            openFileDialog.Filter = "Resim Dosyaları|*.jpg;*.jpeg;*.png;*.bmp";
            openFileDialog.Multiselect = true;
            if (openFileDialog.ShowDialog() == true)
            {
                try 
                {
                    // Dosyayı AppData'ya kopyala
                    string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    string photoDir = System.IO.Path.Combine(appData, "KamatekCrm", "Photos");
                    if (!System.IO.Directory.Exists(photoDir)) System.IO.Directory.CreateDirectory(photoDir);

                    // JSON listesini al
                    var photos = string.IsNullOrEmpty(SelectedJob.PhotoPathsJson) 
                        ? new System.Collections.Generic.List<string>() 
                        : System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.List<string>>(SelectedJob.PhotoPathsJson);

                    foreach (var filePath in openFileDialog.FileNames)
                    {
                        string fileName = $"Job_{SelectedJob.Id}_{Guid.NewGuid()}{System.IO.Path.GetExtension(filePath)}";
                        string destPath = System.IO.Path.Combine(photoDir, fileName);
                        System.IO.File.Copy(filePath, destPath);
                        photos?.Add(destPath);
                    }
                    
                    SelectedJob.PhotoPathsJson = System.Text.Json.JsonSerializer.Serialize(photos);
                    _context.ServiceJobs.Update(SelectedJob);
                    _context.SaveChanges();

                    // UI'ı yenile
                    var jobId = SelectedJob.Id;
                    LoadFullJob(jobId);
                    OnPropertyChanged(nameof(SelectedJob));

                    MessageBox.Show($"{openFileDialog.FileNames.Length} fotoğraf eklendi!", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Hata: {ex.Message}");
                }
            }
        }

        private void OpenPhoto(string? path)
        {
            if (!string.IsNullOrEmpty(path) && System.IO.File.Exists(path))
            {
                new System.Diagnostics.Process 
                { 
                    StartInfo = new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true } 
                }.Start();
            }
        }

        private void InitializeStatusOptions()
        {
            StatusOptions.Add(new RepairStatusOption { Status = null, DisplayName = "Tümü", Icon = "📋" });
            StatusOptions.Add(new RepairStatusOption { Status = RepairStatus.Registered, DisplayName = "Kayıt Açıldı", Icon = "📝" });
            StatusOptions.Add(new RepairStatusOption { Status = RepairStatus.Diagnosing, DisplayName = "Arıza Tespiti", Icon = "🔍" });
            StatusOptions.Add(new RepairStatusOption { Status = RepairStatus.WaitingForParts, DisplayName = "Parça Bekleniyor", Icon = "📦" });
            StatusOptions.Add(new RepairStatusOption { Status = RepairStatus.SentToFactory, DisplayName = "Fabrikada", Icon = "🏭" });
            StatusOptions.Add(new RepairStatusOption { Status = RepairStatus.InRepair, DisplayName = "Tamir Sürüyor", Icon = "🔧" });
            StatusOptions.Add(new RepairStatusOption { Status = RepairStatus.Testing, DisplayName = "Test Aşaması", Icon = "✔️" });
            StatusOptions.Add(new RepairStatusOption { Status = RepairStatus.ReadyForPickup, DisplayName = "Teslimata Hazır", Icon = "✅" });
            StatusOptions.Add(new RepairStatusOption { Status = RepairStatus.Delivered, DisplayName = "Teslim Edildi", Icon = "🚗" });
        }

        private async Task LoadRepairJobsAsync()
        {
            if (IsBusy) return;

            IsBusy = true;
            AllRepairJobs.Clear();

            try
            {
                var repairJobs = await _context.ServiceJobs
                    .Include(j => j.Customer)
                    .Where(j => j.WorkOrderType == WorkOrderType.Repair)
                    .OrderByDescending(j => j.CreatedDate)
                    .ToListAsync();

                foreach (var job in repairJobs)
                {
                    AllRepairJobs.Add(new RepairJobDisplayItem
                    {
                        Id = job.Id,
                        TicketNo = $"#TK-{job.Id:D4}",
                        CustomerName = job.Customer?.FullName ?? "Bilinmiyor",
                        CustomerPhone = job.Customer?.PhoneNumber ?? "",
                        DeviceBrand = job.DeviceBrand ?? "",
                        DeviceModel = job.DeviceModel ?? "",
                        SerialNumber = job.SerialNumber ?? "",
                        RepairStatus = job.RepairStatus,
                        CreatedDate = job.CreatedDate,
                        Price = job.Price,
                        Description = job.Description,
                        PhotoPathsJson = job.PhotoPathsJson
                    });
                }

                OnPropertyChanged(nameof(TotalCount));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Veriler yüklenirken hata: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private bool FilterRepairJobs(object obj)
        {
            if (obj is not RepairJobDisplayItem job)
                return false;

            // Status filtresi
            if (SelectedStatus.HasValue && job.RepairStatus != SelectedStatus.Value)
                return false;

            // Tarih filtresi
            if (StartDate.HasValue && job.CreatedDate.Date < StartDate.Value.Date)
                return false;

            if (EndDate.HasValue && job.CreatedDate.Date > EndDate.Value.Date)
                return false;

            // Metin araması (Seri no, Model, Marka, Müşteri)
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var search = SearchText.ToLower();
                return job.SerialNumber.ToLower().Contains(search)
                    || job.DeviceModel.ToLower().Contains(search)
                    || job.DeviceBrand.ToLower().Contains(search)
                    || job.CustomerName.ToLower().Contains(search)
                    || job.TicketNo.ToLower().Contains(search);
            }

            return true;
        }

        private void ClearFilters()
        {
            SearchText = string.Empty;
            SelectedStatus = null;
            StartDate = null;
            EndDate = null;
        }

        private void ExecutePrintTicket(object? parameter)
        {
            if (SelectedJob == null) return;
            
            try
            {
                 var saveDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "Servis Fişini Kaydet",
                    Filter = "PDF Dosyası (*.pdf)|*.pdf",
                    FileName = $"ServisFisi_{SelectedJob.Id}.pdf"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    var pdfService = new Services.PdfService();
                    pdfService.GenerateServiceForm(SelectedJob, saveDialog.FileName);

                     new System.Diagnostics.Process
                    {
                        StartInfo = new System.Diagnostics.ProcessStartInfo(saveDialog.FileName) { UseShellExecute = true }
                    }.Start();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Yazdırma hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ExecuteCreateNewRepair(object? parameter)
        {
            var regWindow = new Views.RepairRegistrationWindow();
            if (regWindow.ShowDialog() == true) 
            {
                 await LoadRepairJobsAsync();
            }
        }
    }

    /// <summary>
    /// Tamir işi görüntüleme modeli
    /// </summary>
    public class RepairJobDisplayItem
    {
        public int Id { get; set; }
        public string TicketNo { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
        public string DeviceBrand { get; set; } = string.Empty;
        public string DeviceModel { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
        public RepairStatus RepairStatus { get; set; }
        public DateTime CreatedDate { get; set; }
        public decimal Price { get; set; }
        public string Description { get; set; } = string.Empty;
        public string? PhotoPathsJson { get; set; }

        // Computed
        public string DeviceDisplay => $"{DeviceBrand} {DeviceModel}".Trim();
        public int DaysInShop => (DateTime.Now - CreatedDate).Days;
        
        public string StatusDisplay => RepairStatus switch
        {
            RepairStatus.Registered => "Kayıt Açıldı",
            RepairStatus.Diagnosing => "Arıza Tespiti",
            RepairStatus.WaitingForParts => "Parça Bekleniyor",
            RepairStatus.SentToFactory => "Fabrikada",
            RepairStatus.ReturnedFromFactory => "Fabrikadan Geldi",
            RepairStatus.InRepair => "Tamir Sürüyor",
            RepairStatus.Testing => "Test Aşaması",
            RepairStatus.ReadyForPickup => "Teslimata Hazır",
            RepairStatus.Delivered => "Teslim Edildi",
            RepairStatus.Unrepairable => "İade/Hurda",
            _ => RepairStatus.ToString()
        };

        public string StatusColor => RepairStatus switch
        {
            RepairStatus.Registered => "#9E9E9E",
            RepairStatus.Diagnosing => "#2196F3",
            RepairStatus.WaitingForParts => "#FF9800",
            RepairStatus.SentToFactory => "#9C27B0",
            RepairStatus.ReturnedFromFactory => "#673AB7",
            RepairStatus.InRepair => "#03A9F4",
            RepairStatus.Testing => "#00BCD4",
            RepairStatus.ReadyForPickup => "#4CAF50",
            RepairStatus.Delivered => "#8BC34A",
            RepairStatus.Unrepairable => "#F44336",
            _ => "#757575"
        };

        public string StatusBgColor => RepairStatus switch
        {
            RepairStatus.Registered => "#F5F5F5",
            RepairStatus.Diagnosing => "#E3F2FD",
            RepairStatus.WaitingForParts => "#FFF3E0",
            RepairStatus.SentToFactory => "#F3E5F5",
            RepairStatus.ReturnedFromFactory => "#EDE7F6",
            RepairStatus.InRepair => "#E1F5FE",
            RepairStatus.Testing => "#E0F7FA",
            RepairStatus.ReadyForPickup => "#E8F5E9",
            RepairStatus.Delivered => "#F1F8E9",
            RepairStatus.Unrepairable => "#FFEBEE",
            _ => "#FAFAFA"
        };
    }

    /// <summary>
    /// Status dropdown seçeneği
    /// </summary>
    public class RepairStatusOption
    {
        public RepairStatus? Status { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public string FullDisplay => $"{Icon} {DisplayName}";
    }
}
