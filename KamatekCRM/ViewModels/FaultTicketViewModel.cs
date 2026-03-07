using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using KamatekCrm.Data;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Commands;
using KamatekCrm.Shared.Models;
using KamatekCrm.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KamatekCrm.ViewModels
{
    /// <summary>
    /// Arıza & Servis Kaydı ViewModel — DI + Async + Toast
    /// Hızlı arıza/servis kaydı için optimize edilmiş basit akış
    /// </summary>
    public class FaultTicketViewModel : ViewModelBase
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IToastService _toastService;

        #region Private Fields

        private Customer? _selectedCustomer;
        private CustomerAsset? _selectedAsset;
        private JobCategory _selectedCategory = JobCategory.CCTV;
        private JobPriority _selectedPriority = JobPriority.Normal;
        private string _description = string.Empty;
        private string _faultSymptom = string.Empty;

        // Yeni cihaz girişi
        private bool _isNewAsset;
        private string _newAssetBrand = string.Empty;
        private string _newAssetModel = string.Empty;
        private string _newAssetSerialNumber = string.Empty;
        private string _newAssetLocation = string.Empty;

        // Maliyet
        private decimal _laborCost;
        private decimal _estimatedPartsTotal;

        // Modern UI
        private bool _isCameraCategory = true;
        private bool _isDiafonCategory;
        private bool _isOtherCategory;
        private string _selectedDeviceTypeName = string.Empty;
        private string _physicalCondition = string.Empty;

        // Aksesuarlar
        private bool _accessoryAdapter;
        private bool _accessoryCable;
        private bool _accessoryRemote;

        // Hızlı müşteri ekleme
        private bool _isQuickAddCustomer;
        private string _quickCustomerName = string.Empty;
        private string _quickCustomerPhone = string.Empty;

        // Save spinner
        private bool _isSaving;

        // Fotoğraflar
        public ObservableCollection<string> TempPhotoPaths { get; } = new();

        // Window close event
        public event Action<bool>? RequestClose;

        #endregion

        #region Collections

        public ObservableCollection<Customer> Customers { get; } = new();
        public ObservableCollection<CustomerAsset> CustomerAssets { get; } = new();
        public ObservableCollection<JobCategory> Categories { get; } = new(
            Enum.GetValues(typeof(JobCategory)).Cast<JobCategory>());
        public ObservableCollection<JobPriority> Priorities { get; } = new(
            Enum.GetValues(typeof(JobPriority)).Cast<JobPriority>());
        public ObservableCollection<string> DeviceTypeOptions { get; } = new();

        #endregion

        #region Properties

        public Customer? SelectedCustomer
        {
            get => _selectedCustomer;
            set
            {
                if (SetProperty(ref _selectedCustomer, value))
                {
                    _ = LoadCustomerAssetsAsync();
                    OnPropertyChanged(nameof(CustomerAddress));
                }
            }
        }

        public CustomerAsset? SelectedAsset
        {
            get => _selectedAsset;
            set
            {
                if (SetProperty(ref _selectedAsset, value) && value != null)
                    SelectedCategory = value.Category;
            }
        }

        public JobCategory SelectedCategory
        {
            get => _selectedCategory;
            set => SetProperty(ref _selectedCategory, value);
        }

        public JobPriority SelectedPriority
        {
            get => _selectedPriority;
            set => SetProperty(ref _selectedPriority, value);
        }

        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }

        public string FaultSymptom
        {
            get => _faultSymptom;
            set => SetProperty(ref _faultSymptom, value);
        }

        public string CustomerAddress => SelectedCustomer?.FullAddress ?? "Müşteri seçilmedi";

        #endregion

        #region New Asset Properties

        public bool IsNewAsset
        {
            get => _isNewAsset;
            set
            {
                if (SetProperty(ref _isNewAsset, value))
                    OnPropertyChanged(nameof(IsExistingAsset));
            }
        }

        public bool IsExistingAsset => !IsNewAsset;

        public string NewAssetBrand { get => _newAssetBrand; set => SetProperty(ref _newAssetBrand, value); }
        public string NewAssetModel { get => _newAssetModel; set => SetProperty(ref _newAssetModel, value); }
        public string NewAssetSerialNumber { get => _newAssetSerialNumber; set => SetProperty(ref _newAssetSerialNumber, value); }
        public string NewAssetLocation { get => _newAssetLocation; set => SetProperty(ref _newAssetLocation, value); }

        #endregion

        #region Cost Properties

        public decimal LaborCost
        {
            get => _laborCost;
            set { if (SetProperty(ref _laborCost, value)) OnPropertyChanged(nameof(TotalEstimate)); }
        }

        public decimal EstimatedPartsTotal
        {
            get => _estimatedPartsTotal;
            set { if (SetProperty(ref _estimatedPartsTotal, value)) OnPropertyChanged(nameof(TotalEstimate)); }
        }

        public decimal TotalEstimate => LaborCost + EstimatedPartsTotal;

        #endregion

        #region Modern UI Properties

        public bool IsCameraCategory
        {
            get => _isCameraCategory;
            set
            {
                if (SetProperty(ref _isCameraCategory, value))
                {
                    if (value) { IsDiafonCategory = false; IsOtherCategory = false; SelectedCategory = JobCategory.CCTV; }
                    UpdateDeviceTypeOptions();
                }
            }
        }

        public bool IsDiafonCategory
        {
            get => _isDiafonCategory;
            set
            {
                if (SetProperty(ref _isDiafonCategory, value))
                {
                    if (value) { IsCameraCategory = false; IsOtherCategory = false; SelectedCategory = JobCategory.VideoIntercom; }
                    UpdateDeviceTypeOptions();
                }
            }
        }

        public bool IsOtherCategory
        {
            get => _isOtherCategory;
            set
            {
                if (SetProperty(ref _isOtherCategory, value))
                {
                    if (value) { IsCameraCategory = false; IsDiafonCategory = false; SelectedCategory = JobCategory.Other; }
                    UpdateDeviceTypeOptions();
                }
            }
        }

        public string SelectedDeviceTypeName { get => _selectedDeviceTypeName; set => SetProperty(ref _selectedDeviceTypeName, value); }
        public string PhysicalCondition { get => _physicalCondition; set => SetProperty(ref _physicalCondition, value); }
        public bool AccessoryAdapter { get => _accessoryAdapter; set => SetProperty(ref _accessoryAdapter, value); }
        public bool AccessoryCable { get => _accessoryCable; set => SetProperty(ref _accessoryCable, value); }
        public bool AccessoryRemote { get => _accessoryRemote; set => SetProperty(ref _accessoryRemote, value); }
        public bool IsQuickAddCustomer { get => _isQuickAddCustomer; set => SetProperty(ref _isQuickAddCustomer, value); }
        public string QuickCustomerName { get => _quickCustomerName; set => SetProperty(ref _quickCustomerName, value); }
        public string QuickCustomerPhone { get => _quickCustomerPhone; set => SetProperty(ref _quickCustomerPhone, value); }
        public bool IsSaving { get => _isSaving; set => SetProperty(ref _isSaving, value); }

        #endregion

        #region Commands

        public ICommand SaveFaultTicketCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand AddPhotoCommand { get; }
        public ICommand RemovePhotoCommand { get; }

        #endregion

        #region Constructor

        public FaultTicketViewModel(IServiceProvider serviceProvider, IToastService toastService)
        {
            _serviceProvider = serviceProvider;
            _toastService = toastService;

            SaveFaultTicketCommand = new RelayCommand(async _ => await SaveFaultTicketAsync(), _ => CanSave());
            CancelCommand = new RelayCommand(_ => Cancel());
            AddPhotoCommand = new RelayCommand(_ => ExecuteAddPhoto());
            RemovePhotoCommand = new RelayCommand<string>(ExecuteRemovePhoto);

            _ = LoadCustomersAsync();
            UpdateDeviceTypeOptions();
        }

        #endregion

        #region Private Methods

        private async Task LoadCustomersAsync()
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var customers = await ctx.Customers.OrderBy(c => c.FullName).ToListAsync();

                Application.Current.Dispatcher.Invoke(() =>
                {
                    Customers.Clear();
                    foreach (var c in customers) Customers.Add(c);
                });
            }
            catch (Exception ex)
            {
                _toastService.ShowError($"Müşteriler yüklenemedi: {ex.Message}");
            }
        }

        private async Task LoadCustomerAssetsAsync()
        {
            CustomerAssets.Clear();
            if (SelectedCustomer == null) return;

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var assets = await ctx.CustomerAssets
                    .Where(a => a.CustomerId == SelectedCustomer.Id)
                    .OrderBy(a => a.Category)
                    .ThenBy(a => a.Brand)
                    .ToListAsync();

                Application.Current.Dispatcher.Invoke(() =>
                {
                    foreach (var asset in assets) CustomerAssets.Add(asset);
                });
            }
            catch (Exception ex)
            {
                _toastService.ShowError($"Cihazlar yüklenemedi: {ex.Message}");
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
            else if (IsOtherCategory)
            {
                DeviceTypeOptions.Add("Hırsız Alarmı");
                DeviceTypeOptions.Add("Yangın Alarmı");
                DeviceTypeOptions.Add("Access Kontrol");
                DeviceTypeOptions.Add("Diğer (Lütfen Yazınız)");
            }
        }

        private bool CanSave()
        {
            return (SelectedCustomer != null || IsQuickAddCustomer) &&
                   !string.IsNullOrWhiteSpace(Description) &&
                   !IsSaving;
        }

        private async Task SaveFaultTicketAsync()
        {
            if (IsSaving) return;
            IsSaving = true;

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                int customerId;

                // Hızlı müşteri ekleme
                if (IsQuickAddCustomer)
                {
                    if (string.IsNullOrWhiteSpace(QuickCustomerName))
                    {
                        _toastService.ShowError("Müşteri adı zorunludur!");
                        return;
                    }

                    var newCustomer = new Customer
                    {
                        FullName = QuickCustomerName.Trim(),
                        PhoneNumber = QuickCustomerPhone?.Trim(),
                        CreatedDate = DateTime.UtcNow
                    };
                    ctx.Customers.Add(newCustomer);
                    await ctx.SaveChangesAsync();
                    customerId = newCustomer.Id;
                }
                else
                {
                    if (SelectedCustomer == null)
                    {
                        _toastService.ShowError("Müşteri seçilmedi!");
                        return;
                    }
                    customerId = SelectedCustomer.Id;
                }

                int? assetId = null;

                // Yeni cihaz
                if (IsNewAsset)
                {
                    if (string.IsNullOrWhiteSpace(NewAssetBrand) || string.IsNullOrWhiteSpace(NewAssetModel))
                    {
                        _toastService.ShowError("Yeni cihaz için Marka ve Model zorunludur.");
                        return;
                    }

                    var newAsset = new CustomerAsset
                    {
                        CustomerId = customerId,
                        Category = SelectedCategory,
                        Brand = NewAssetBrand.Trim(),
                        Model = NewAssetModel.Trim(),
                        SerialNumber = string.IsNullOrWhiteSpace(NewAssetSerialNumber) ? null : NewAssetSerialNumber.Trim(),
                        Location = string.IsNullOrWhiteSpace(NewAssetLocation) ? null : NewAssetLocation.Trim(),
                        Status = AssetStatus.NeedsRepair,
                        CreatedDate = DateTime.UtcNow
                    };

                    ctx.CustomerAssets.Add(newAsset);
                    await ctx.SaveChangesAsync();
                    assetId = newAsset.Id;
                }
                else if (SelectedAsset != null)
                {
                    assetId = SelectedAsset.Id;
                    SelectedAsset.Status = AssetStatus.NeedsRepair;
                    ctx.CustomerAssets.Update(SelectedAsset);
                }

                // Arıza kaydı
                var accessories = new System.Collections.Generic.List<string>();
                if (AccessoryAdapter) accessories.Add("Adaptör");
                if (AccessoryCable) accessories.Add("Kablo");
                if (AccessoryRemote) accessories.Add("Kumanda");

                var faultTicket = new ServiceJob
                {
                    CustomerId = customerId,
                    CustomerAssetId = assetId,
                    ServiceJobType = ServiceJobType.Fault,
                    WorkOrderType = WorkOrderType.Repair,
                    WorkflowStatus = WorkflowStatus.Draft,
                    JobCategory = SelectedCategory,
                    Priority = SelectedPriority,
                    Description = $"CİHAZ TİPİ: {SelectedDeviceTypeName}\nARIZA: {FaultSymptom}\n\n{Description}",
                    PhysicalCondition = PhysicalCondition,
                    Accessories = accessories.Count > 0 ? string.Join(", ", accessories) : null,
                    DeviceBrand = IsNewAsset ? NewAssetBrand.Trim() : SelectedAsset?.Brand,
                    DeviceModel = IsNewAsset ? NewAssetModel.Trim() : SelectedAsset?.Model,
                    SerialNumber = IsNewAsset ? NewAssetSerialNumber?.Trim() : SelectedAsset?.SerialNumber,
                    Status = JobStatus.Pending,
                    RepairStatus = RepairStatus.Registered,
                    LaborCost = LaborCost,
                    CreatedDate = DateTime.UtcNow,
                    CreatedBy = App.CurrentUser?.Username ?? "System"
                };

                // Fotoğraf kaydet
                if (TempPhotoPaths.Any())
                {
                    string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    string photoDir = System.IO.Path.Combine(appData, "KamatekCrm", "Photos");
                    if (!System.IO.Directory.Exists(photoDir)) System.IO.Directory.CreateDirectory(photoDir);

                    var finalPaths = new System.Collections.Generic.List<string>();
                    foreach (var src in TempPhotoPaths)
                    {
                        string fileName = $"NewJob_{Guid.NewGuid()}{System.IO.Path.GetExtension(src)}";
                        string destPath = System.IO.Path.Combine(photoDir, fileName);
                        System.IO.File.Copy(src, destPath);
                        finalPaths.Add(destPath);
                    }
                    faultTicket.PhotoPathsJson = System.Text.Json.JsonSerializer.Serialize(finalPaths);
                }

                ctx.ServiceJobs.Add(faultTicket);
                await ctx.SaveChangesAsync();

                _toastService.ShowSuccess($"Arıza kaydı oluşturuldu: #{faultTicket.Id}");

                // Pencereyi kapat
                RequestClose?.Invoke(true);
            }
            catch (Exception ex)
            {
                _toastService.ShowError($"Kayıt hatası: {ex.Message}");
            }
            finally
            {
                IsSaving = false;
            }
        }

        private void Cancel()
        {
            ClearForm();
            RequestClose?.Invoke(false);
        }

        private void ClearForm()
        {
            SelectedCustomer = null;
            SelectedAsset = null;
            SelectedCategory = JobCategory.CCTV;
            SelectedPriority = JobPriority.Normal;
            Description = string.Empty;
            FaultSymptom = string.Empty;
            IsNewAsset = false;
            NewAssetBrand = string.Empty;
            NewAssetModel = string.Empty;
            NewAssetSerialNumber = string.Empty;
            NewAssetLocation = string.Empty;
            LaborCost = 0;
            EstimatedPartsTotal = 0;
            PhysicalCondition = string.Empty;
            AccessoryAdapter = false;
            AccessoryCable = false;
            AccessoryRemote = false;
            IsQuickAddCustomer = false;
            QuickCustomerName = string.Empty;
            QuickCustomerPhone = string.Empty;
            TempPhotoPaths.Clear();
        }

        private void ExecuteAddPhoto()
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Resim Dosyaları|*.jpg;*.jpeg;*.png;*.bmp",
                Multiselect = true
            };
            if (openFileDialog.ShowDialog() == true)
            {
                foreach (var file in openFileDialog.FileNames)
                {
                    if (!TempPhotoPaths.Contains(file)) TempPhotoPaths.Add(file);
                }
            }
        }

        private void ExecuteRemovePhoto(string? path)
        {
            if (path != null && TempPhotoPaths.Contains(path))
            {
                TempPhotoPaths.Remove(path);
            }
        }

        #endregion
    }
}
