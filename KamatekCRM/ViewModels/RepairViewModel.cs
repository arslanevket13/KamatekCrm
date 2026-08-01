using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using KamatekCrm.ApplicationCore.DTOs.ServiceJobs;
using KamatekCrm.ApplicationCore.Interfaces;
using KamatekCrm.Infrastructure.Data;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;
using Microsoft.EntityFrameworkCore;

using KamatekCrm.Services;

namespace KamatekCrm.ViewModels
{
    public partial class RepairViewModel : ViewModelBase
    {
        private readonly Microsoft.EntityFrameworkCore.IDbContextFactory<KamatekCrm.Infrastructure.Data.AppDbContext> _dbContextFactory;
        private readonly IAuthService _authService;
        private readonly IToastService _toastService;
        private readonly ILoadingService _loadingService;
        private readonly SmsService _smsService;
        private readonly IServiceJobCommandService _serviceJobCommandService;

        public RepairViewModel(
            IAuthService authService,
            Microsoft.EntityFrameworkCore.IDbContextFactory<KamatekCrm.Infrastructure.Data.AppDbContext> dbContextFactory,
            IToastService toastService,
            ILoadingService loadingService,
            SmsService smsService,
            IServiceJobCommandService serviceJobCommandService)
        {
            _authService = authService;
            _dbContextFactory = dbContextFactory;
            _toastService = toastService;
            _loadingService = loadingService;
            _smsService = smsService;
            _serviceJobCommandService = serviceJobCommandService;
            
            // Komutlar
            
            _ = Refresh();
            UpdateDeviceTypeOptions();
        }

        private decimal _laborCost;
        public decimal LaborCost
        {
            get => _laborCost;
            set
            {
                if (SetProperty(ref _laborCost, value)) _ = UpdateTotals();
            }
        }

        private decimal _discountAmount;
        public decimal DiscountAmount
        {
            get => _discountAmount;
            set
            {
                if (SetProperty(ref _discountAmount, value)) _ = UpdateTotals();
            }
        }

        public decimal MaterialTotal => CurrentJobItems.Sum(x => x.UnitPrice * x.QuantityUsed);
        public decimal GrandTotal => MaterialTotal + LaborCost - DiscountAmount;

        public ObservableCollection<ServiceJobItem> CurrentJobItems { get; set; } = new ObservableCollection<ServiceJobItem>();
        public ObservableCollection<Product> Products { get; set; } = new ObservableCollection<Product>();

        private Product? _selectedProductToAdd;
        public Product? SelectedProductToAdd
        {
            get => _selectedProductToAdd;
            set
            {
                if (SetProperty(ref _selectedProductToAdd, value) && value != null)
                {
                    UnitPriceToAdd = value.SalePrice;
                }
            }
        }

        private int _quantityToAdd = 1;
        public int QuantityToAdd
        {
            get => _quantityToAdd;
            set => SetProperty(ref _quantityToAdd, value);
        }

        private decimal _unitPriceToAdd;
        public decimal UnitPriceToAdd
        {
            get => _unitPriceToAdd;
            set => SetProperty(ref _unitPriceToAdd, value);
        }

        #region Properties (List & Detail)

        private ObservableCollection<ServiceJob> _allRepairs = new();
        public ObservableCollection<ServiceJob> AllRepairs
        {
            get => _allRepairs;
            set => SetProperty(ref _allRepairs, value);
        }

        // Gruplandırma için CollectionView kullanılabilir ama şimdilik ViewModel'de filtreleyelim
        public IEnumerable<ServiceJob> PendingRepairs => AllRepairs.Where(x => x.RepairStatus == RepairStatus.Registered || x.RepairStatus == RepairStatus.Diagnosing);
        public IEnumerable<ServiceJob> InProgressRepairs => AllRepairs.Where(x => x.RepairStatus == RepairStatus.InRepair || x.RepairStatus == RepairStatus.WaitingForParts || x.RepairStatus == RepairStatus.SentToFactory);
        public IEnumerable<ServiceJob> CompletedRepairs => AllRepairs.Where(x => x.RepairStatus == RepairStatus.ReadyForPickup || x.RepairStatus == RepairStatus.Delivered || x.RepairStatus == RepairStatus.Unrepairable);


        private ServiceJob? _selectedJob;
        public ServiceJob? SelectedJob
        {
            get => _selectedJob;
            set
            {
                if (SetProperty(ref _selectedJob, value))
                {
                    _ = LoadHistory(value?.Id ?? 0);
                    OnPropertyChanged(nameof(IsJobSelected));
                    // Yeni not alanını temizle
                    NewNoteText = string.Empty;
                }
            }
        }

        public bool IsJobSelected => SelectedJob != null;

        private ObservableCollection<ServiceJobHistory> _jobHistory = new();
        public ObservableCollection<ServiceJobHistory> JobHistory
        {
            get => _jobHistory;
            set => SetProperty(ref _jobHistory, value);
        }

        private string _newNoteText = string.Empty;
        public string NewNoteText
        {
            get => _newNoteText;
            set => SetProperty(ref _newNoteText, value);
        }

        #endregion

        #region Properties (Registration Form)

        // Yeni Kayıt Formu için alanlar
        private ServiceJob _newJob = new() { ServiceJobType = ServiceJobType.Fault };
        public ServiceJob NewJob
        {
            get => _newJob;
            set => SetProperty(ref _newJob, value);
        }

        private Customer? _selectedCustomerForNewJob;
        public Customer? SelectedCustomerForNewJob
        {
            get => _selectedCustomerForNewJob;
            set
            {
                if (SetProperty(ref _selectedCustomerForNewJob, value) && value != null)
                {
                    NewJob.CustomerId = value.Id;
                }
            }
        }

        public ObservableCollection<Customer> Customers { get; } = new();
        
        // Yeni: Cihaz tipi seçenekleri
        public ObservableCollection<string> DeviceTypeOptions { get; } = new();

        // === MODERN UI PROPERTİES ===
        
        private bool _isCameraCategory = true;
        public bool IsCameraCategory
        {
            get => _isCameraCategory;
            set
            {
                if (SetProperty(ref _isCameraCategory, value))
                {
                    if (value) 
                    {
                        IsDiafonCategory = false;
                        NewJob.JobCategory = JobCategory.CCTV;
                    }
                    UpdateDeviceTypeOptions();
                }
            }
        }

        private bool _isDiafonCategory;
        public bool IsDiafonCategory
        {
            get => _isDiafonCategory;
            set
            {
                if (SetProperty(ref _isDiafonCategory, value))
                {
                    if (value) 
                    {
                        IsCameraCategory = false;
                        NewJob.JobCategory = JobCategory.VideoIntercom;
                    }
                    UpdateDeviceTypeOptions();
                }
            }
        }

        private string _selectedDeviceTypeName = string.Empty;
        public string SelectedDeviceTypeName
        {
            get => _selectedDeviceTypeName;
            set => SetProperty(ref _selectedDeviceTypeName, value);
        }

        // Aksesuarlar
        private bool _accessoryAdapter;
        public bool AccessoryAdapter
        {
            get => _accessoryAdapter;
            set => SetProperty(ref _accessoryAdapter, value);
        }

        private bool _accessoryCable;
        public bool AccessoryCable
        {
            get => _accessoryCable;
            set => SetProperty(ref _accessoryCable, value);
        }

        private bool _accessoryRemote;
        public bool AccessoryRemote
        {
            get => _accessoryRemote;
            set => SetProperty(ref _accessoryRemote, value);
        }

        // Hızlı müşteri ekleme
        private bool _isQuickAddCustomer;
        public bool IsQuickAddCustomer
        {
            get => _isQuickAddCustomer;
            set => SetProperty(ref _isQuickAddCustomer, value);
        }

        private string _quickCustomerName = string.Empty;
        public string QuickCustomerName
        {
            get => _quickCustomerName;
            set => SetProperty(ref _quickCustomerName, value);
        }

        private string _quickCustomerPhone = string.Empty;
        public string QuickCustomerPhone
        {
            get => _quickCustomerPhone;
            set => SetProperty(ref _quickCustomerPhone, value);
        }

        #endregion

        #region Commands

        public void SelectJobById(int id)
        {
            var job = AllRepairs.FirstOrDefault(x => x.Id == id);
            if (job != null)
            {
                SelectedJob = job;
            }
        }

        #endregion

        #region Methods

        [RelayCommand]
        private async Task Refresh()
        {
            _loadingService?.Show();
            try
            {
                using var context = await _dbContextFactory.CreateDbContextAsync();
                
                var jobs = await context.ServiceJobs
                    .Include(j => j.Customer)
                    .Where(j => j.ServiceJobType == ServiceJobType.Fault)
                    .ToListAsync();
                AllRepairs = new ObservableCollection<ServiceJob>(jobs);

                var customers = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(context.Customers);
                Customers.Clear();
                foreach(var c in customers.OrderBy(x => x.FullName)) Customers.Add(c);

                // Ürünleri yükle (Parça değişimi için)
                await LoadProducts();

                OnPropertyChanged(nameof(PendingRepairs));
                OnPropertyChanged(nameof(InProgressRepairs));
                OnPropertyChanged(nameof(CompletedRepairs));
            }
            catch (Exception ex)
            {
                _toastService?.ShowError("Veriler yüklenirken bir hata oluştu: " + ex.Message);
            }
            finally
            {
                _loadingService?.Hide();
            }
        }

        private async Task LoadProducts()
        {
            try
            {
                using var context = await _dbContextFactory.CreateDbContextAsync();
                var products = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(context.Products);
                Products.Clear();
                foreach (var p in products.OrderBy(x => x.ProductName)) Products.Add(p);
            }
            catch (Exception ex)
            {
                _toastService?.ShowError($"Ürünler yüklenemedi: {ex.Message}");
            }
        }

        private void UpdateDeviceTypeOptions()
        {
            DeviceTypeOptions.Clear();
            
            if (IsCameraCategory)
            {
                DeviceTypeOptions.Add("DVR");
                DeviceTypeOptions.Add("NVR");
                DeviceTypeOptions.Add("IP Kamera");
                DeviceTypeOptions.Add("Analog Kamera");
                DeviceTypeOptions.Add("PTZ Kamera");
                DeviceTypeOptions.Add("Speed Dome");
                DeviceTypeOptions.Add("Monitor");
            }
            else if (IsDiafonCategory)
            {
                DeviceTypeOptions.Add("Diafon Paneli");
                DeviceTypeOptions.Add("Diafon Dairesi");
                DeviceTypeOptions.Add("Görüntülü Diafon");
                DeviceTypeOptions.Add("Zil Paneli");
                DeviceTypeOptions.Add("Santral");
            }
        }

        private async Task LoadHistory(int jobId)
        {
            if (jobId == 0)
            {
                JobHistory.Clear();
                CurrentJobItems.Clear();
                return;
            }

            try
            {
                using var context = await _dbContextFactory.CreateDbContextAsync();
                var history = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
                    System.Linq.Queryable.OrderByDescending(
                        System.Linq.Queryable.Where(context.ServiceJobHistories, h => h.ServiceJobId == jobId), 
                        h => h.Date));
                JobHistory = new ObservableCollection<ServiceJobHistory>(history);

                // Parçaları yükle
                await LoadJobItems(jobId);
            }
            catch (Exception ex)
            {
                _toastService?.ShowError($"Geçmiş yüklenemedi: {ex.Message}");
            }
        }

        private async Task LoadJobItems(int jobId)
        {
            try
            {
                CurrentJobItems.Clear();
                using var context = await _dbContextFactory.CreateDbContextAsync();
                var items = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
                    Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.Include(
                        System.Linq.Queryable.Where(context.ServiceJobItems, i => i.ServiceJobId == jobId), 
                        i => i.Product));
                        
                foreach(var item in items) CurrentJobItems.Add(item);
                
                await UpdateTotals();
            }
            catch (Exception ex)
            {
                _toastService?.ShowError($"İş kalemleri yüklenemedi: {ex.Message}");
            }
        }

        [RelayCommand]
        private void OpenRegistration(object? parameter)
        {
            ResetNewJobForm();
        }

        private void ResetNewJobForm()
        {
            NewJob = new ServiceJob 
            { 
                ServiceJobType = ServiceJobType.Fault,
                CreatedDate = DateTime.UtcNow,
                RepairStatus = RepairStatus.Registered,
                Status = JobStatus.Pending,
                WorkOrderType = WorkOrderType.Repair,
                JobCategory = JobCategory.CCTV // Default
            };
            SelectedCustomerForNewJob = null;
        }

        private bool CanSaveNewRepair(object? parameter)
        {
            return SelectedCustomerForNewJob != null 
                && !string.IsNullOrWhiteSpace(NewJob.Description)
                && !string.IsNullOrWhiteSpace(NewJob.DeviceBrand)
                && !string.IsNullOrWhiteSpace(NewJob.DeviceModel);
        }

        [RelayCommand]
        private async Task SaveNewRepair(object? parameter)
        {
            try
            {
                _loadingService?.Show();
                NewJob.CreatedDate = DateTime.UtcNow;

                using var context = await _dbContextFactory.CreateDbContextAsync();
                context.ServiceJobs.Add(NewJob);
                await context.SaveChangesAsync();

                _toastService?.ShowSuccess($"Cihaz kabul edildi! Takip No: {NewJob.Id}");
                ResetNewJobForm();
                await Refresh();
                if (parameter is Window w) w.Close();
            }
            catch (Exception ex)
            {
                _toastService?.ShowError($"Hata: {ex.Message}");
            }
            finally
            {
                _loadingService?.Hide();
            }
        }

        [RelayCommand]
        private async Task UpdateStatus(RepairStatus? newStatus)
        {
            if (SelectedJob == null || newStatus == null) return;

            if (newStatus == RepairStatus.Delivered)
            {
                try
                {
                    _loadingService?.Show();
                    var completion = await _serviceJobCommandService.CompleteAsync(
                        SelectedJob.Id,
                        LaborCost,
                        DiscountAmount,
                        NewNoteText,
                        App.CurrentUser?.Username ?? "Sistem");
                    if (completion.IsFailure)
                    {
                        _toastService?.ShowError(completion.Error);
                        return;
                    }

                    SelectedJob.Status = JobStatus.Completed;
                    SelectedJob.RepairStatus = RepairStatus.Delivered;
                    NewNoteText = string.Empty;
                    await LoadHistory(SelectedJob.Id);
                    await Refresh();
                    _toastService?.ShowSuccess("İş emri tamamlandı ve ayrılan stoklar düşüldü.");
                }
                finally
                {
                    _loadingService?.Hide();
                }
                return;
            }

             var oldStatus = SelectedJob.RepairStatus;
            SelectedJob.RepairStatus = newStatus.Value;
            
            // ServiceJob.Status (Genel) mapping
            if (newStatus == RepairStatus.Unrepairable) SelectedJob.Status = JobStatus.Cancelled;
            else SelectedJob.Status = JobStatus.InProgress;

            if (newStatus == RepairStatus.ReadyForPickup || newStatus == RepairStatus.Delivered)
            {
                // Fiyatları kaydet
                SelectedJob.LaborCost = LaborCost;
                SelectedJob.DiscountAmount = DiscountAmount;
            }

            try
            {
                _loadingService?.Show();
                using var context = await _dbContextFactory.CreateDbContextAsync();
                using var transaction = await context.Database.BeginTransactionAsync();
                
                var existingJob = await context.ServiceJobs.FindAsync(SelectedJob.Id);
                if (existingJob != null)
                {
                    existingJob.RepairStatus = SelectedJob.RepairStatus;
                    existingJob.Status = SelectedJob.Status;
                    existingJob.LaborCost = SelectedJob.LaborCost;
                    existingJob.DiscountAmount = SelectedJob.DiscountAmount;
                    
                    if (!string.IsNullOrWhiteSpace(NewNoteText))
                    {
                        var history = new ServiceJobHistory
                        {
                            ServiceJobId = SelectedJob.Id,
                            StatusChange = newStatus.Value,
                            TechnicianNote = NewNoteText,
                            Date = DateTime.UtcNow
                        };
                        context.ServiceJobHistories.Add(history);
                    }

                    if (newStatus == RepairStatus.Delivered && SelectedJob.TotalAmount > 0)
                    {
                        var cashTransaction = new CashTransaction
                        {
                            Amount = SelectedJob.TotalAmount,
                            TransactionType = CashTransactionType.CashIncome,
                            PaymentMethod = PaymentMethod.Cash,
                            Description = $"Tamir Teslimi - İş #{SelectedJob.Id}",
                            ReferenceNumber = $"REP-{SelectedJob.Id}",
                            Date = DateTime.UtcNow
                        };
                        context.CashTransactions.Add(cashTransaction);
                    }
                    
                    await context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    if (newStatus == RepairStatus.ReadyForPickup)
                    {
                        var customer = await context.Customers.FindAsync(SelectedJob.CustomerId);
                        if (customer != null && !string.IsNullOrWhiteSpace(customer.PhoneNumber))
                        {
                            string msg = $"Sayın {customer.FullName}, cihazınızın (Takip No: {SelectedJob.Id}) tamir işlemleri tamamlanmıştır. Teslim alabilirsiniz. Kamatek Teknik Servis";
                            await _smsService.SendSmsAsync(customer.PhoneNumber, msg);
                            _toastService?.ShowSuccess("Müşteriye otomatik SMS bildirimi gönderildi.");
                        }
                    }
                }

                NewNoteText = string.Empty; // Notu temizle
                await LoadHistory(SelectedJob.Id);
                await Refresh(); // Listeleri güncelle
            }
            catch (Exception ex)
            {
                _toastService?.ShowError($"Durum güncellenirken hata: {ex.Message}");
            }
            finally
            {
                _loadingService?.Hide();
            }
        }

        [RelayCommand]
        private async Task AddNote(object? parameter)
        {
            if (SelectedJob == null) return;

            try
            {
                using var context = await _dbContextFactory.CreateDbContextAsync();
                var history = new ServiceJobHistory
                {
                    ServiceJobId = SelectedJob.Id,
                    TechnicianNote = NewNoteText,
                    Date = DateTime.UtcNow
                };
                
                context.ServiceJobHistories.Add(history);
                await context.SaveChangesAsync();
                
                NewNoteText = string.Empty;
                await LoadHistory(SelectedJob.Id);
            }
            catch (Exception ex)
            {
                _toastService?.ShowError("Not eklenirken hata: " + ex.Message);
            }
        }

        // ==========================================
        // PARÇA VE MALİYET YÖNETİMİ
        // ==========================================

        [RelayCommand]
        private async Task AddItemToJob(object? parameter)
        {
            if (SelectedJob == null || SelectedProductToAdd == null) return;

            try
            {
                var newItem = new ServiceJobItem
                {
                    ServiceJobId = SelectedJob.Id,
                    ProductId = SelectedProductToAdd.Id,
                    QuantityUsed = QuantityToAdd,
                    UnitPrice = UnitPriceToAdd,
                    UnitCost = SelectedProductToAdd.PurchasePrice
                };

                var proposedItems = CurrentJobItems.Concat([newItem]).ToList();
                var save = await _serviceJobCommandService.SaveAsync(new ServiceJobSaveRequest(
                    SelectedJob,
                    proposedItems,
                    true,
                    App.CurrentUser?.Username ?? "Sistem"));
                if (save.IsFailure)
                {
                    _toastService?.ShowError(save.Error);
                    return;
                }

                await LoadJobItems(SelectedJob.Id);
                SelectedProductToAdd = null;
                QuantityToAdd = 1;
                UnitPriceToAdd = 0;
            }
            catch (Exception ex)
            {
                _toastService?.ShowError($"Parça eklenirken hata: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task RemoveItemFromJob(object? parameter)
        {
            if (parameter is ServiceJobItem item && SelectedJob != null)
            {
                try
                {
                    var proposedItems = CurrentJobItems.Where(existing => existing.Id != item.Id).ToList();
                    var save = await _serviceJobCommandService.SaveAsync(new ServiceJobSaveRequest(
                        SelectedJob,
                        proposedItems,
                        true,
                        App.CurrentUser?.Username ?? "Sistem"));
                    if (save.IsFailure)
                    {
                        _toastService?.ShowError(save.Error);
                        return;
                    }

                    await LoadJobItems(SelectedJob.Id);
                }
                catch (Exception ex)
                {
                    _toastService?.ShowError($"Parça çıkarılırken hata: {ex.Message}");
                }
            }
        }

        private async Task UpdateTotals()
        {
            if (SelectedJob == null) return;

            OnPropertyChanged(nameof(MaterialTotal));
            OnPropertyChanged(nameof(GrandTotal));
            
            try
            {
                using var context = await _dbContextFactory.CreateDbContextAsync();
                var existingJob = await context.ServiceJobs.FindAsync(SelectedJob.Id);
                if (existingJob != null)
                {
                    existingJob.LaborCost = LaborCost;
                    existingJob.DiscountAmount = DiscountAmount;
                    await context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _toastService?.ShowError($"Toplamlar güncellenemedi: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task CompleteJob(object? parameter)
        {
            await UpdateStatus(RepairStatus.Delivered);
        }

        [RelayCommand]
        private void PrintServiceForm(object? parameter)
        {
            if (SelectedJob == null) return;
            
            try
            {
                // PDF Servisi kullan
                 var saveDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "Servis Fişini Kaydet",
                    Filter = "PDF Dosyası (*.pdf)|*.pdf",
                    FileName = $"ServisFisi_{SelectedJob.Id}.pdf"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    // Tam data (Items yüklü olmalı)
                    var pdfService = new PdfService();
                    pdfService.GenerateServiceForm(SelectedJob, saveDialog.FileName);

                     var processInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = saveDialog.FileName,
                        UseShellExecute = true
                    };
                    System.Diagnostics.Process.Start(processInfo);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Yazdırma hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion
    }
}


