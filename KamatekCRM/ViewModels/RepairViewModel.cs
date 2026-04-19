using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using KamatekCrm.Commands;
using KamatekCrm.Data;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;
using Microsoft.EntityFrameworkCore;

using KamatekCrm.Services;

namespace KamatekCrm.ViewModels
{
    public class RepairViewModel : ViewModelBase
    {
        private readonly ApiClient _apiClient;
        private readonly IAuthService _authService;
        private readonly IToastService _toastService;
        private readonly ILoadingService _loadingService;

        public RepairViewModel(
            IAuthService authService,
            ApiClient apiClient,
            IToastService toastService,
            ILoadingService loadingService)
        {
            _authService = authService;
            _apiClient = apiClient;
            _toastService = toastService;
            _loadingService = loadingService;
            
            // Komutlar
            SaveNewRepairCommand = new RelayCommand(SaveNewRepair, CanSaveNewRepair);
            UpdateStatusCommand = new RelayCommand<RepairStatus?>(UpdateStatus);
            AddNoteCommand = new RelayCommand(AddNote, _ => SelectedJob != null && !string.IsNullOrWhiteSpace(NewNoteText));
            RefreshCommand = new RelayCommand(_ => LoadData());
            OpenRegistrationCommand = new RelayCommand(OpenRegistration);
            AddItemToJobCommand = new RelayCommand(AddItemToJob);
            RemoveItemFromJobCommand = new RelayCommand(RemoveItemFromJob);
            CompleteJobCommand = new RelayCommand(CompleteJob);
            PrintServiceFormCommand = new RelayCommand(PrintServiceForm);
            
            LoadData();
            UpdateDeviceTypeOptions();
        }

        private decimal _laborCost;
        public decimal LaborCost
        {
            get => _laborCost;
            set
            {
                if (SetProperty(ref _laborCost, value)) UpdateTotals();
            }
        }

        private decimal _discountAmount;
        public decimal DiscountAmount
        {
            get => _discountAmount;
            set
            {
                if (SetProperty(ref _discountAmount, value)) UpdateTotals();
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
                    LoadHistory(value?.Id ?? 0);
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

        public ICommand SaveNewRepairCommand { get; }
        public ICommand UpdateStatusCommand { get; }
        public ICommand AddNoteCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand OpenRegistrationCommand { get; }



        public ICommand AddItemToJobCommand { get; }
        public ICommand RemoveItemFromJobCommand { get; }
        public ICommand CompleteJobCommand { get; }
        public ICommand PrintServiceFormCommand { get; }

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

        private async void LoadData()
        {
            _loadingService?.Show();
            try
            {
                var response = await _apiClient.GetAsync<IEnumerable<ServiceJob>>("api/servicejobs?type=Fault&pageSize=1000");
                if (response?.Data != null)
                {
                    AllRepairs = new ObservableCollection<ServiceJob>(response.Data);
                }

                // Müşterileri de yükle (Registration için)
                var customersResponse = await _apiClient.GetAsync<IEnumerable<Customer>>("api/customers?pageSize=1000");
                if (customersResponse?.Data != null)
                {
                    Customers.Clear();
                    foreach(var c in customersResponse.Data.OrderBy(x => x.FullName)) Customers.Add(c);
                }

                // Ürünleri yükle (Parça değişimi için)
                LoadProducts();

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

        private async void LoadProducts()
        {
            try
            {
                var productsResponse = await _apiClient.GetAsync<IEnumerable<Product>>("api/products?pageSize=1000");
                if (productsResponse?.Data != null)
                {
                    Products.Clear();
                    foreach (var p in productsResponse.Data.OrderBy(x => x.ProductName)) Products.Add(p);
                }
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

        private async void LoadHistory(int jobId)
        {
            if (jobId == 0)
            {
                JobHistory.Clear();
                CurrentJobItems.Clear();
                return;
            }

            try
            {
                var historyResponse = await _apiClient.GetAsync<IEnumerable<ServiceJobHistory>>($"api/servicejobs/{jobId}/history");
                if (historyResponse?.Data != null)
                {
                    JobHistory = new ObservableCollection<ServiceJobHistory>(historyResponse.Data);
                }

                // Parçaları yükle
                LoadJobItems(jobId);
            }
            catch (Exception ex)
            {
                _toastService?.ShowError($"Geçmiş yüklenemedi: {ex.Message}");
            }
        }

        private async void LoadJobItems(int jobId)
        {
            try
            {
                CurrentJobItems.Clear();
                var jobResponse = await _apiClient.GetAsync<ServiceJob>($"api/servicejobs/{jobId}");
                if (jobResponse?.Data?.ServiceJobItems != null)
                {
                    foreach(var item in jobResponse.Data.ServiceJobItems) CurrentJobItems.Add(item);
                }
                
                UpdateTotals();
            }
            catch (Exception ex)
            {
                _toastService?.ShowError($"İş kalemleri yüklenemedi: {ex.Message}");
            }
        }

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

        private async void SaveNewRepair(object? parameter)
        {
            try
            {
                _loadingService?.Show();
                NewJob.CreatedDate = DateTime.UtcNow;

                var result = await _apiClient.PostAsync<ServiceJob>("api/servicejobs", NewJob);
                if (result != null && result.Success && result.Data != null)
                {
                    _toastService?.ShowSuccess($"Cihaz kabul edildi! Takip No: {result.Data.Id}");
                    ResetNewJobForm();
                    LoadData();
                    if (parameter is Window w) w.Close();
                }
                else
                {
                    _toastService?.ShowError($"Kayıt hatası: {result?.Message}");
                }
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

        private async void UpdateStatus(RepairStatus? newStatus)
        {
            if (SelectedJob == null || newStatus == null) return;

             var oldStatus = SelectedJob.RepairStatus;
            SelectedJob.RepairStatus = newStatus.Value;
            
            // ServiceJob.Status (Genel) mapping
            if (newStatus == RepairStatus.Delivered) SelectedJob.Status = JobStatus.Completed;
            else if (newStatus == RepairStatus.Unrepairable) SelectedJob.Status = JobStatus.Cancelled;
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
                var updateResult = await _apiClient.PutAsync<object>($"api/servicejobs/{SelectedJob.Id}", SelectedJob);

                if (!string.IsNullOrWhiteSpace(NewNoteText))
                {
                    var history = new ServiceJobHistory
                    {
                        ServiceJobId = SelectedJob.Id,
                        StatusChange = newStatus.Value,
                        TechnicianNote = NewNoteText
                    };
                    await _apiClient.PostAsync<object>($"api/servicejobs/{SelectedJob.Id}/history", history);
                }

                if (newStatus == RepairStatus.Delivered && SelectedJob.TotalAmount > 0)
                {
                    var cashTransaction = new CashTransaction
                    {
                        Amount = SelectedJob.TotalAmount,
                        TransactionType = CashTransactionType.CashIncome,
                        PaymentMethod = PaymentMethod.Cash,
                        Description = $"Tamir Teslimi - İş #{SelectedJob.Id}",
                        Category = "Teknik Servis",
                        ReferenceNumber = $"REP-{SelectedJob.Id}",
                        CustomerId = SelectedJob.CustomerId,
                        CreatedBy = _authService.CurrentUser?.AdSoyad ?? "Teknisyen"
                    };
                    await _apiClient.PostAsync<object>("api/finance/cash-transactions", cashTransaction);
                }

                if (newStatus == RepairStatus.ReadyForPickup && SelectedJob.Customer != null)
                {
                    if (!string.IsNullOrWhiteSpace(SelectedJob.Customer.PhoneNumber))
                    {
                        var smsService = new SmsService();
                        string msg = $"Sayın {SelectedJob.Customer.FullName}, cihazınızın (Takip No: {SelectedJob.Id}) tamir işlemleri tamamlanmıştır. Teslim alabilirsiniz. Kamatek Teknik Servis";
                        await smsService.SendSmsAsync(SelectedJob.Customer.PhoneNumber, msg);
                        _toastService?.ShowSuccess("Müşteriye otomatik SMS bildirimi gönderildi.");
                    }
                }

                NewNoteText = string.Empty; // Notu temizle
                LoadHistory(SelectedJob.Id);
                LoadData(); // Listeleri güncelle
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

        private async void AddNote(object? parameter)
        {
            if (SelectedJob == null) return;

            try
            {
                var history = new ServiceJobHistory
                {
                    ServiceJobId = SelectedJob.Id,
                    TechnicianNote = NewNoteText
                };
                
                var result = await _apiClient.PostAsync<object>($"api/servicejobs/{SelectedJob.Id}/history", history);
                if (result != null && result.Success)
                {
                    NewNoteText = string.Empty;
                    LoadHistory(SelectedJob.Id);
                }
                else
                {
                    _toastService?.ShowError("Not eklenemedi.");
                }
            }
            catch (Exception ex)
            {
                _toastService?.ShowError("Not eklenirken hata: " + ex.Message);
            }
        }

        // ==========================================
        // PARÇA VE MALİYET YÖNETİMİ
        // ==========================================

        private async void AddItemToJob(object? parameter)
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

                var result = await _apiClient.PostAsync<ServiceJobItem>($"api/servicejobs/{SelectedJob.Id}/items", newItem);
                
                if (result != null && result.Success && result.Data != null)
                {
                    CurrentJobItems.Add(result.Data);
                    SelectedProductToAdd = null;
                    QuantityToAdd = 1;
                    UnitPriceToAdd = 0;
                    UpdateTotals();
                }
                else
                {
                    _toastService?.ShowError("Parça eklenirken hata: " + result?.Message);
                }
            }
            catch (Exception ex)
            {
                _toastService?.ShowError($"Parça eklenirken hata: {ex.Message}");
            }
        }

        private async void RemoveItemFromJob(object? parameter)
        {
            if (parameter is ServiceJobItem item && SelectedJob != null)
            {
                try
                {
                    var result = await _apiClient.DeleteAsync<object>($"api/servicejobs/{SelectedJob.Id}/items/{item.Id}");
                    if (result != null && result.Success)
                    {
                        CurrentJobItems.Remove(item);
                        UpdateTotals();
                    }
                }
                catch (Exception ex)
                {
                    _toastService?.ShowError($"Parça çıkarılırken hata: {ex.Message}");
                }
            }
        }

        private async void UpdateTotals()
        {
            if (SelectedJob == null) return;

            OnPropertyChanged(nameof(MaterialTotal));
            OnPropertyChanged(nameof(GrandTotal));
            
            try
            {
                SelectedJob.LaborCost = LaborCost;
                SelectedJob.DiscountAmount = DiscountAmount;
                
                await _apiClient.PutAsync<object>($"api/servicejobs/{SelectedJob.Id}", SelectedJob);
            }
            catch (Exception ex)
            {
                _toastService?.ShowError($"Toplamlar güncellenemedi: {ex.Message}");
            }
        }

        private async void CompleteJob(object? parameter)
        {
            try
            {
                UpdateStatus(RepairStatus.Delivered);

                foreach(var item in CurrentJobItems)
                {
                    var productResp = await _apiClient.GetAsync<Product>($"api/products/{item.ProductId}");
                    if (productResp?.Data != null)
                    {
                        productResp.Data.TotalStockQuantity -= item.QuantityUsed;
                        await _apiClient.PutAsync<object>($"api/products/{item.ProductId}", productResp.Data);
                    }
                }
            }
            catch (Exception ex)
            {
                _toastService?.ShowError($"İş tamamlanırken hata: {ex.Message}");
            }
        }

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
