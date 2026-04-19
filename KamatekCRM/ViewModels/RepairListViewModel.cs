using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using KamatekCrm.Commands;
using KamatekCrm.Data;
using KamatekCrm.Services;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KamatekCrm.ViewModels
{
    /// <summary>
    /// Cihaz Kabul & Tamir Listesi ViewModel — DI + Async + Toast
    /// </summary>
    public class RepairListViewModel : ViewModelBase
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IToastService _toastService;

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
        public int PendingCount => AllRepairJobs.Count(j => j.RepairStatus == RepairStatus.Registered || j.RepairStatus == RepairStatus.WaitingForParts);
        public int InProgressCount => AllRepairJobs.Count(j => j.RepairStatus == RepairStatus.Diagnosing || j.RepairStatus == RepairStatus.InRepair || j.RepairStatus == RepairStatus.Testing || j.RepairStatus == RepairStatus.SentToFactory || j.RepairStatus == RepairStatus.ReturnedFromFactory);
        public int DeliveredCount => AllRepairJobs.Count(j => j.RepairStatus == RepairStatus.Delivered || j.RepairStatus == RepairStatus.ReadyForPickup);

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

        // ===== DETAY & İŞLEM =====
        private RepairJobDisplayItem? _selectedDisplayItem;
        public RepairJobDisplayItem? SelectedDisplayItem
        {
            get => _selectedDisplayItem;
            set
            {
                if (SetProperty(ref _selectedDisplayItem, value))
                {
                    if (value != null)
                        _ = LoadFullJobAsync(value.Id);
                    else
                        SelectedJob = null;
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

        public bool IsJobSelected => SelectedJob != null;

        public ObservableCollection<ServiceJobHistory> JobHistory { get; set; } = new();
        public ObservableCollection<ServiceJobItem> CurrentJobItems { get; set; } = new();
        public ObservableCollection<Product> Products { get; set; } = new();
        public ObservableCollection<string> PhotoPaths { get; set; } = new();

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

        public RepairListViewModel(IServiceProvider serviceProvider, IToastService toastService)
        {
            _serviceProvider = serviceProvider;
            _toastService = toastService;

            AllRepairJobs = new ObservableCollection<RepairJobDisplayItem>();
            StatusOptions = new ObservableCollection<RepairStatusOption>();

            InitializeStatusOptions();

            // Commands
            RefreshCommand = new RelayCommand(async _ => await LoadRepairJobsAsync());
            ClearFiltersCommand = new RelayCommand(_ => ClearFilters());
            CreateNewRepairCommand = new RelayCommand(ExecuteCreateNewRepair);

            // Workflow Commands
            UpdateStatusCommand = new RelayCommand<RepairStatus?>(async s => await UpdateStatusAsync(s));
            AddNoteCommand = new RelayCommand(async _ => await AddNoteAsync());
            AddItemToJobCommand = new RelayCommand(async _ => await AddItemToJobAsync());
            RemoveItemFromJobCommand = new RelayCommand(async p => await RemoveItemFromJobAsync(p));
            CompleteJobCommand = new RelayCommand(async _ => await CompleteJobAsync());
            AddPhotoCommand = new RelayCommand(async _ => await AddPhotoAsync());
            OpenPhotoCommand = new RelayCommand<string>(OpenPhoto);
            PrintTicketCommand = new RelayCommand(ExecutePrintTicket);

            // CollectionView
            FilteredRepairJobs = CollectionViewSource.GetDefaultView(AllRepairJobs);
            FilteredRepairJobs.Filter = FilterRepairJobs;
            FilteredRepairJobs.CollectionChanged += (s, e) => 
            {
                OnPropertyChanged(nameof(TotalCount));
                OnPropertyChanged(nameof(PendingCount));
                OnPropertyChanged(nameof(InProgressCount));
                OnPropertyChanged(nameof(DeliveredCount));
            };

            // Load Initial Data
            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            await LoadProductsAsync();
            await LoadRepairJobsAsync();
        }

        // ===== DATA LOADING (Async + Scoped DbContext) =====

        private async Task LoadProductsAsync()
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var products = await ctx.Products.OrderBy(p => p.ProductName).ToListAsync();

                Application.Current.Dispatcher.Invoke(() =>
                {
                    Products.Clear();
                    foreach (var p in products) Products.Add(p);
                });
            }
            catch (Exception ex)
            {
                _toastService.ShowError($"Ürünler yüklenemedi: {ex.Message}");
            }
        }

        private async Task LoadFullJobAsync(int id)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var job = await ctx.ServiceJobs.Include(j => j.Customer).FirstOrDefaultAsync(j => j.Id == id);
                if (job != null)
                {
                    SelectedJob = job;
                    await LoadHistoryAsync(id);
                    await LoadJobItemsAsync(id);
                    LoadPhotoGallery();
                }
            }
            catch (Exception ex)
            {
                _toastService.ShowError($"İş detayı yüklenemedi: {ex.Message}");
            }
        }

        private async Task LoadHistoryAsync(int jobId)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var history = await ctx.ServiceJobHistories
                    .Where(h => h.ServiceJobId == jobId)
                    .OrderByDescending(h => h.Date)
                    .ToListAsync();

                Application.Current.Dispatcher.Invoke(() =>
                {
                    JobHistory.Clear();
                    foreach (var h in history) JobHistory.Add(h);
                });
            }
            catch (Exception ex)
            {
                _toastService.ShowError($"Geçmiş yüklenemedi: {ex.Message}");
            }
        }

        private async Task LoadJobItemsAsync(int jobId)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var items = await ctx.ServiceJobItems
                    .Include(i => i.Product)
                    .Where(i => i.ServiceJobId == jobId)
                    .ToListAsync();

                Application.Current.Dispatcher.Invoke(() =>
                {
                    CurrentJobItems.Clear();
                    foreach (var item in items) CurrentJobItems.Add(item);
                    NotifyCostChanged();
                });
            }
            catch (Exception ex)
            {
                _toastService.ShowError($"Parça listesi yüklenemedi: {ex.Message}");
            }
        }

        private void LoadPhotoGallery()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                PhotoPaths.Clear();
                if (SelectedJob == null || string.IsNullOrEmpty(SelectedJob.PhotoPathsJson)) return;

                try
                {
                    var paths = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.List<string>>(SelectedJob.PhotoPathsJson);
                    if (paths != null)
                        foreach (var p in paths) PhotoPaths.Add(p);
                }
                catch { /* Invalid JSON — ignore */ }
            });
        }

        private void NotifyCostChanged()
        {
            OnPropertyChanged(nameof(MaterialTotal));
            OnPropertyChanged(nameof(GrandTotal));
        }

        // ===== WORKFLOW ACTIONS (Async) =====

        private async Task UpdateStatusAsync(RepairStatus? newStatus)
        {
            if (SelectedJob == null || newStatus == null) return;

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var job = await ctx.ServiceJobs.FindAsync(SelectedJob.Id);
                if (job == null) return;

                var oldStatus = job.RepairStatus;
                job.RepairStatus = newStatus.Value;
                job.ModifiedDate = DateTime.UtcNow;

                // Map to ServiceJob.Status
                if (newStatus == RepairStatus.Delivered) job.Status = JobStatus.Completed;
                else if (newStatus == RepairStatus.Unrepairable) job.Status = JobStatus.Cancelled;
                else job.Status = JobStatus.InProgress;

                ctx.ServiceJobs.Update(job);

                // History
                var history = new ServiceJobHistory
                {
                    ServiceJobId = job.Id,
                    Date = DateTime.UtcNow,
                    StatusChange = newStatus.Value,
                    TechnicianNote = !string.IsNullOrWhiteSpace(NewNoteText)
                        ? NewNoteText
                        : $"Durum değişikliği: {oldStatus} → {newStatus}",
                    UserId = App.CurrentUser?.Username ?? "System"
                };
                ctx.ServiceJobHistories.Add(history);

                await ctx.SaveChangesAsync();

                // Refresh
                SelectedJob.RepairStatus = newStatus.Value;
                SelectedJob.Status = job.Status;
                NewNoteText = string.Empty;
                await LoadHistoryAsync(job.Id);

                var listItem = AllRepairJobs.FirstOrDefault(x => x.Id == job.Id);
                if (listItem != null)
                {
                    listItem.RepairStatus = newStatus.Value;
                    FilteredRepairJobs.Refresh();
                }

                _toastService.ShowSuccess($"Durum güncellendi: {listItem?.StatusDisplay}");
            }
            catch (Exception ex)
            {
                _toastService.ShowError($"Durum güncellenemedi: {ex.Message}");
            }
        }

        private async Task AddNoteAsync()
        {
            if (SelectedJob == null || string.IsNullOrWhiteSpace(NewNoteText)) return;
            await UpdateStatusAsync(SelectedJob.RepairStatus);
        }

        private async Task AddItemToJobAsync()
        {
            if (SelectedJob == null || SelectedProductToAdd == null) return;

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var newItem = new ServiceJobItem
                {
                    ServiceJobId = SelectedJob.Id,
                    ProductId = SelectedProductToAdd.Id,
                    Product = SelectedProductToAdd,
                    QuantityUsed = QuantityToAdd,
                    UnitPrice = UnitPriceToAdd,
                    UnitCost = SelectedProductToAdd.PurchasePrice
                };
                ctx.ServiceJobItems.Add(newItem);
                await ctx.SaveChangesAsync();

                CurrentJobItems.Add(newItem);
                NotifyCostChanged();

                _toastService.ShowSuccess($"{SelectedProductToAdd.ProductName} eklendi");
                SelectedProductToAdd = null;
                QuantityToAdd = 1;
            }
            catch (Exception ex)
            {
                _toastService.ShowError($"Parça eklenemedi: {ex.Message}");
            }
        }

        private async Task RemoveItemFromJobAsync(object? param)
        {
            if (param is not ServiceJobItem item) return;

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var dbItem = await ctx.ServiceJobItems.FindAsync(item.Id);
                if (dbItem != null)
                {
                    ctx.ServiceJobItems.Remove(dbItem);
                    await ctx.SaveChangesAsync();
                }

                CurrentJobItems.Remove(item);
                NotifyCostChanged();
                _toastService.ShowSuccess("Parça kaldırıldı");
            }
            catch (Exception ex)
            {
                _toastService.ShowError($"Parça kaldırılamadı: {ex.Message}");
            }
        }

        private async Task CompleteJobAsync()
        {
            if (SelectedJob == null) return;

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                await using var transaction = await ctx.Database.BeginTransactionAsync();
                try
                {
                    // Stok düşümü
                    foreach (var item in CurrentJobItems)
                    {
                        var product = await ctx.Products.FindAsync(item.ProductId);
                        if (product != null) product.TotalStockQuantity -= item.QuantityUsed;
                    }

                    var job = await ctx.ServiceJobs.FindAsync(SelectedJob.Id);
                    if (job != null)
                    {
                        job.RepairStatus = RepairStatus.Delivered;
                        job.Status = JobStatus.Completed;
                        job.CompletedDate = DateTime.UtcNow;
                        job.ModifiedDate = DateTime.UtcNow;
                    }

                    // History
                    ctx.ServiceJobHistories.Add(new ServiceJobHistory
                    {
                        ServiceJobId = SelectedJob.Id,
                        Date = DateTime.UtcNow,
                        StatusChange = RepairStatus.Delivered,
                        TechnicianNote = "İş tamamlandı — stok düşümü yapıldı",
                        UserId = App.CurrentUser?.Username ?? "System"
                    });

                    await ctx.SaveChangesAsync();
                    await transaction.CommitAsync();

                    _toastService.ShowSuccess("İş tamamlandı ve stok güncellendi!");
                    await LoadFullJobAsync(SelectedJob.Id);
                    
                    var listItem = AllRepairJobs.FirstOrDefault(x => x.Id == SelectedJob.Id);
                    if (listItem != null)
                    {
                        listItem.RepairStatus = RepairStatus.Delivered;
                        FilteredRepairJobs.Refresh();
                    }
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                _toastService.ShowError($"Tamamlama hatası: {ex.Message}");
            }
        }

        // ===== PHOTO MANAGEMENT =====

        private async Task AddPhotoAsync()
        {
            if (SelectedJob == null) return;

            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Resim Dosyaları|*.jpg;*.jpeg;*.png;*.bmp",
                Multiselect = true
            };

            if (openFileDialog.ShowDialog() != true) return;

            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string photoDir = System.IO.Path.Combine(appData, "KamatekCrm", "Photos");
                if (!System.IO.Directory.Exists(photoDir)) System.IO.Directory.CreateDirectory(photoDir);

                var photos = string.IsNullOrEmpty(SelectedJob.PhotoPathsJson)
                    ? new System.Collections.Generic.List<string>()
                    : System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.List<string>>(SelectedJob.PhotoPathsJson) ?? new();

                foreach (var filePath in openFileDialog.FileNames)
                {
                    string fileName = $"Job_{SelectedJob.Id}_{Guid.NewGuid()}{System.IO.Path.GetExtension(filePath)}";
                    string destPath = System.IO.Path.Combine(photoDir, fileName);
                    System.IO.File.Copy(filePath, destPath);
                    photos.Add(destPath);
                }

                SelectedJob.PhotoPathsJson = System.Text.Json.JsonSerializer.Serialize(photos);

                using var scope = _serviceProvider.CreateScope();
                var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                
                var jobInDb = await ctx.ServiceJobs.FindAsync(SelectedJob.Id);
                if (jobInDb != null)
                {
                    jobInDb.PhotoPathsJson = SelectedJob.PhotoPathsJson;
                    await ctx.SaveChangesAsync();
                }

                LoadPhotoGallery();
                _toastService.ShowSuccess($"{openFileDialog.FileNames.Length} fotoğraf eklendi");
            }
            catch (Exception ex)
            {
                _toastService.ShowError($"Fotoğraf eklenemedi: {ex.Message}");
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

        // ===== INITIALIZATION & FILTERS =====

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
            StatusOptions.Add(new RepairStatusOption { Status = RepairStatus.Unrepairable, DisplayName = "İade/Hurda", Icon = "❌" });
        }

        private async Task LoadRepairJobsAsync()
        {
            if (IsBusy) return;
            IsBusy = true;

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var repairJobs = await ctx.ServiceJobs
                    .Include(j => j.Customer)
                    .Where(j => j.WorkOrderType == WorkOrderType.Repair)
                    .OrderByDescending(j => j.CreatedDate)
                    .Take(500) // Limit
                    .ToListAsync();

                Application.Current.Dispatcher.Invoke(() =>
                {
                    AllRepairJobs.Clear();
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
                    OnPropertyChanged(nameof(PendingCount));
                    OnPropertyChanged(nameof(InProgressCount));
                    OnPropertyChanged(nameof(DeliveredCount));
                });
            }
            catch (Exception ex)
            {
                _toastService.ShowError($"Veriler yüklenirken hata: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private bool FilterRepairJobs(object obj)
        {
            if (obj is not RepairJobDisplayItem job) return false;

            if (SelectedStatus.HasValue && job.RepairStatus != SelectedStatus.Value) return false;
            if (StartDate.HasValue && job.CreatedDate.Date < StartDate.Value.Date) return false;
            if (EndDate.HasValue && job.CreatedDate.Date > EndDate.Value.Date) return false;

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

                    _toastService.ShowSuccess("PDF oluşturuldu");
                }
            }
            catch (Exception ex)
            {
                _toastService.ShowError($"Yazdırma hatası: {ex.Message}");
            }
        }

        private async void ExecuteCreateNewRepair(object? parameter)
        {
            var faultVm = _serviceProvider.GetRequiredService<FaultTicketViewModel>();
            var regWindow = new Views.FaultTicketWindow(faultVm);
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
        public int DaysInShop => (DateTime.UtcNow - CreatedDate).Days;

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

        public System.Windows.Media.Brush StatusColor => (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString(RepairStatus switch
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
        })!;

        public System.Windows.Media.Brush StatusBgColor => (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString(RepairStatus switch
        {
            RepairStatus.Registered => "#269E9E9E",
            RepairStatus.Diagnosing => "#262196F3",
            RepairStatus.WaitingForParts => "#26FF9800",
            RepairStatus.SentToFactory => "#269C27B0",
            RepairStatus.ReturnedFromFactory => "#26673AB7",
            RepairStatus.InRepair => "#2603A9F4",
            RepairStatus.Testing => "#2600BCD4",
            RepairStatus.ReadyForPickup => "#264CAF50",
            RepairStatus.Delivered => "#268BC34A",
            RepairStatus.Unrepairable => "#26F44336",
            _ => "#26757575"
        })!;
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
