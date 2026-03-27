using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using KamatekCrm.Commands;
using KamatekCrm.Data;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;
using KamatekCrm.Shared.Models.JobDetails;
using KamatekCrm.Services;
using KamatekCrm.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;

namespace KamatekCrm.ViewModels
{
    /// <summary>
    /// İş kaydı ViewModel - Wizard UI ile KRİTİK İŞ MANTIĞI İÇERİR
    /// </summary>
    public class ServiceJobViewModel : ViewModelBase
    {
        private readonly ApiClient _apiClient;
        private readonly NavigationService _navigationService;
        private readonly IToastService _toastService;
        private readonly ILoadingService _loadingService;
        private ServiceJob? _selectedServiceJob;

        // ===== DASHBOARD KPI ALANLARI =====
        private int _totalJobCount;
        private int _pendingCount;
        private int _inProgressCount;
        private int _completedCount;
        private int _slaBreachedCount;
        private int _todayCreatedCount;
        private double _avgCompletionHours;

        // ===== EDIT & FOTOĞRAF =====
        private bool _isEditing = false;
        private ObservableCollection<string> _uploadedPhotos = new();

        // ===== WIZARD ADIM YÖNETİMİ =====
        private int _currentWizardStep = 1;
        private const int TotalWizardSteps = 4;

        // ===== KDV HESAPLAMA =====
        private decimal _kdvRate = 20m; // %20 varsayılan
        public ObservableCollection<decimal> KdvRates { get; } = new ObservableCollection<decimal> { 0m, 1m, 10m, 20m };

        // ===== TEKNİSYEN SEÇİMİ =====
        private int? _selectedTechnicianId;

        // ===== DETAY PANELİ =====
        private bool _isDetailPanelOpen;
        private ObservableCollection<ServiceJobHistory> _selectedJobHistory = new();

        public ServiceJobViewModel(NavigationService navigationService, IToastService toastService, ILoadingService loadingService, ApiClient apiClient)
        {
            _navigationService = navigationService;
            _toastService = toastService;
            _loadingService = loadingService;
            _apiClient = apiClient;

            ServiceJobs = new ObservableCollection<ServiceJob>();
            Customers = new ObservableCollection<Customer>();
            Products = new ObservableCollection<Product>();
            Technicians = new ObservableCollection<User>();
            CurrentJobItems = new ObservableCollection<ServiceJobItem>();
            CurrentJobItems.CollectionChanged += (s, e) =>
            {
                UpdateTotals();
                OnPropertyChanged(nameof(ItemCount));
            };

            JobTypes = new ObservableCollection<JobType>
            {
                JobType.SecurityCamera,
                JobType.VideoIntercom,
                JobType.SatelliteSystem
            };

            CategoryItems = new ObservableCollection<CategorySelectItem>
            {
                new CategorySelectItem { Category = JobCategory.CCTV },
                new CategorySelectItem { Category = JobCategory.VideoIntercom },
                new CategorySelectItem { Category = JobCategory.FireAlarm },
                new CategorySelectItem { Category = JobCategory.BurglarAlarm },
                new CategorySelectItem { Category = JobCategory.SmartHome },
                new CategorySelectItem { Category = JobCategory.AccessControl },
                new CategorySelectItem { Category = JobCategory.SatelliteSystem },
                new CategorySelectItem { Category = JobCategory.FiberOptic }
            };

            _selectedJobCategory = JobCategory.CCTV;

            _serviceJobsView = CollectionViewSource.GetDefaultView(ServiceJobs);
            _serviceJobsView.Filter = FilterServiceJobs;

            SaveServiceJobCommand = new RelayCommand(_ => SaveServiceJob(), _ => CanSaveServiceJob());
            AddItemToJobCommand = new RelayCommand(_ => AddItemToJob(), _ => CanAddItem());
            RemoveItemFromJobCommand = new RelayCommand(param => RemoveItemFromJob(param as ServiceJobItem));
            CompleteJobCommand = new RelayCommand(_ => CompleteJob(), _ => CanCompleteJob());
            ClearFormCommand = new RelayCommand(_ => ClearForm());
            OpenNewJobFormCommand = new RelayCommand(_ => OpenNewJobForm());
            RefreshListCommand = new RelayCommand(_ => RefreshList());
            ViewJobDetailCommand = new RelayCommand(param => ViewJobDetail(param as ServiceJob));
            PrintServiceFormCommand = new RelayCommand(param => PrintServiceForm(param as ServiceJob), param => param is ServiceJob);
            AddAssetCommand = new RelayCommand(_ => OpenQuickAssetAdd(), _ => SelectedCustomer != null);
            CancelCommand = new RelayCommand(_ => CancelRequested?.Invoke());
            EditJobCommand = new RelayCommand(param => EditJob(param as ServiceJob), param => param is ServiceJob);
            ApproveDiscoveryCommand = new RelayCommand(param => ApproveDiscovery(param as ServiceJob), param => param is ServiceJob);
            BrowsePhotosCommand = new RelayCommand(_ => BrowsePhotos());
            RemovePhotoCommand = new RelayCommand(param => RemovePhoto(param as string));

            // Wizard komutları
            GoNextStepCommand = new RelayCommand(_ => GoNextStep(), _ => CanGoNextStep());
            GoPreviousStepCommand = new RelayCommand(_ => GoPreviousStep(), _ => CurrentWizardStep > 1);

            // Dashboard & durum değiştirme komutları
            ChangeJobStatusCommand = new RelayCommand(param => ChangeJobStatus(param), param => SelectedServiceJob != null);
            DeleteJobCommand = new RelayCommand(_ => DeleteJob(), _ => SelectedServiceJob != null);

            _ = LoadData();
            UpdateDeviceTypeOptions();
        }

        public ServiceJobViewModel() : this(null!, null!, null!, null!)
        {
        }



        private Customer? _selectedCustomer;
        private JobType _selectedJobType;
        private JobCategory _selectedJobCategory; // Geriye uyumluluk için
        private string _description = string.Empty;
        private Product? _selectedProductToAdd;
        private int _quantityToAdd = 1;

        // Filtreleme için
        private string _searchText = string.Empty;
        private StatusFilter _selectedStatusFilter = StatusFilter.All;
        private DateTime? _filterStartDate;
        private DateTime? _filterEndDate;
        private ICollectionView? _serviceJobsView;

        // ===== SINGLE-PAGE FORM STATE =====
        private StructureType _selectedStructureType = StructureType.SingleUnit;
        private int _blockCount = 1;
        private int _flatCount = 1;
        private bool _applyToAllUnits;
        private DateTime? _scheduledDate;
        private string? _assignedTechnician;
        private JobPriority _selectedPriority = JobPriority.Normal;
        private decimal _laborCost;
        private decimal _discountAmount;
        private decimal _unitPriceToAdd;

        // ===== NEW ASSET ENTRY (Hibrit Akış) =====
        private bool _isNewAsset;
        private CustomerAsset? _selectedAsset;
        private DeviceType _newAssetDeviceType = DeviceType.IpCamera;
        private string _newAssetBrand = string.Empty;
        private string _newAssetModel = string.Empty;
        private string _newAssetSerialNumber = string.Empty;
        private string _newAssetLocation = string.Empty;

        // ===== ARIZA KAYIT FORM (Yeni UI) =====
        private bool _isCameraCategory = true;
        private bool _isDiafonCategory;
        private string _selectedDeviceTypeName = string.Empty;
        private string _deviceBrand = string.Empty;
        private string _deviceModel = string.Empty;
        private string _serialNumber = string.Empty;
        private bool _accessoryAdapter;
        private bool _accessoryCable;
        private bool _accessoryRemote;
        private string _physicalCondition = string.Empty;
        private bool _isQuickAddCustomer;
        private string _quickCustomerName = string.Empty;
        private string _quickCustomerPhone = string.Empty;
        private bool _isSaving;
        private bool _hasValidationError;

        #region Structure Type Properties (Yapı Tipi)

        /// <summary>
        /// Yapı tipleri listesi (ComboBox için)
        /// </summary>
        public ObservableCollection<StructureType> StructureTypes { get; } = new ObservableCollection<StructureType>
        {
            StructureType.SingleUnit,
            StructureType.Apartment,
            StructureType.Site,
            StructureType.Commercial
        };

        /// <summary>
        /// Seçili yapı tipi
        /// </summary>
        public StructureType SelectedStructureType
        {
            get => _selectedStructureType;
            set
            {
                if (SetProperty(ref _selectedStructureType, value))
                {
                    OnPropertyChanged(nameof(ShowBlockCount));
                    OnPropertyChanged(nameof(ShowFlatCount));
                    OnPropertyChanged(nameof(TotalUnitCount));
                }
            }
        }

        public int BlockCount
        {
            get => _blockCount;
            set
            {
                if (SetProperty(ref _blockCount, Math.Max(1, value)))
                {
                    OnPropertyChanged(nameof(TotalUnitCount));
                }
            }
        }

        public int FlatCount
        {
            get => _flatCount;
            set
            {
                if (SetProperty(ref _flatCount, Math.Max(1, value)))
                {
                    OnPropertyChanged(nameof(TotalUnitCount));
                }
            }
        }

        public bool ApplyToAllUnits
        {
            get => _applyToAllUnits;
            set => SetProperty(ref _applyToAllUnits, value);
        }

        public bool ShowBlockCount => SelectedStructureType == StructureType.Site;
        
        public bool ShowFlatCount => SelectedStructureType == StructureType.Apartment || SelectedStructureType == StructureType.Site;
        
        public int TotalUnitCount => SelectedStructureType switch
        {
            StructureType.SingleUnit => 1,
            StructureType.Apartment => FlatCount,
            StructureType.Site => BlockCount * FlatCount,
            StructureType.Commercial => 1,
            _ => 1
        };

        // SLA Alanları
        private DateTime? _slaDeadline;
        public DateTime? SlaDeadline
        {
            get => _slaDeadline;
            set => SetProperty(ref _slaDeadline, value);
        }

        private int? _estimatedDuration;
        public int? EstimatedDuration
        {
            get => _estimatedDuration;
            set => SetProperty(ref _estimatedDuration, value);
        }

        private string? _technicianNotes;
        public string? TechnicianNotes
        {
            get => _technicianNotes;
            set => SetProperty(ref _technicianNotes, value);
        }

        #endregion

        #region Dashboard KPI Properties

        public int TotalJobCount
        {
            get => _totalJobCount;
            set => SetProperty(ref _totalJobCount, value);
        }

        public int PendingCount
        {
            get => _pendingCount;
            set => SetProperty(ref _pendingCount, value);
        }

        public int InProgressCount
        {
            get => _inProgressCount;
            set => SetProperty(ref _inProgressCount, value);
        }

        public int CompletedCount
        {
            get => _completedCount;
            set => SetProperty(ref _completedCount, value);
        }

        public int SlaBreachedCount
        {
            get => _slaBreachedCount;
            set => SetProperty(ref _slaBreachedCount, value);
        }

        public int TodayCreatedCount
        {
            get => _todayCreatedCount;
            set => SetProperty(ref _todayCreatedCount, value);
        }

        public double AvgCompletionHours
        {
            get => _avgCompletionHours;
            set => SetProperty(ref _avgCompletionHours, value);
        }

        #endregion

        #region Wizard Step Properties

        public int CurrentWizardStep
        {
            get => _currentWizardStep;
            set
            {
                if (SetProperty(ref _currentWizardStep, value))
                {
                    OnPropertyChanged(nameof(IsStep1));
                    OnPropertyChanged(nameof(IsStep2));
                    OnPropertyChanged(nameof(IsStep3));
                    OnPropertyChanged(nameof(IsStep4));
                    OnPropertyChanged(nameof(WizardProgress));
                    OnPropertyChanged(nameof(WizardStepTitle));
                    OnPropertyChanged(nameof(CanGoBack));
                    OnPropertyChanged(nameof(IsLastStep));
                }
            }
        }

        public bool IsStep1 => CurrentWizardStep == 1;
        public bool IsStep2 => CurrentWizardStep == 2;
        public bool IsStep3 => CurrentWizardStep == 3;
        public bool IsStep4 => CurrentWizardStep == 4;
        public bool CanGoBack => CurrentWizardStep > 1;
        public bool IsLastStep => CurrentWizardStep == TotalWizardSteps;

        public double WizardProgress => (double)CurrentWizardStep / TotalWizardSteps * 100;

        public string WizardStepTitle => CurrentWizardStep switch
        {
            1 => "👤 Müşteri & Konum",
            2 => "📋 İş Detayları",
            3 => "📦 Malzeme & Maliyet",
            4 => "✅ Özet & Onay",
            _ => ""
        };

        #endregion

        #region KDV Properties

        public decimal KdvRate
        {
            get => _kdvRate;
            set
            {
                if (SetProperty(ref _kdvRate, value))
                {
                    OnPropertyChanged(nameof(KdvAmount));
                    OnPropertyChanged(nameof(GrandTotalWithKdv));
                }
            }
        }

        public decimal SubTotal => MaterialTotal + LaborCost - DiscountAmount;
        public decimal KdvAmount => SubTotal * KdvRate / 100m;
        public decimal GrandTotalWithKdv => SubTotal + KdvAmount;

        #endregion

        #region Technician & Detail Panel Properties

        public ObservableCollection<User> Technicians { get; set; }

        public int? SelectedTechnicianId
        {
            get => _selectedTechnicianId;
            set => SetProperty(ref _selectedTechnicianId, value);
        }

        public bool IsDetailPanelOpen
        {
            get => _isDetailPanelOpen;
            set => SetProperty(ref _isDetailPanelOpen, value);
        }

        public ObservableCollection<ServiceJobHistory> SelectedJobHistory
        {
            get => _selectedJobHistory;
            set => SetProperty(ref _selectedJobHistory, value);
        }

        #endregion

        #region Form Properties
        public DateTime? ScheduledDate
        {
            get => _scheduledDate;
            set => SetProperty(ref _scheduledDate, value);
        }

        /// <summary>
        /// Atanan teknisyen
        /// </summary>
        public string? AssignedTechnician
        {
            get => _assignedTechnician;
            set => SetProperty(ref _assignedTechnician, value);
        }

        /// <summary>
        /// Seçili öncelik
        /// </summary>
        public JobPriority SelectedPriority
        {
            get => _selectedPriority;
            set => SetProperty(ref _selectedPriority, value);
        }

        /// <summary>
        /// Öncelik seçenekleri
        /// </summary>
        public ObservableCollection<JobPriority> Priorities { get; } = new ObservableCollection<JobPriority>
        {
            JobPriority.Low,
            JobPriority.Normal,
            JobPriority.Urgent,
            JobPriority.Critical
        };

        /// <summary>
        /// İşçilik ücreti
        /// </summary>
        public decimal LaborCost
        {
            get => _laborCost;
            set
            {
                if (SetProperty(ref _laborCost, value))
                {
                    UpdateTotals();
                }
            }
        }

        /// <summary>
        /// İndirim tutarı
        /// </summary>
        public decimal DiscountAmount
        {
            get => _discountAmount;
            set
            {
                if (SetProperty(ref _discountAmount, value))
                {
                    UpdateTotals();
                }
            }
        }

        /// <summary>
        /// Eklenecek ürün birim fiyatı
        /// </summary>
        public decimal UnitPriceToAdd
        {
            get => _unitPriceToAdd;
            set => SetProperty(ref _unitPriceToAdd, value);
        }

        /// <summary>
        /// Malzeme toplamı
        /// </summary>
        public decimal MaterialTotal => CurrentJobItems.Sum(x => x.UnitPrice * x.QuantityUsed);

        /// <summary>
        /// Genel toplam
        /// </summary>
        public decimal GrandTotal => MaterialTotal + LaborCost - DiscountAmount;

        /// <summary>
        /// Ürün sayısı (Summary için)
        /// </summary>
        public int ItemCount => CurrentJobItems.Count;

        /// <summary>
        /// Harita HTML'i (WebViewHelper için)
        /// </summary>
        public string MapHtml
        {
            get
            {
                if (SelectedCustomer == null || string.IsNullOrWhiteSpace(SelectedCustomer.FullAddress))
                {
                    return @"<!DOCTYPE html>
<html>
<head><meta charset='utf-8'></head>
<body style='display:flex;justify-content:center;align-items:center;height:100vh;margin:0;background:#f5f5f5;font-family:Arial,sans-serif;'>
<div style='text-align:center;color:#757575;'>
<div style='font-size:48px;margin-bottom:16px;'>📍</div>
<div style='font-size:16px;'>Harita için müşteri seçin</div>
</div>
</body>
</html>";
                }
                var encoded = Uri.EscapeDataString(SelectedCustomer.FullAddress);
                return $@"<!DOCTYPE html>
<html>
<head><meta charset='utf-8'><style>body{{margin:0;padding:0;overflow:hidden;}}</style></head>
<body>
<iframe width='100%' height='100%' frameborder='0' scrolling='no' marginheight='0' marginwidth='0' 
  src='https://maps.google.com/maps?q={encoded}&t=&z=15&ie=UTF8&iwloc=&output=embed'></iframe>
</body>
</html>";
            }
        }

        #endregion

        #region Arıza Kayıt Form Properties

        /// <summary>
        /// Kamera kategorisi seçili mi?
        /// </summary>
        public bool IsCameraCategory
        {
            get => _isCameraCategory;
            set
            {
                if (SetProperty(ref _isCameraCategory, value) && value)
                {
                    _isDiafonCategory = false;
                    OnPropertyChanged(nameof(IsDiafonCategory));
                    UpdateDeviceTypeOptions();
                }
            }
        }

        /// <summary>
        /// Diafon kategorisi seçili mi?
        /// </summary>
        public bool IsDiafonCategory
        {
            get => _isDiafonCategory;
            set
            {
                if (SetProperty(ref _isDiafonCategory, value) && value)
                {
                    _isCameraCategory = false;
                    OnPropertyChanged(nameof(IsCameraCategory));
                    UpdateDeviceTypeOptions();
                }
            }
        }

        /// <summary>
        /// Cihaz tipi seçenekleri (Kategoriye göre değişir)
        /// </summary>
        public ObservableCollection<string> DeviceTypeOptions { get; } = new ObservableCollection<string>();

        /// <summary>
        /// Cihaz tipi adı (manuel giriş destekli)
        /// </summary>
        public string SelectedDeviceTypeName
        {
            get => _selectedDeviceTypeName;
            set => SetProperty(ref _selectedDeviceTypeName, value);
        }

        /// <summary>
        /// Cihaz markası
        /// </summary>
        public string DeviceBrand
        {
            get => _deviceBrand;
            set => SetProperty(ref _deviceBrand, value);
        }

        /// <summary>
        /// Cihaz modeli
        /// </summary>
        public string DeviceModel
        {
            get => _deviceModel;
            set => SetProperty(ref _deviceModel, value);
        }

        /// <summary>
        /// Seri numarası
        /// </summary>
        public string SerialNumber
        {
            get => _serialNumber;
            set => SetProperty(ref _serialNumber, value);
        }

        /// <summary>
        /// Aksesuar: Adaptör
        /// </summary>
        public bool AccessoryAdapter
        {
            get => _accessoryAdapter;
            set => SetProperty(ref _accessoryAdapter, value);
        }

        /// <summary>
        /// Aksesuar: Kablo
        /// </summary>
        public bool AccessoryCable
        {
            get => _accessoryCable;
            set => SetProperty(ref _accessoryCable, value);
        }

        /// <summary>
        /// Aksesuar: Kumanda
        /// </summary>
        public bool AccessoryRemote
        {
            get => _accessoryRemote;
            set => SetProperty(ref _accessoryRemote, value);
        }

        /// <summary>
        /// Fiziksel durum açıklaması
        /// </summary>
        public string PhysicalCondition
        {
            get => _physicalCondition;
            set => SetProperty(ref _physicalCondition, value);
        }

        /// <summary>
        /// Hızlı müşteri ekleme modu
        /// </summary>
        public bool IsQuickAddCustomer
        {
            get => _isQuickAddCustomer;
            set
            {
                if (SetProperty(ref _isQuickAddCustomer, value))
                    (GoNextStepCommand as CommunityToolkit.Mvvm.Input.IRelayCommand)?.NotifyCanExecuteChanged();
            }
        }

        private bool _isDiscoveryOnly;
        /// <summary>
        /// Yalnızca keşif yapılacak seçeneği
        /// </summary>
        public bool IsDiscoveryOnly
        {
            get => _isDiscoveryOnly;
            set => SetProperty(ref _isDiscoveryOnly, value);
        }

        /// <summary>
        /// Hızlı müşteri adı
        /// </summary>
        public string QuickCustomerName
        {
            get => _quickCustomerName;
            set
            {
                if (SetProperty(ref _quickCustomerName, value))
                    (GoNextStepCommand as CommunityToolkit.Mvvm.Input.IRelayCommand)?.NotifyCanExecuteChanged();
            }
        }

        /// <summary>
        /// Hızlı müşteri telefonu
        /// </summary>
        public string QuickCustomerPhone
        {
            get => _quickCustomerPhone;
            set
            {
                if (SetProperty(ref _quickCustomerPhone, value))
                    (GoNextStepCommand as CommunityToolkit.Mvvm.Input.IRelayCommand)?.NotifyCanExecuteChanged();
            }
        }

        /// <summary>
        /// Kaydediliyor mu? (Spinner için)
        /// </summary>
        public bool IsSaving
        {
            get => _isSaving;
            set => SetProperty(ref _isSaving, value);
        }

        /// <summary>
        /// Doğrulama hatası var mı?
        /// </summary>
        public bool HasValidationError
        {
            get => _hasValidationError;
            set => SetProperty(ref _hasValidationError, value);
        }

        public ObservableCollection<string> UploadedPhotos
        {
            get => _uploadedPhotos;
            set => SetProperty(ref _uploadedPhotos, value);
        }

        /// <summary>
        /// Kategoriye göre cihaz tipi seçeneklerini güncelle
        /// </summary>
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
                DeviceTypeOptions.Add("Monitör");
                DeviceTypeOptions.Add("Hard Disk");
            }
            else if (IsDiafonCategory)
            {
                DeviceTypeOptions.Add("Diafon Paneli");
                DeviceTypeOptions.Add("Daire Monitörü");
                DeviceTypeOptions.Add("Kapı Açma Ünitesi");
                DeviceTypeOptions.Add("Santral");
                DeviceTypeOptions.Add("Güç Kaynağı");
            }
        }

        #endregion

        #region Existing Properties

        /// <summary>
        /// İş kayıtları koleksiyonu
        /// </summary>
        public ObservableCollection<ServiceJob> ServiceJobs { get; set; }

        /// <summary>
        /// İş kayıtları görünümü (Filtreleme için)
        /// </summary>
        public ICollectionView ServiceJobsView => _serviceJobsView!;

        /// <summary>
        /// Müşteriler listesi (ComboBox için)
        /// </summary>
        public ObservableCollection<Customer> Customers { get; set; }

        /// <summary>
        /// Ürünler listesi (ComboBox için)
        /// </summary>
        public ObservableCollection<Product> Products { get; set; }

        /// <summary>
        /// İş türleri listesi (DEPRECATED)
        /// </summary>
        public ObservableCollection<JobType> JobTypes { get; set; }

        /// <summary>
        /// Kategori çoklu seçimi için (CheckBox binding)
        /// </summary>
        public ObservableCollection<CategorySelectItem> CategoryItems { get; set; }

        /// <summary>
        /// Müşterinin cihazları (Seçilen müşteriye göre filtrelenir)
        /// </summary>
        public ObservableCollection<CustomerAsset> CustomerAssets { get; set; } = new ObservableCollection<CustomerAsset>();

        /// <summary>
        /// Müşterinin projeleri (Seçilen müşteriye göre filtrelenir)
        /// </summary>
        public ObservableCollection<ServiceProject> CustomerProjects { get; set; } = new ObservableCollection<ServiceProject>();

        /// <summary>
        /// İş emri tipleri
        /// </summary>
        public ObservableCollection<WorkOrderType> WorkOrderTypes { get; } = new ObservableCollection<WorkOrderType>
        {
            WorkOrderType.Repair,
            WorkOrderType.Installation,
            WorkOrderType.Maintenance,
            WorkOrderType.Inspection,
            WorkOrderType.Replacement
        };

        /// <summary>
        /// Cihaz tipleri listesi
        /// </summary>
        public ObservableCollection<DeviceType> DeviceTypes { get; } = new ObservableCollection<DeviceType>(
            Enum.GetValues(typeof(DeviceType)).Cast<DeviceType>());

        #region Hybrid Asset Entry Properties

        /// <summary>
        /// Yeni cihaz mı giriliyor?
        /// </summary>
        public bool IsNewAsset
        {
            get => _isNewAsset;
            set
            {
                if (SetProperty(ref _isNewAsset, value))
                {
                    OnPropertyChanged(nameof(IsExistingAsset));
                    OnPropertyChanged(nameof(NewAssetFormVisible));
                }
            }
        }

        /// <summary>
        /// Mevcut cihaz mı seçiliyor?
        /// </summary>
        public bool IsExistingAsset
        {
            get => !IsNewAsset;
            set => IsNewAsset = !value;
        }

        /// <summary>
        /// Yeni cihaz formu görünür mü?
        /// </summary>
        public bool NewAssetFormVisible => IsNewAsset;

        /// <summary>
        /// Seçilen mevcut cihaz
        /// </summary>
        public CustomerAsset? SelectedAsset
        {
            get => _selectedAsset;
            set
            {
                if (SetProperty(ref _selectedAsset, value))
                {
                    // Cihaz seçildiğinde kategoriyi otomatik ayarla
                    if (value != null)
                    {
                        SelectedJobCategory = value.Category;
                    }
                }
            }
        }

        /// <summary>
        /// Yeni cihaz tipi
        /// </summary>
        public DeviceType NewAssetDeviceType
        {
            get => _newAssetDeviceType;
            set => SetProperty(ref _newAssetDeviceType, value);
        }

        /// <summary>
        /// Yeni cihaz markası
        /// </summary>
        public string NewAssetBrand
        {
            get => _newAssetBrand;
            set => SetProperty(ref _newAssetBrand, value);
        }

        /// <summary>
        /// Yeni cihaz modeli
        /// </summary>
        public string NewAssetModel
        {
            get => _newAssetModel;
            set => SetProperty(ref _newAssetModel, value);
        }

        /// <summary>
        /// Yeni cihaz seri numarası
        /// </summary>
        public string NewAssetSerialNumber
        {
            get => _newAssetSerialNumber;
            set => SetProperty(ref _newAssetSerialNumber, value);
        }

        /// <summary>
        /// Yeni cihaz konumu
        /// </summary>
        public string NewAssetLocation
        {
            get => _newAssetLocation;
            set => SetProperty(ref _newAssetLocation, value);
        }

        #endregion

        /// <summary>
        /// Arama metni
        /// </summary>
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    _ = LoadServiceJobs(); // API tabanlı arama
                }
            }
        }

        /// <summary>
        /// Durum filtresi
        /// </summary>
        public StatusFilter SelectedStatusFilter
        {
            get => _selectedStatusFilter;
            set
            {
                if (SetProperty(ref _selectedStatusFilter, value))
                {
                    _ = LoadServiceJobs(); // API tabanlı filtreleme
                }
            }
        }

        /// <summary>
        /// Durum filtre seçenekleri
        /// </summary>
        public ObservableCollection<StatusFilter> StatusFilters { get; } = new ObservableCollection<StatusFilter>
        {
            StatusFilter.All,
            StatusFilter.Pending,
            StatusFilter.InProgress,
            StatusFilter.Completed
        };

        /// <summary>
        /// Başlangıç tarihi filtresi
        /// </summary>
        public DateTime? FilterStartDate
        {
            get => _filterStartDate;
            set
            {
                if (SetProperty(ref _filterStartDate, value))
                {
                    _ = LoadServiceJobs(); // API tabanlı arama
                }
            }
        }

        /// <summary>
        /// Bitiş tarihi filtresi
        /// </summary>
        public DateTime? FilterEndDate
        {
            get => _filterEndDate;
            set
            {
                if (SetProperty(ref _filterEndDate, value))
                {
                    _ = LoadServiceJobs(); // API tabanlı arama
                }
            }
        }

        /// <summary>
        /// Seçili işe ait ürünler
        /// </summary>
        public ObservableCollection<ServiceJobItem> CurrentJobItems { get; set; }

        /// <summary>
        /// Seçili iş
        /// </summary>
        public ServiceJob? SelectedServiceJob
        {
            get => _selectedServiceJob;
            set
            {
                if (SetProperty(ref _selectedServiceJob, value))
                {
                    _ = LoadJobItems();
                }
            }
        }

        /// <summary>
        /// Seçili müşteri
        /// </summary>
        public Customer? SelectedCustomer
        {
            get => _selectedCustomer;
            set
            {
                if (SetProperty(ref _selectedCustomer, value))
                {
                    OnPropertyChanged(nameof(MapHtml));
                    OnPropertyChanged(nameof(SummaryCustomerName));
                    OnPropertyChanged(nameof(SummaryCustomerAddress));

                    // Müşteri değiştiğinde cihaz ve projeleri yükle
                    _ = LoadCustomerAssets();
                    _ = LoadCustomerProjects();
                }
            }
        }

        /// <summary>
        /// Summary: Müşteri adı
        /// </summary>
        public string SummaryCustomerName => SelectedCustomer?.FullName ?? "Seçilmedi";

        /// <summary>
        /// Summary: Müşteri adresi
        /// </summary>
        public string SummaryCustomerAddress => SelectedCustomer?.FullAddress ?? "-";

        /// <summary>
        /// Summary: Seçili kategoriler (çoklu)
        /// </summary>
        public string SummaryCategory => string.Join(", ", 
            CategoryItems?.Where(c => c.IsSelected).Select(c => c.DisplayName) ?? Array.Empty<string>())
            ?? "Seçilmedi";

        /// <summary>
        /// Seçili iş türü (DEPRECATED)
        /// </summary>
        public JobType SelectedJobType
        {
            get => _selectedJobType;
            set => SetProperty(ref _selectedJobType, value);
        }

        /// <summary>
        /// Seçili iş kategorisi (geriye uyumluluk - ilk seçili kategori)
        /// </summary>
        public JobCategory SelectedJobCategory
        {
            get => _selectedJobCategory;
            set
            {
                if (SetProperty(ref _selectedJobCategory, value))
                {
                    OnPropertyChanged(nameof(SummaryCategory));
                }
            }
        }



        /// <summary>
        /// İş açıklaması
        /// </summary>
        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }

        /// <summary>
        /// Eklenecek ürün
        /// </summary>
        public Product? SelectedProductToAdd
        {
            get => _selectedProductToAdd;
            set
            {
                if (SetProperty(ref _selectedProductToAdd, value) && value != null)
                {
                    // Varsayılan birim fiyatı ayarla
                    UnitPriceToAdd = value.SalePrice;
                }
            }
        }

        /// <summary>
        /// Eklenecek miktar
        /// </summary>
        public int QuantityToAdd
        {
            get => _quantityToAdd;
            set => SetProperty(ref _quantityToAdd, value);
        }

        #endregion

        #region Commands

        /// <summary>
        /// İş kaydet komutu
        /// </summary>
        public ICommand SaveServiceJobCommand { get; }

        /// <summary>
        /// İşe ürün ekle komutu
        /// </summary>
        public ICommand AddItemToJobCommand { get; }

        /// <summary>
        /// İşten ürün çıkar komutu
        /// </summary>
        public ICommand RemoveItemFromJobCommand { get; }

        /// <summary>
        /// İşi tamamla komutu (KRİTİK - STOK DÜŞME MANTIĞI)
        /// </summary>
        public ICommand CompleteJobCommand { get; }

        /// <summary>
        /// Formu temizle komutu
        /// </summary>
        public ICommand ClearFormCommand { get; }

        /// <summary>
        /// Yeni iş formunu aç
        /// </summary>
        public ICommand OpenNewJobFormCommand { get; }

        /// <summary>
        /// Listeyi yenile
        /// </summary>
        public ICommand RefreshListCommand { get; }

        /// <summary>
        /// İş detayı görüntüle
        /// </summary>
        public ICommand ViewJobDetailCommand { get; }

        /// <summary>
        /// Keşfi onayla ve malzeme adımından başlat
        /// </summary>
        public ICommand ApproveDiscoveryCommand { get; }


        /// <summary>
        /// PDF Yazdır komutu
        /// </summary>
        public ICommand PrintServiceFormCommand { get; }

        /// <summary>
        /// Hızlı cihaz ekle komutu
        /// </summary>
        /// <summary>
        /// Hızlı cihaz ekle komutu
        /// </summary>
        public ICommand AddAssetCommand { get; }

        /// <summary>
        /// İptal komutu
        /// </summary>
        public ICommand CancelCommand { get; }

        public ICommand EditJobCommand { get; }
        public ICommand BrowsePhotosCommand { get; }
        public ICommand RemovePhotoCommand { get; }

        /// <summary>
        /// Wizard ileri adım
        /// </summary>
        public ICommand GoNextStepCommand { get; }

        /// <summary>
        /// Wizard geri adım
        /// </summary>
        public ICommand GoPreviousStepCommand { get; }

        /// <summary>
        /// İş durumu değiştirme komutu
        /// </summary>
        public ICommand ChangeJobStatusCommand { get; }

        /// <summary>
        /// İş silme komutu
        /// </summary>
        public ICommand DeleteJobCommand { get; }

        /// <summary>
        /// İptal talebi event
        /// </summary>
        public event Action? CancelRequested;

        /// <summary>
        /// Kayıt/Güncelleme başarılı event (UX düzeltmesi)
        /// </summary>
        public event Action? SaveCompleted;

        #endregion

        /// <summary>
        /// Constructor
        /// </summary>


        #region Helper Methods

        /// <summary>
        /// Toplamları güncelle
        /// </summary>
        private void UpdateTotals()
        {
            OnPropertyChanged(nameof(MaterialTotal));
            OnPropertyChanged(nameof(GrandTotal));
            OnPropertyChanged(nameof(SubTotal));
            OnPropertyChanged(nameof(KdvAmount));
            OnPropertyChanged(nameof(GrandTotalWithKdv));
        }

        #endregion

        #region Filtering

        /// <summary>
        /// Servis işi filtreleme metodu (Composite Filter)
        /// </summary>
        private bool FilterServiceJobs(object obj)
        {
            // İstemci tarafında (Client-Side) yapılan filtreleme, LoadServiceJobs API çağrısı ile yer değiştirilmiştir.
            return true;
        }

        /// <summary>
        /// Yeni iş formunu aç
        /// </summary>
        private void OpenNewJobForm()
        {
            // Create a new ViewModel with dependencies
            var newVm = new ServiceJobViewModel(_navigationService, _toastService, _loadingService, _apiClient);
            
            // Veri aktarımı (YENI: API cagrisini beklememek icin ana VM'den listeleri gonderiyoruz)
            foreach (var cust in Customers) newVm.Customers.Add(cust);
            foreach (var prod in Products) newVm.Products.Add(prod);
            foreach (var tech in Technicians) newVm.Technicians.Add(tech);
            foreach (var asset in CustomerAssets) newVm.CustomerAssets.Add(asset);

            var window = new NewServiceJobWindow(newVm);
            window.Owner = System.Windows.Application.Current.MainWindow;
            var result = window.ShowDialog();

            if (result == true)
            {
                RefreshList();
            }
        }

        /// <summary>
        /// Listeyi yenile
        /// </summary>
        private async void RefreshList()
        {
            await LoadServiceJobs();
            _serviceJobsView?.Refresh();
        }

        /// <summary>
        /// İş detayını görüntüle
        /// </summary>
        private void ViewJobDetail(ServiceJob? job)
        {
            if (job == null) return;
            MessageBox.Show($"İş Detayı: #{job.Id}\n{job.Description}", "Detay", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void EditJob(ServiceJob? job)
        {
            if (job == null) return;

            ClearForm();
            SelectedServiceJob = job;
            _isEditing = true;

            Description = job.Description ?? string.Empty;
            IsDiscoveryOnly = job.WorkOrderType == WorkOrderType.Discovery;
            ScheduledDate = job.ScheduledDate;
            SelectedPriority = job.Priority;
            LaborCost = job.LaborCost;
            DiscountAmount = job.DiscountAmount;
            TechnicianNotes = job.TechnicianNotes;
            EstimatedDuration = job.EstimatedDuration;
            SlaDeadline = job.SlaDeadline;
            SelectedTechnicianId = job.AssignedTechnicianId;

            SelectedCustomer = Customers.FirstOrDefault(c => c.Id == job.CustomerId);
            if (SelectedCustomer != null && job.CustomerAssetId.HasValue)
            {
                IsExistingAsset = true;
                SelectedAsset = CustomerAssets.FirstOrDefault(a => a.Id == job.CustomerAssetId.Value);
            }

            if (!string.IsNullOrEmpty(job.CategoriesJson))
            {
                var jobCats = JsonSerializer.Deserialize<System.Collections.Generic.List<int>>(job.CategoriesJson);
                if (jobCats != null)
                {
                    foreach (var cat in CategoryItems)
                        cat.IsSelected = jobCats.Contains((int)cat.Category);
                }
            }

            if (job.ServiceJobItems != null)
            {
                foreach (var item in job.ServiceJobItems)
                    CurrentJobItems.Add(item);
            }

            var photos = job.PhotoPathsList;
            if (photos != null)
            {
                foreach (var p in photos) UploadedPhotos.Add(p);
            }

            CurrentWizardStep = 1;
            
            // Edit işlemi için yeni pencereyi kendi contextimiz ile açıyoruz
            var window = new NewServiceJobWindow(this);
            window.Owner = System.Windows.Application.Current.MainWindow;
            var result = window.ShowDialog();

            if (result == true) RefreshList();
        }

        /// <summary>
        /// Keşfi onayla ve malzeme seçimiyle işe dönüştür
        /// </summary>
        private void ApproveDiscovery(ServiceJob? job)
        {
            if (job == null) return;

            ClearForm();
            SelectedServiceJob = job;
            _isEditing = true;

            Description = job.Description ?? string.Empty;
            IsDiscoveryOnly = false; // Artık keşif değil, normal arıza işi gibi devam edecek
            job.WorkOrderType = WorkOrderType.Repair; // Tip kalıcı olarak değişiyor
            
            ScheduledDate = job.ScheduledDate;
            SelectedPriority = job.Priority;
            LaborCost = job.LaborCost;
            DiscountAmount = job.DiscountAmount;
            TechnicianNotes = job.TechnicianNotes;
            EstimatedDuration = job.EstimatedDuration;
            SlaDeadline = job.SlaDeadline;
            SelectedTechnicianId = job.AssignedTechnicianId;

            SelectedCustomer = Customers.FirstOrDefault(c => c.Id == job.CustomerId);
            if (SelectedCustomer != null && job.CustomerAssetId.HasValue)
            {
                IsExistingAsset = true;
                SelectedAsset = CustomerAssets.FirstOrDefault(a => a.Id == job.CustomerAssetId.Value);
            }

            if (!string.IsNullOrEmpty(job.CategoriesJson))
            {
                var jobCats = JsonSerializer.Deserialize<System.Collections.Generic.List<int>>(job.CategoriesJson);
                if (jobCats != null)
                {
                    foreach (var cat in CategoryItems)
                        cat.IsSelected = jobCats.Contains((int)cat.Category);
                }
            }

            if (job.ServiceJobItems != null)
            {
                foreach (var item in job.ServiceJobItems)
                    CurrentJobItems.Add(item);
            }

            var photos = job.PhotoPathsList;
            if (photos != null)
            {
                foreach (var p in photos) UploadedPhotos.Add(p);
            }

            CurrentWizardStep = 3; // Doğrudan malzeme seçimine atla
            
            // Yeni pencereyi kendi contextimiz ile açıyoruz
            var window = new NewServiceJobWindow(this);
            window.Owner = System.Windows.Application.Current.MainWindow;
            var result = window.ShowDialog();

            if (result == true) RefreshList();
        }

        private void BrowsePhotos()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Multiselect = true,
                Filter = "Resim Dosyaları|*.jpg;*.jpeg;*.png;*.webp"
            };

            if (dialog.ShowDialog() == true)
            {
                foreach (var file in dialog.FileNames)
                {
                    if (!UploadedPhotos.Contains(file))
                        UploadedPhotos.Add(file);
                }
            }
        }

        private void RemovePhoto(string? path)
        {
            if (path != null && UploadedPhotos.Contains(path))
                UploadedPhotos.Remove(path);
        }

        #endregion

        #region Data Loading

        /// <summary>
        /// Tüm verileri yükle
        /// </summary>
        /// <summary>
        /// Tüm verileri yükle
        /// </summary>
        private async Task LoadData()
        {
            if (_loadingService != null) 
                _loadingService.Show("İşler yükleniyor...");
            
            await Task.Delay(300);

            try
            {
                await LoadCustomers();
                await LoadProducts();
                await LoadServiceJobs();
                await LoadDashboardAsync();
                await LoadTechnicians();
            }
            finally
            {
                _loadingService?.Hide();
            }
        }

        /// <summary>
        /// Müşterileri yükle
        /// </summary>
        private async Task LoadCustomers()
        {
            var response = await _apiClient.GetAsync<List<Customer>>("api/customers?pageSize=1000"); // Varsayılan limitsiz veya yüksek limitli
            if (response != null && response.Success && response.Data != null)
            {
                // UI Thread üzerinden çalıştığından emin olmak için Dispatcher gerekebilir ama ViewModel bazlı collection update'leri WPF halleder (ObservableCollection)
                Customers.Clear();
                foreach (var customer in response.Data) Customers.Add(customer);
            }
        }

        /// <summary>
        /// Ürünleri yükle
        /// </summary>
        private async Task LoadProducts()
        {
            var response = await _apiClient.GetAsync<List<Product>>("api/products?pageSize=1000");
            if (response != null && response.Success && response.Data != null)
            {
                Products.Clear();
                foreach (var product in response.Data) Products.Add(product);
            }
        }

        /// <summary>
        /// Seçilen müşterinin cihazlarını yükle
        /// </summary>
        private async Task LoadCustomerAssets()
        {
            CustomerAssets.Clear();
            if (SelectedCustomer == null) return;

            try
            {
                var response = await _apiClient.GetAsync<List<CustomerAsset>>($"api/customers/{SelectedCustomer.Id}/assets");
                if (response != null && response.Success && response.Data != null)
                {
                    foreach (var asset in response.Data) CustomerAssets.Add(asset);
                }
            }
            catch (Exception ex)
            {
                // Asset tablosu henüz oluşturulmamış olabilir veya endpoint hatası
                _toastService.ShowError($"Cihazlar yüklenemedi: {ex.Message}");
            }
        }

        /// <summary>
        /// Seçilen müşterinin projelerini yükle
        /// </summary>
        private async Task LoadCustomerProjects()
        {
            CustomerProjects.Clear();
            if (SelectedCustomer == null) return;

            try
            {
                var response = await _apiClient.GetAsync<List<ServiceProject>>($"api/customers/{SelectedCustomer.Id}/projects");
                if (response != null && response.Success && response.Data != null)
                {
                    foreach (var project in response.Data) CustomerProjects.Add(project);
                }
            }
            catch (Exception ex)
            {
                // Project tablosu henüz oluşturulmamış olabilir
                _toastService.ShowError($"Projeler yüklenemedi: {ex.Message}");
            }
        }

        /// <summary>
        /// Hızlı cihaz ekleme popup'ını aç
        /// </summary>
        private void OpenQuickAssetAdd()
        {
            if (SelectedCustomer == null)
            {
                System.Windows.MessageBox.Show("Lütfen önce müşteri seçin.", "Uyarı",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            var window = new Views.QuickAssetAddWindow(SelectedCustomer.Id);
            if (window.ShowDialog() == true && window.CreatedAsset != null)
            {
                // Listeye ekle ve seç
                CustomerAssets.Add(window.CreatedAsset);
                _toastService.ShowSuccess($"Cihaz eklendi: {window.CreatedAsset.FullName}");
            }
        }

        /// <summary>
        /// İş kayıtlarını yükle
        /// </summary>
        private async Task LoadServiceJobs()
        {
            var url = "api/servicejobs?pageSize=50";

            // Arama ve Filtreleme API Parametreleri (Client-Side yerine API'ye devredildi)
            if (!string.IsNullOrWhiteSpace(SearchText))
                url += $"&search={Uri.EscapeDataString(SearchText)}";

            if (FilterStartDate.HasValue)
                url += $"&startDate={FilterStartDate.Value:yyyy-MM-dd}";

            if (FilterEndDate.HasValue)
                url += $"&endDate={FilterEndDate.Value:yyyy-MM-dd}";

            if (SelectedStatusFilter != StatusFilter.All)
            {
                // Enum map mapping
                int statusParam = SelectedStatusFilter switch
                {
                    StatusFilter.Pending => 0,
                    StatusFilter.InProgress => 1,
                    StatusFilter.Completed => 2,
                    _ => 0
                };
                url += $"&status={statusParam}";
            }

            var response = await _apiClient.GetAsync<List<ServiceJob>>(url);
            if (response != null && response.Success && response.Data != null)
            {
                ServiceJobs.Clear();
                foreach (var job in response.Data) ServiceJobs.Add(job);
            }
        }

        /// <summary>
        /// Seçili işe ait ürünleri yükle
        /// </summary>
        private async Task LoadJobItems()
        {
            CurrentJobItems.Clear();

            if (SelectedServiceJob != null)
            {
                var response = await _apiClient.GetAsync<ServiceJob>($"api/servicejobs/{SelectedServiceJob.Id}");
                if (response != null && response.Success && response.Data != null && response.Data.ServiceJobItems != null)
                {
                    foreach (var item in response.Data.ServiceJobItems)
                    {
                        CurrentJobItems.Add(item);
                    }
                }
            }
        }

        #endregion

        #region Service Job Operations

        /// <summary>
        /// İş kaydetme kontrolü
        /// </summary>
        private bool CanSaveServiceJob()
        {
            // Keşif sırasında müşteri seçiliyse description istenmeden kayıt yapılabilir (Hızlı Kayıt)
            if (IsDiscoveryOnly && SelectedCustomer != null) return true;
            return SelectedCustomer != null && !string.IsNullOrWhiteSpace(Description);
        }

        /// <summary>
        /// Yeni iş kaydet (Hibrit Cihaz Desteği ile)
        /// </summary>
        private async void SaveServiceJob()
        {
            try
            {
                int? assetId = null;

                // === ADIM 0: Hızlı Yeni Müşteri Mı? ===
                if (IsQuickAddCustomer)
                {
                    if (string.IsNullOrWhiteSpace(QuickCustomerName) || string.IsNullOrWhiteSpace(QuickCustomerPhone))
                    {
                        MessageBox.Show("Yeni müşteri için Ad Soyad ve Telefon zorunludur.", "Uyarı",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    var newCustomer = new Customer
                    {
                        FullName = QuickCustomerName.Trim(),
                        PhoneNumber = QuickCustomerPhone.Trim(),
                        Type = CustomerType.Individual,
                        CreatedDate = DateTime.Now
                    };
                    
                    if (_loadingService != null) _loadingService.Show("Müşteri kaydediliyor...");
                    var custResponse = await _apiClient.PostAsync<Customer>("api/customers", newCustomer);
                    if (_loadingService != null) _loadingService.Hide();

                    if (custResponse != null && custResponse.Success && custResponse.Data != null)
                    {
                        Customers.Add(custResponse.Data);
                        SelectedCustomer = custResponse.Data;
                        IsQuickAddCustomer = false; // Reset
                        _toastService?.ShowSuccess($"Yeni müşteri eklendi: {custResponse.Data.FullName}");
                    }
                    else
                    {
                        _toastService?.ShowError("Yeni müşteri kaydedilemedi.");
                        return;
                    }
                }

                if (SelectedCustomer == null)
                {
                    MessageBox.Show("Lütfen müşteri seçin veya oluşturun.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // === ADIM 1: Yeni cihaz mı? Önce onu oluştur ===
                if (IsNewAsset)
                {
                    // Validasyon
                    if (string.IsNullOrWhiteSpace(NewAssetBrand) || string.IsNullOrWhiteSpace(NewAssetModel))
                    {
                        MessageBox.Show("Yeni cihaz için Marka ve Model zorunludur.", "Uyarı",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    var newAsset = new CustomerAsset
                    {
                        CustomerId = SelectedCustomer!.Id,
                        Category = SelectedJobCategory,
                        Brand = NewAssetBrand.Trim(),
                        Model = NewAssetModel.Trim(),
                        SerialNumber = string.IsNullOrWhiteSpace(NewAssetSerialNumber) ? null : NewAssetSerialNumber.Trim(),
                        Location = string.IsNullOrWhiteSpace(NewAssetLocation) ? null : NewAssetLocation.Trim(),
                        Status = AssetStatus.NeedsRepair, // Arıza ile geliyor
                        CreatedDate = DateTime.Now
                    };

                    // API'ye asset ekleme isteği (API'de bu endpoint'i açmamız gerekirse CustomersController'a bir POST eklenmeli - şimdilik eklediğimiz varsayımıyla devam ediyoruz)
                    var assetResponse = await _apiClient.PostAsync<CustomerAsset>($"api/customers/{SelectedCustomer.Id}/assets", newAsset);
                    
                    if (assetResponse != null && assetResponse.Success && assetResponse.Data != null)
                    {
                        assetId = assetResponse.Data.Id;
                        CustomerAssets.Add(assetResponse.Data);
                        _toastService.ShowSuccess($"Cihaz kaydedildi: {assetResponse.Data.FullName}");
                    }
                    else
                    {
                        _toastService.ShowError("Cihaz kaydedilemedi.");
                        return;
                    }
                }
                else if (SelectedAsset != null)
                {
                    assetId = SelectedAsset.Id;
                }

                // === ADIM 2: Kategorileri JSON olarak kaydet ===
                var selectedCategories = CategoryItems?
                    .Where(c => c.IsSelected)
                    .Select(c => (int)c.Category)
                    .ToList() ?? new List<int>();
                string categoriesJson = JsonSerializer.Serialize(selectedCategories);

                // === ADIM 3: İş emrini oluştur/güncelle ===
                var jobToSave = _isEditing && SelectedServiceJob != null ? SelectedServiceJob : new ServiceJob();

                jobToSave.CustomerId = SelectedCustomer!.Id;
                jobToSave.CustomerAssetId = assetId;
                jobToSave.WorkOrderType = WorkOrderType.Repair;
                jobToSave.JobCategory = selectedCategories.Any() ? (JobCategory)selectedCategories.First() : JobCategory.CCTV;
                jobToSave.CategoriesJson = categoriesJson;

                // Keşif talebi için Description boşsa otomatik doldur
                if (IsDiscoveryOnly && string.IsNullOrWhiteSpace(Description))
                {
                    Description = "Keşif Talebi";
                }
                jobToSave.Description = Description;
                
                if (!_isEditing) 
                {
                    jobToSave.Status = JobStatus.Pending;
                    jobToSave.CreatedDate = DateTime.Now;
                }
                
                jobToSave.ScheduledDate = ScheduledDate;
                jobToSave.AssignedTechnician = AssignedTechnician;
                jobToSave.AssignedTechnicianId = SelectedTechnicianId;
                jobToSave.Priority = SelectedPriority;
                jobToSave.LaborCost = LaborCost;
                jobToSave.DiscountAmount = DiscountAmount;
                jobToSave.EstimatedDuration = EstimatedDuration;
                jobToSave.SlaDeadline = SlaDeadline;
                jobToSave.TechnicianNotes = TechnicianNotes;
                jobToSave.PhotoPathsJson = JsonSerializer.Serialize(UploadedPhotos.ToList());
                jobToSave.WorkOrderType = IsDiscoveryOnly ? WorkOrderType.Discovery : WorkOrderType.Repair;

                // Seçilen ürünler (ServiceJobItem)
                jobToSave.ServiceJobItems = CurrentJobItems.ToList();

                if (_loadingService != null) _loadingService.Show("İş emri kaydediliyor...");
                if (_isEditing)
                {
                    var response = await _apiClient.PutAsync<ServiceJob>($"api/servicejobs/{jobToSave.Id}", jobToSave);
                    if (_loadingService != null) _loadingService.Hide();

                    if (response != null && response.Success)
                    {
                        await LoadServiceJobs();
                        ClearForm();
                        _toastService?.ShowSuccess("İş kaydı başarıyla güncellendi!");
                        SaveCompleted?.Invoke();
                    }
                    else
                    {
                        _toastService?.ShowError(response?.Message ?? "İş güncellenemedi");
                    }
                }
                else
                {
                    var response = await _apiClient.PostAsync<ServiceJob>("api/servicejobs", jobToSave);
                    if (_loadingService != null) _loadingService.Hide();

                    if (response != null && response.Success)
                    {
                        await LoadServiceJobs();
                        ClearForm();
                        _toastService?.ShowSuccess("İş kaydı başarıyla oluşturuldu!");
                        SaveCompleted?.Invoke();
                    }
                    else
                    {
                        _toastService?.ShowError(response?.Message ?? "İş kaydedilemedi");
                    }
                }
            }
            catch (Exception ex)
            {
                _toastService.ShowError($"Hata: {ex.Message}");
            }
        }

        /// <summary>
        /// Ürün ekleme kontrolü
        /// </summary>
        private bool CanAddItem()
        {
            return SelectedProductToAdd != null && QuantityToAdd > 0;
        }

        /// <summary>
        /// İşe ürün ekle
        /// </summary>
        private void AddItemToJob()
        {
            if (SelectedProductToAdd == null) return;

            var newItem = new ServiceJobItem
            {
                ProductId = SelectedProductToAdd.Id,
                Product = SelectedProductToAdd,
                QuantityUsed = QuantityToAdd,
                UnitPrice = UnitPriceToAdd,
                UnitCost = SelectedProductToAdd.PurchasePrice
            };

            CurrentJobItems.Add(newItem);
            SelectedProductToAdd = null;
            QuantityToAdd = 1;
            UnitPriceToAdd = 0;
        }

        /// <summary>
        /// İşten ürün çıkar
        /// </summary>
        private void RemoveItemFromJob(ServiceJobItem? item)
        {
            if (item != null)
            {
                CurrentJobItems.Remove(item);
            }
        }

        /// <summary>
        /// İşi tamamlama kontrolü
        /// </summary>
        private bool CanCompleteJob()
        {
            return SelectedServiceJob != null &&
                   SelectedServiceJob.Status != JobStatus.Completed;
        }

        /// <summary>
        /// İşi tamamla - KRİTİK İŞ MANTIĞI: STOK DÜŞME
        /// </summary>
        private async void CompleteJob()
        {
            if (SelectedServiceJob == null) return;

            try
            {
                var response = await _apiClient.PatchAsync<object>($"api/servicejobs/{SelectedServiceJob.Id}/status", new { Status = JobStatus.Completed });

                if (response != null && response.Success)
                {
                    SelectedServiceJob.Status = JobStatus.Completed;
                    SelectedServiceJob.CompletedDate = DateTime.Now;
                    
                    await LoadServiceJobs();
                    await LoadProducts();

                    _toastService.ShowSuccess("İş başarıyla tamamlandı!\nStok miktarları güncellendi.");
                }
                else
                {
                    _toastService.ShowError(response?.Message ?? "İş tamamlanamadı.");
                }
            }
            catch (Exception ex)
            {
                _toastService.ShowError($"Hata: {ex.Message}");
            }
        }

        /// <summary>
        /// Formu temizle
        /// </summary>
        private void ClearForm()
        {
            _isEditing = false;
            IsQuickAddCustomer = false;
            UploadedPhotos.Clear();
            SelectedTechnicianId = null;
            EstimatedDuration = null;
            SlaDeadline = null;
            TechnicianNotes = null;
            KdvRate = 20m;
            QuickCustomerName = string.Empty;
            QuickCustomerPhone = string.Empty;

            // Single-page form reset
            SelectedStructureType = StructureType.SingleUnit;
            SelectedCustomer = null;
            SelectedJobType = JobType.SecurityCamera;
            SelectedJobCategory = JobCategory.CCTV;
            // Kategorileri temizle
            if (CategoryItems != null)
            {
                foreach (var item in CategoryItems)
                {
                    item.IsSelected = false;
                }
            }
            Description = string.Empty;
            SelectedProductToAdd = null;
            QuantityToAdd = 1;
            UnitPriceToAdd = 0;
            ScheduledDate = null;
            AssignedTechnician = null;
            SelectedPriority = JobPriority.Normal;
            LaborCost = 0;
            DiscountAmount = 0;
            CurrentJobItems.Clear();
        }

        /// <summary>
        /// Seçilen kategoriye göre JobDetail instance oluştur
        /// </summary>
        private JobDetailBase CreateJobDetailInstance(JobCategory category)
        {
            return category switch
            {
                JobCategory.CCTV => new CctvJobDetail(),
                JobCategory.VideoIntercom => new VideoIntercomJobDetail(),
                JobCategory.FireAlarm => new FireAlarmJobDetail(),
                JobCategory.BurglarAlarm => new BurglarAlarmJobDetail(),
                JobCategory.SmartHome => new SmartHomeJobDetail(),
                JobCategory.AccessControl => new AccessControlJobDetail(),
                JobCategory.SatelliteSystem => new SatelliteJobDetail(),
                JobCategory.FiberOptic => new FiberOpticJobDetail(),
                _ => new CctvJobDetail()
            };
        }

        /// <summary>
        /// Servis formunu PDF olarak yazdır
        /// </summary>
        private async void PrintServiceForm(ServiceJob? job)
        {
            if (job == null) return;

            try
            {
                // İş kaydını tam API üzerinden yükle
                var response = await _apiClient.GetAsync<ServiceJob>($"api/servicejobs/{job.Id}");
                var fullJob = response?.Data;

                if (fullJob == null)
                {
                    _toastService.ShowError("İş kaydı bulunamadı.");
                    return;
                }

                // SaveFileDialog göster
                var saveDialog = new SaveFileDialog
                {
                    Title = "Servis Formunu Kaydet",
                    Filter = "PDF Dosyası (*.pdf)|*.pdf",
                    FileName = $"ServisFormu_{fullJob.Id:D6}.pdf",
                    DefaultExt = ".pdf"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    // PDF oluştur
                    var pdfService = new PdfService();
                    pdfService.GenerateServiceForm(fullJob, saveDialog.FileName);

                    // PDF'i aç
                    var processInfo = new ProcessStartInfo
                    {
                        FileName = saveDialog.FileName,
                        UseShellExecute = true
                    };
                    Process.Start(processInfo);

                    _toastService.ShowSuccess("Servis formu başarıyla oluşturuldu.");
                }
            }
            catch (Exception ex)
            {
                _toastService.ShowError($"PDF oluşturulurken hata: {ex.Message}");
            }
        }

        #endregion

        #region Dashboard & Wizard Methods

        /// <summary>
        /// Dashboard istatistiklerini API'den yükle
        /// </summary>
        public async Task LoadDashboardAsync()
        {
            try
            {
                // Tip güvenliği (DTO) sağlandı
                var response = await _apiClient.GetAsync<KamatekCrm.Shared.DTOs.ServiceJobStatsResponseDto>("api/servicejobs/stats");
                if (response != null && response.Success && response.Data != null)
                {
                    var data = response.Data;
                    TotalJobCount = data.TotalJobs;
                    PendingCount = data.PendingJobs;
                    InProgressCount = data.InProgressJobs;
                    CompletedCount = data.CompletedJobs;
                    SlaBreachedCount = data.SlaBreachedJobs;
                    TodayCreatedCount = data.TodayCreated;
                    AvgCompletionHours = data.AvgCompletionHours;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Dashboard stats yüklenemedi: {ex.Message}");
            }
        }

        /// <summary>
        /// Teknisyen listesini API'den yükle
        /// </summary>
        private async Task LoadTechnicians()
        {
            try
            {
                var response = await _apiClient.GetAsync<List<User>>("api/users?pageSize=100");
                if (response != null && response.Success && response.Data != null)
                {
                    Technicians.Clear();
                    foreach (var user in response.Data.Where(u => u.Role == "Personel" || u.Role == "Admin"))
                    {
                        Technicians.Add(user);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Teknisyenler yüklenemedi: {ex.Message}");
            }
        }

        /// <summary>
        /// Seçili işin tarihçesini yükle
        /// </summary>
        private async Task LoadSelectedJobHistory()
        {
            SelectedJobHistory.Clear();
            if (SelectedServiceJob == null) return;

            try
            {
                var response = await _apiClient.GetAsync<List<ServiceJobHistory>>($"api/servicejobs/{SelectedServiceJob.Id}/history");
                if (response != null && response.Success && response.Data != null)
                {
                    foreach (var h in response.Data) SelectedJobHistory.Add(h);
                }
            }
            catch { /* Tarihçe opsiyonel */ }
        }

        /// <summary>
        /// Wizard ileri adım
        /// </summary>
        private void GoNextStep()
        {
            if (CurrentWizardStep == 2 && IsDiscoveryOnly)
                CurrentWizardStep = 4; // Skip Malzeme (Step 3)
            else if (CurrentWizardStep < TotalWizardSteps)
                CurrentWizardStep++;
        }

        /// <summary>
        /// Wizard geri adım
        /// </summary>
        private void GoPreviousStep()
        {
            if (CurrentWizardStep == 4 && IsDiscoveryOnly)
                CurrentWizardStep = 2; // Skip Malzeme (Step 3) back
            else if (CurrentWizardStep > 1)
                CurrentWizardStep--;
        }

        /// <summary>
        /// Wizard ileri adım izin kontrolü (per-step validation)
        /// </summary>
        private bool CanGoNextStep()
        {
            return CurrentWizardStep switch
            {
                1 => IsQuickAddCustomer 
                        ? (!string.IsNullOrWhiteSpace(QuickCustomerName) && !string.IsNullOrWhiteSpace(QuickCustomerPhone)) 
                        : SelectedCustomer != null,
                2 => !string.IsNullOrWhiteSpace(Description), // Açıklama girilmiş olmalı
                3 => true, // Malzeme opsiyonel
                _ => false
            };
        }

        /// <summary>
        /// İş durumunu değiştir (Dashboard context menu)
        /// </summary>
        private async void ChangeJobStatus(object? param)
        {
            if (SelectedServiceJob == null || param == null) return;

            try
            {
                JobStatus newStatus;
                if (param is JobStatus js)
                    newStatus = js;
                else if (Enum.TryParse<JobStatus>(param.ToString(), out var parsed))
                    newStatus = parsed;
                else
                    return;

                var response = await _apiClient.PatchAsync<object>($"api/servicejobs/{SelectedServiceJob.Id}/status", 
                    new { Status = newStatus });

                if (response != null && response.Success)
                {
                    SelectedServiceJob.Status = newStatus;
                    if (newStatus == JobStatus.Completed)
                        SelectedServiceJob.CompletedDate = DateTime.Now;

                    await LoadServiceJobs();
                    await LoadDashboardAsync();
                    _toastService?.ShowSuccess($"İş #{SelectedServiceJob.Id} durumu güncellendi: {newStatus}");
                }
                else
                {
                    _toastService?.ShowError(response?.Message ?? "Durum güncellenemedi.");
                }
            }
            catch (Exception ex)
            {
                _toastService?.ShowError($"Hata: {ex.Message}");
            }
        }

        /// <summary>
        /// İş sil (Dashboard context menu)
        /// </summary>
        private async void DeleteJob()
        {
            if (SelectedServiceJob == null) return;

            var result = MessageBox.Show(
                $"İş #{SelectedServiceJob.Id} silinecek. Emin misiniz?",
                "Silme Onayı", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                var response = await _apiClient.DeleteAsync<object>($"api/servicejobs/{SelectedServiceJob.Id}");
                if (response != null && response.Success)
                {
                    await LoadServiceJobs();
                    await LoadDashboardAsync();
                    _toastService?.ShowSuccess("İş kaydı silindi.");
                }
                else
                {
                    _toastService?.ShowError(response?.Message ?? "Silinemedi.");
                }
            }
            catch (Exception ex)
            {
                _toastService?.ShowError($"Hata: {ex.Message}");
            }
        }

        #endregion
    }
}
