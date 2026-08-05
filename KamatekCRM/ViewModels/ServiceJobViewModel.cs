using System;
using KamatekCrm.ApplicationCore.DTOs.ServiceJobs;
using KamatekCrm.ApplicationCore.Interfaces;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;
using KamatekCrm.Shared.Services;
using KamatekCrm.Services;
using KamatekCrm.Views;
using KamatekCrm.Validation;

namespace KamatekCrm.ViewModels
{
    public sealed record ServiceJobStatusFilterOption(StatusFilter Value, string Label);

    /// <summary>
    /// İş kaydı ViewModel - Wizard UI ile KRİTİK İŞ MANTIĞI İÇERİR
    /// </summary>
    public partial class ServiceJobViewModel : ViewModelBase
    {
        private readonly NavigationService _navigationService;
        private readonly IToastService _toastService;
        private readonly ILoadingService _loadingService;
        private readonly IServiceJobCommandService _serviceJobCommandService;
        private readonly IServiceJobReadService _serviceJobReadService;
        private readonly PdfService _pdfService;
        private readonly IDialogService _dialogService;
        private ServiceJobRowDto? _selectedServiceJob;
        private CancellationTokenSource? _searchCts;

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
        private ObservableCollection<ServiceJobHistoryDto> _selectedJobHistory = new();

        public ServiceJobViewModel(
            NavigationService navigationService,
            IToastService toastService,
            ILoadingService loadingService,
            IServiceJobCommandService serviceJobCommandService,
            IServiceJobReadService serviceJobReadService,
            PdfService pdfService,
            IDialogService dialogService)
        {
            _navigationService = navigationService;
            _toastService = toastService;
            _loadingService = loadingService;
            _serviceJobCommandService = serviceJobCommandService;
            _serviceJobReadService = serviceJobReadService;
            _pdfService = pdfService;
            _dialogService = dialogService;

            ServiceJobs = new ObservableCollection<ServiceJobRowDto>();
            Customers = new ObservableCollection<ServiceJobCustomerLookupDto>();
            Products = new ObservableCollection<ServiceJobProductLookupDto>();
            Technicians = new ObservableCollection<ServiceJobTechnicianLookupDto>();
            CurrentJobItems = new ObservableCollection<ServiceJobMaterialDto>();
            CurrentJobItems.CollectionChanged += (s, e) =>
            {
                UpdateTotals();
                OnPropertyChanged(nameof(ItemCount));
            };
            ErrorsChanged += (_, _) => RefreshSaveState();

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

            // Wizard komutları

            // Dashboard & durum değiştirme komutları

            _ = Refresh();
            UpdateDeviceTypeOptions();
        }

        public ServiceJobViewModel() : this(null!, null!, null!, null!, null!, null!, null!)
        {
        }



        private ServiceJobCustomerLookupDto? _selectedCustomer;
        private JobCategory _selectedJobCategory; // Geriye uyumluluk için
        private string _description = string.Empty;
        private ServiceJobProductLookupDto? _selectedProductToAdd;
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
        private ServiceJobAssetLookupDto? _selectedAsset;
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

        [Range(1, 1000, ErrorMessage = "Blok sayısı 1-1000 arasında olmalıdır.")]
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

        [Range(1, 100000, ErrorMessage = "Daire sayısı 1-100000 arasında olmalıdır.")]
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
        [Range(1, 100000, ErrorMessage = "Tahmini süre 1 dakikadan büyük olmalıdır.")]
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

        public ObservableCollection<ServiceJobTechnicianLookupDto> Technicians { get; set; }

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

        public ObservableCollection<ServiceJobHistoryDto> SelectedJobHistory
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
        [Range(typeof(decimal), "0", "999999999", ErrorMessage = "İşçilik ücreti negatif olamaz.")]
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
        [Range(typeof(decimal), "0", "999999999", ErrorMessage = "İndirim tutarı negatif olamaz.")]
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
                {
                    ValidateProperty(QuickCustomerName, nameof(QuickCustomerName));
                    ValidateProperty(QuickCustomerPhone, nameof(QuickCustomerPhone));
                    (GoNextStepCommand as CommunityToolkit.Mvvm.Input.IRelayCommand)?.NotifyCanExecuteChanged();
                    RefreshSaveState();
                }
            }
        }

        private bool _isDiscoveryOnly;
        /// <summary>
        /// Yalnızca keşif yapılacak seçeneği
        /// </summary>
        public bool IsDiscoveryOnly
        {
            get => _isDiscoveryOnly;
            set
            {
                if (SetProperty(ref _isDiscoveryOnly, value))
                {
                    ValidateProperty(Description, nameof(Description));
                    RefreshSaveState();
                }
            }
        }

        /// <summary>
        /// Hızlı müşteri adı
        /// </summary>
        [RequiredWhen(nameof(IsQuickAddCustomer), true, ErrorMessage = "Yeni müşteri adı zorunludur.")]
        [StringLength(150, ErrorMessage = "Müşteri adı en fazla 150 karakter olabilir.")]
        public string QuickCustomerName
        {
            get => _quickCustomerName;
            set
            {
                if (SetProperty(ref _quickCustomerName, value))
                {
                    (GoNextStepCommand as CommunityToolkit.Mvvm.Input.IRelayCommand)?.NotifyCanExecuteChanged();
                    RefreshSaveState();
                }
            }
        }

        /// <summary>
        /// Hızlı müşteri telefonu
        /// </summary>
        [RequiredWhen(nameof(IsQuickAddCustomer), true, ErrorMessage = "Yeni müşteri telefonu zorunludur.")]
        [RegularExpression(@"^\+?[0-9\s()\-]{10,20}$", ErrorMessage = "Geçerli bir telefon numarası girin.")]
        public string QuickCustomerPhone
        {
            get => _quickCustomerPhone;
            set
            {
                if (SetProperty(ref _quickCustomerPhone, value))
                {
                    (GoNextStepCommand as CommunityToolkit.Mvvm.Input.IRelayCommand)?.NotifyCanExecuteChanged();
                    RefreshSaveState();
                }
            }
        }

        /// <summary>
        /// Kaydediliyor mu? (Spinner için)
        /// </summary>
        public bool IsSaving
        {
            get => _isSaving;
            set
            {
                if (SetProperty(ref _isSaving, value))
                {
                    RefreshSaveState();
                }
            }
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
        /// Kaydet butonunun pasif kalma nedenini kullanıcıya sunar (ToolTip binding)
        /// </summary>
        public string SaveDisabledReason
        {
            get
            {
                if (IsSaving)
                    return "Kayıt işlemi devam ediyor.";

                if (!IsQuickAddCustomer && SelectedCustomer is null)
                    return "Lütfen bir müşteri seçin veya hızlı müşteri ekleyin.";

                if (IsQuickAddCustomer && string.IsNullOrWhiteSpace(QuickCustomerName))
                    return "Yeni müşteri adı zorunludur.";

                if (IsQuickAddCustomer && string.IsNullOrWhiteSpace(QuickCustomerPhone))
                    return "Yeni müşteri telefonu zorunludur.";

                if (!IsDiscoveryOnly && string.IsNullOrWhiteSpace(Description))
                    return "İş açıklaması zorunludur.";

                if (IsNewAsset && (string.IsNullOrWhiteSpace(NewAssetBrand) || string.IsNullOrWhiteSpace(NewAssetModel)))
                    return "Yeni cihaz için marka ve model zorunludur.";

                if (HasErrors)
                    return "Formda doğrulama hataları bulunmaktadır.";

                return string.Empty;
            }
        }

        public void RefreshSaveState()
        {
            OnPropertyChanged(nameof(SaveDisabledReason));
            SaveServiceJobCommand.NotifyCanExecuteChanged();
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
        public ObservableCollection<ServiceJobRowDto> ServiceJobs { get; set; }

        /// <summary>
        /// İş kayıtları görünümü (Filtreleme için)
        /// </summary>
        public ICollectionView ServiceJobsView => _serviceJobsView!;

        /// <summary>
        /// Müşteriler listesi (ComboBox için)
        /// </summary>
        public ObservableCollection<ServiceJobCustomerLookupDto> Customers { get; set; }

        /// <summary>
        /// Ürünler listesi (ComboBox için)
        /// </summary>
        public ObservableCollection<ServiceJobProductLookupDto> Products { get; set; }

        /// <summary>
        /// Kategori çoklu seçimi için (CheckBox binding)
        /// </summary>
        public ObservableCollection<CategorySelectItem> CategoryItems { get; set; }

        /// <summary>
        /// Müşterinin cihazları (Seçilen müşteriye göre filtrelenir)
        /// </summary>
        public ObservableCollection<ServiceJobAssetLookupDto> CustomerAssets { get; set; } = new();

        /// <summary>
        /// Müşterinin projeleri (Seçilen müşteriye göre filtrelenir)
        /// </summary>
        public ObservableCollection<ServiceJobProjectLookupDto> CustomerProjects { get; set; } = new();

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
        public ServiceJobAssetLookupDto? SelectedAsset
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
                    _searchCts?.Cancel();
                    _searchCts = new CancellationTokenSource();
                    _ = DebounceSearchAsync(_searchCts.Token);
                }
            }
        }

        private async Task DebounceSearchAsync(CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(500, cancellationToken);
                await LoadServiceJobs();
            }
            catch (OperationCanceledException)
            {
                // Yeni arama metni önceki isteğin yerini aldı.
            }
        }

        [RelayCommand]
        private void SelectStatusFilter(string filterName)
        {
            SelectedStatusFilter = filterName switch
            {
                "Pending" => StatusFilter.Pending,
                "InProgress" => StatusFilter.InProgress,
                "Completed" => StatusFilter.Completed,
                "Cancelled" => StatusFilter.Cancelled,
                "SlaBreached" => StatusFilter.SlaBreached,
                _ => StatusFilter.All
            };
        }

        [RelayCommand]
        private void ClearFilters()
        {
            SearchText = string.Empty;
            SelectedStatusFilter = StatusFilter.All;
            FilterStartDate = null;
            FilterEndDate = null;
        }

        private WorkOrderType _selectedWorkOrderType = WorkOrderType.Repair;
        public WorkOrderType SelectedWorkOrderType
        {
            get => _selectedWorkOrderType;
            set
            {
                if (SetProperty(ref _selectedWorkOrderType, value))
                {
                    IsDiscoveryOnly = (value == WorkOrderType.Discovery);
                    OnPropertyChanged(nameof(IsDiscoveryWorkOrder));
                    OnPropertyChanged(nameof(IsRepairWorkOrder));
                    OnPropertyChanged(nameof(IsInstallationWorkOrder));
                    OnPropertyChanged(nameof(IsMaintenanceWorkOrder));
                    RefreshSaveState();
                }
            }
        }

        public bool IsDiscoveryWorkOrder => SelectedWorkOrderType == WorkOrderType.Discovery;
        public bool IsRepairWorkOrder => SelectedWorkOrderType == WorkOrderType.Repair;
        public bool IsInstallationWorkOrder => SelectedWorkOrderType == WorkOrderType.Installation;
        public bool IsMaintenanceWorkOrder => SelectedWorkOrderType == WorkOrderType.Maintenance;

        [RelayCommand]
        private void SelectWorkOrderType(string typeName)
        {
            if (Enum.TryParse<WorkOrderType>(typeName, out var type))
            {
                SelectedWorkOrderType = type;
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
        public ObservableCollection<ServiceJobStatusFilterOption> StatusFilters { get; } = new()
        {
            new(StatusFilter.All, "Tümü"),
            new(StatusFilter.Pending, "Bekliyor"),
            new(StatusFilter.InProgress, "Devam Ediyor"),
            new(StatusFilter.Completed, "Tamamlandı"),
            new(StatusFilter.Cancelled, "İptal Edildi"),
            new(StatusFilter.SlaBreached, "SLA Aşan")
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
        public ObservableCollection<ServiceJobMaterialDto> CurrentJobItems { get; set; }

        /// <summary>
        /// Seçili iş
        /// </summary>
        public ServiceJobRowDto? SelectedServiceJob
        {
            get => _selectedServiceJob;
            set
            {
                if (SetProperty(ref _selectedServiceJob, value))
                {
                    _ = LoadJobItems();
                    (ChangeJobStatusCommand as CommunityToolkit.Mvvm.Input.IRelayCommand)?.NotifyCanExecuteChanged();
                    (DeleteJobCommand as CommunityToolkit.Mvvm.Input.IRelayCommand)?.NotifyCanExecuteChanged();
                    CompleteJobCommand.NotifyCanExecuteChanged();
                    ConvertToQuoteCommand.NotifyCanExecuteChanged();
                    EditQuoteCommand.NotifyCanExecuteChanged();
                    AcceptQuoteCommand.NotifyCanExecuteChanged();
                    RejectQuoteCommand.NotifyCanExecuteChanged();
                    SetInstallationPlannedCommand.NotifyCanExecuteChanged();
                    SetInstallationCompletedCommand.NotifyCanExecuteChanged();
                    CancelJobCommand.NotifyCanExecuteChanged();
                }
            }
        }

        /// <summary>
        /// Seçili müşteri
        /// </summary>
        public ServiceJobCustomerLookupDto? SelectedCustomer
        {
            get => _selectedCustomer;
            set
            {
                if (SetProperty(ref _selectedCustomer, value))
                {
                    OnPropertyChanged(nameof(SummaryCustomerName));
                    OnPropertyChanged(nameof(SummaryCustomerAddress));

                    // Müşteri değiştiğinde cihaz ve projeleri yükle
                    _ = LoadCustomerAssets();
                    _ = LoadCustomerProjects();
                    SaveServiceJobCommand.NotifyCanExecuteChanged();
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
        [RequiredWhen(nameof(IsDiscoveryOnly), false, ErrorMessage = "İş açıklaması zorunludur.")]
        [StringLength(2000, ErrorMessage = "İş açıklaması en fazla 2000 karakter olabilir.")]
        public string Description
        {
            get => _description;
            set
            {
                if (SetProperty(ref _description, value))
                {
                    SaveServiceJobCommand.NotifyCanExecuteChanged();
                }
            }
        }

        /// <summary>
        /// Eklenecek ürün
        /// </summary>
        public ServiceJobProductLookupDto? SelectedProductToAdd
        {
            get => _selectedProductToAdd;
            set
            {
                if (SetProperty(ref _selectedProductToAdd, value) && value != null)
                {
                    // Varsayılan birim fiyatı ayarla
                    UnitPriceToAdd = value.SalePrice;
                }
                AddItemToJobCommand.NotifyCanExecuteChanged();
            }
        }

        /// <summary>
        /// Eklenecek miktar
        /// </summary>
        [Range(1, 100000, ErrorMessage = "Malzeme miktarı 1 veya daha büyük olmalıdır.")]
        public int QuantityToAdd
        {
            get => _quantityToAdd;
            set
            {
                if (SetProperty(ref _quantityToAdd, value))
                {
                    AddItemToJobCommand.NotifyCanExecuteChanged();
                }
            }
        }

        #endregion

        #region Commands

        /// <summary>
        /// İş kaydet komutu
        /// </summary>

        /// <summary>
        /// İşe ürün ekle komutu
        /// </summary>

        /// <summary>
        /// İşten ürün çıkar komutu
        /// </summary>

        /// <summary>
        /// İşi tamamla komutu (KRİTİK - STOK DÜŞME MANTIĞI)
        /// </summary>

        /// <summary>
        /// Formu temizle komutu
        /// </summary>

        /// <summary>
        /// Yeni iş formunu aç
        /// </summary>

        /// <summary>
        /// Listeyi yenile
        /// </summary>

        /// <summary>
        /// İş detayı görüntüle
        /// </summary>

        /// <summary>
        /// Keşfi onayla ve malzeme adımından başlat
        /// </summary>


        /// <summary>
        /// PDF Yazdır komutu
        /// </summary>

        /// <summary>
        /// Hızlı cihaz ekle komutu
        /// </summary>
        /// <summary>
        /// Hızlı cihaz ekle komutu
        /// </summary>

        /// <summary>
        /// İptal komutu
        /// </summary>

        /// <summary>
        /// Wizard ileri adım
        /// </summary>

        /// <summary>
        /// Wizard geri adım
        /// </summary>

        /// <summary>
        /// İş durumu değiştirme komutu
        /// </summary>

        /// <summary>
        /// İş silme komutu
        /// </summary>

        /// <summary>
        /// İptal talebi event
        /// </summary>
        public event Action? CancelRequested;

        [RelayCommand]
        private void CancelForm() => CancelRequested?.Invoke();

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

        public async Task InitializeForCreateAsync(CancellationToken cancellationToken = default)
        {
            await LoadWorkspace();
        }

        public async Task InitializeForEditAsync(ServiceJobRowDto job, CancellationToken cancellationToken = default)
        {
            await LoadWorkspace();

            ClearForm();
            SelectedServiceJob = job;
            await LoadJobItems();
            _isEditing = true;

            Description = job.Description ?? string.Empty;
            SelectedWorkOrderType = job.WorkOrderType;
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
                await LoadCustomerAssets();
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

            var photos = job.PhotoPathsList;
            if (photos != null)
            {
                foreach (var p in photos) UploadedPhotos.Add(p);
            }

            CurrentWizardStep = 1;
        }

        /// <summary>
        /// Yeni iş formunu aç
        /// </summary>
        [RelayCommand]
        private async Task OpenNewJobForm()
        {
            // Create a new ViewModel with dependencies
            var newVm = new ServiceJobViewModel(
                _navigationService,
                _toastService,
                _loadingService,
                _serviceJobCommandService,
                _serviceJobReadService,
                _pdfService,
                _dialogService);
            
            await newVm.InitializeForCreateAsync();

            var window = new NewServiceJobWindow(newVm);
            window.Owner = System.Windows.Application.Current.MainWindow;
            var result = window.ShowDialog();

            if (result == true)
            {
                await RefreshList();
            }
        }

        [RelayCommand]
        private void NavigateBack()
        {
            _navigationService.NavigateTo<DashboardViewModel>();
        }

        public IRelayCommand BackCommand => NavigateBackCommand;

        /// <summary>
        /// Listeyi yenile
        /// </summary>
        [RelayCommand]
        private async Task RefreshList()
        {
            await LoadServiceJobs();
            await LoadDashboardAsync();
            _serviceJobsView?.Refresh();
            NotifyWorkflowCommands();
        }

        /// <summary>
        /// İş akışı sağ tık komutlarının durumunu tazeler (satır durumları değişince).
        /// </summary>
        private void NotifyWorkflowCommands()
        {
            ConvertToQuoteCommand.NotifyCanExecuteChanged();
            EditQuoteCommand.NotifyCanExecuteChanged();
            AcceptQuoteCommand.NotifyCanExecuteChanged();
            RejectQuoteCommand.NotifyCanExecuteChanged();
            SetInstallationPlannedCommand.NotifyCanExecuteChanged();
            SetInstallationCompletedCommand.NotifyCanExecuteChanged();
            CancelJobCommand.NotifyCanExecuteChanged();
        }

        /// <summary>
        /// İş detayını görüntüle (Sağ Çekmece Panelini Aç)
        /// </summary>
        [RelayCommand]
        private void ViewJobDetail(ServiceJobRowDto? job)
        {
            if (job != null)
            {
                SelectedServiceJob = job;
                _ = LoadSelectedJobHistory();
            }
            IsDetailPanelOpen = true;
        }

        [RelayCommand]
        private async Task EditJob(ServiceJobRowDto? job)
        {
            var targetJob = job ?? SelectedServiceJob;
            if (targetJob == null) return;

            // 'İşi Düzenle' artık yeni kayıt formunu değil, İş Emri Çalışma Alanını açar.
            // Genel bilgiler, çalışma alanından ayrı bir butonla düzenlenebilir.
            var workspaceVm = new WorkOrderWorkspaceViewModel(
                targetJob,
                _serviceJobReadService,
                _serviceJobCommandService,
                _pdfService,
                _dialogService,
                _toastService,
                () => OpenGeneralEditorWindowAsync(targetJob));

            if (!await workspaceVm.InitializeAsync()) return;

            var window = new WorkOrderWorkspaceWindow(workspaceVm)
            {
                Owner = System.Windows.Application.Current.MainWindow
            };
            window.ShowDialog();

            await RefreshList();
        }

        /// <summary>
        /// Genel bilgileri düzenlemek için yeni kayıt formunu düzenleme modunda açar.
        /// </summary>
        private async Task OpenGeneralEditorWindowAsync(ServiceJobRowDto job)
        {
            var editVm = new ServiceJobViewModel(
                _navigationService,
                _toastService,
                _loadingService,
                _serviceJobCommandService,
                _serviceJobReadService,
                _pdfService,
                _dialogService);

            await editVm.InitializeForEditAsync(job);
            var window = new NewServiceJobWindow(editVm)
            {
                Owner = System.Windows.Application.Current.MainWindow
            };
            var result = window.ShowDialog();

            if (result == true) await RefreshList();
        }

        /// <summary>
        /// Keşfi onayla ve malzeme seçimiyle işe dönüştür
        /// </summary>
        [RelayCommand]
        private async Task ApproveDiscovery(ServiceJobRowDto? job)
        {
            var targetJob = job ?? SelectedServiceJob;
            if (targetJob == null) return;

            var editVm = new ServiceJobViewModel(
                _navigationService,
                _toastService,
                _loadingService,
                _serviceJobCommandService,
                _serviceJobReadService,
                _pdfService,
                _dialogService);

            await editVm.InitializeForEditAsync(targetJob);
            editVm.IsDiscoveryOnly = false;
            editVm.CurrentWizardStep = 3;

            var window = new NewServiceJobWindow(editVm)
            {
                Owner = System.Windows.Application.Current.MainWindow
            };
            var result = window.ShowDialog();

            if (result == true) await RefreshList();
        }

        /// <summary>
        /// Keşif kaydını teklife dönüştürür (yalnızca Keşif Talebi aşamasında geçerlidir).
        /// Keşif verileri DiscoveryReport'a, malzemeler QuotationItem'a kopyalanır.
        /// </summary>
        [RelayCommand]
        private async Task ConvertToQuote(object? param)
        {
            int? jobId = param switch
            {
                ServiceJobRowDto row => row.Id,
                ServiceJobListItemDto dto => dto.Id,
                ServiceJob job => job.Id,
                int id => id,
                _ => SelectedServiceJob?.Id
            };

            if (!jobId.HasValue)
            {
                _toastService?.ShowWarning("Lütfen teklife dönüştürmek istediğiniz iş emrini seçin.");
                return;
            }

            var target = GetRowFromParam(param);
            if (target is not null && target.QuotationId is not null)
            {
                _toastService?.ShowWarning("Bu iş emri zaten teklife dönüştürülmüş.");
                return;
            }
            if (target is not null && target.Status is not (JobStatus.DiscoveryRequest or JobStatus.Pending or JobStatus.PendingDiscovery or JobStatus.DiscoveryCompleted))
            {
                _toastService?.ShowWarning("Bu iş emri yalnızca keşif aşamasında teklife dönüştürülebilir.");
                return;
            }

            try
            {
                _loadingService?.Show("Keşif teklife dönüştürülüyor...");

                var conversion = await _serviceJobCommandService.ConvertToQuoteAsync(
                    jobId.Value,
                    App.CurrentUser?.Username ?? "Sistem");
                if (conversion.IsFailure)
                {
                    _toastService?.ShowError(conversion.Error);
                    return;
                }

                _toastService?.ShowSuccess($"İş #{conversion.Value!.JobId} teklif aşamasına alındı ve teklif kaydı oluşturuldu.");
                await RefreshList();

                // Kullanıcıya teklif düzenleme ekranını aç
                if (await _dialogService.ShowConfirmationAsync(
                        "Teklif kaydı oluşturuldu. Fiyat ve şartları düzenlemek için teklif ekranını açmak ister misiniz?",
                        "Teklif Düzenle") &&
                    await OpenQuotationEditorAsync(conversion.Value.JobId))
                {
                    await RefreshList();
                }
            }
            catch (Exception ex)
            {
                _toastService?.ShowError($"Teklife dönüştürülürken hata: {ex.Message}");
            }
            finally
            {
                _loadingService?.Hide();
            }
        }

        /// <summary>
        /// Teklife dönüştürülmüş iş için montajı planlar (yalnızca kabul edilmiş tekliflerde).
        /// Teklif kalemleri montaj malzemelerine kopyalanır.
        /// </summary>
        [RelayCommand]
        private async Task SetInstallationPlanned(object? param)
        {
            int? jobId = param switch
            {
                ServiceJobRowDto row => row.Id,
                ServiceJobListItemDto dto => dto.Id,
                ServiceJob job => job.Id,
                int id => id,
                _ => SelectedServiceJob?.Id
            };

            if (!jobId.HasValue)
            {
                _toastService?.ShowWarning("Lütfen işlem yapılacak iş emrini seçin.");
                return;
            }

            var target = GetRowFromParam(param);
            if (target is not null && target.QuotationStatus != QuotationStatus.Accepted)
            {
                _toastService?.ShowWarning("Montaj planlamak için teklifin önce 'Kabul Edildi' durumunda olması gerekir.");
                return;
            }
            if (target is not null && target.InstallationOrderId is not null)
            {
                _toastService?.ShowWarning("Bu iş için montaj zaten planlanmış.");
                return;
            }

            string? dateInput = await _dialogService.ShowInputAsync(
                "Montaj tarihi (ör. 2026-08-10) ve/veya boş bırakarak planlayın:",
                "Montaj Planlama",
                DateTime.Today.AddDays(1).ToString("yyyy-MM-dd"));
            if (dateInput is null) return; // iptal edildi

            DateTime? installationDate = null;
            if (DateTime.TryParse(dateInput.Trim(), out var parsed))
            {
                installationDate = parsed;
            }

            try
            {
                _loadingService?.Show("Montaj planlanıyor...");

                var result = await _serviceJobCommandService.PlanInstallationAsync(
                    new PlanInstallationRequest(
                        jobId.Value,
                        null,
                        null,
                        installationDate,
                        null,
                        App.CurrentUser?.Username ?? "Sistem"));

                if (result.IsFailure || result.Value is null)
                {
                    _toastService?.ShowError(result.Error);
                    return;
                }

                _toastService?.ShowSuccess($"İş #{jobId.Value} 'Montaj Yapılacak' durumuna alındı.");
                await RefreshList();
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

        /// <summary>
        /// Montajı tamamlar (yalnızca Montaj Yapılacak aşamasında geçerlidir).
        /// Teslim notu, teknisyen ve müşteri imzası montaj emrine saklanır.
        /// </summary>
        [RelayCommand]
        private async Task SetInstallationCompleted(object? param)
        {
            int? jobId = param switch
            {
                ServiceJobRowDto row => row.Id,
                ServiceJobListItemDto dto => dto.Id,
                ServiceJob job => job.Id,
                int id => id,
                _ => SelectedServiceJob?.Id
            };

            if (!jobId.HasValue)
            {
                _toastService?.ShowWarning("Lütfen işlem yapılacak iş emrini seçin.");
                return;
            }

            var target = GetRowFromParam(param);
            if (target is not null && target.InstallationOrderId is null && target.Status != JobStatus.InstallationPlanned)
            {
                _toastService?.ShowWarning("Montaj tamamlamak için işin önce 'Montaj Yapılacak' aşamasında olması gerekir.");
                return;
            }
            if (target is not null && target.IsInstallationCompleted)
            {
                _toastService?.ShowWarning("Bu işin montajı zaten tamamlanmış.");
                return;
            }

            string? deliveryNote = await _dialogService.ShowInputAsync(
                "Teslim notu (boş bırakılabilir):",
                "Montaj Tamamlama",
                "Cihaz test edilerek teslim edildi.");
            if (deliveryNote is null) return; // iptal edildi

            string? laborInput = await _dialogService.ShowInputAsync(
                "Montajda harcanan işçilik saati (ör. 4 veya 6.5):",
                "Montaj Tamamlama",
                "4");
            if (laborInput is null) return; // iptal edildi
            if (!decimal.TryParse(laborInput.Trim(), out var laborHours) || laborHours <= 0m)
            {
                _toastService?.ShowWarning("İşçilik saati geçerli bir pozitif sayı olmalıdır.");
                return;
            }

            try
            {
                _loadingService?.Show("Montaj tamamlanıyor...");

                var result = await _serviceJobCommandService.CompleteInstallationAsync(
                    new CompleteInstallationRequest(
                        jobId.Value,
                        deliveryNote.Trim(),
                        null,
                        null,
                        laborHours,
                        App.CurrentUser?.Username ?? "Sistem"));

                if (result.IsFailure || result.Value is null)
                {
                    _toastService?.ShowError(result.Error);
                    return;
                }

                _toastService?.ShowSuccess($"İş #{jobId.Value} montajı tamamlandı.");
                await RefreshList();
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

        /// <summary>
        /// İş emrini iptal eder (yalnızca sonlanmamış durumlarda geçerlidir)
        /// </summary>
        [RelayCommand]
        private async Task CancelJob(object? param)
        {
            int? jobId = param switch
            {
                ServiceJobRowDto row => row.Id,
                ServiceJobListItemDto dto => dto.Id,
                ServiceJob job => job.Id,
                int id => id,
                _ => SelectedServiceJob?.Id
            };

            if (!jobId.HasValue)
            {
                _toastService?.ShowWarning("Lütfen iptal edilecek iş emrini seçin.");
                return;
            }

            bool confirmed = await _dialogService.ShowConfirmationAsync(
                $"İş #{jobId.Value} iptal edilecek. Emin misiniz?",
                "İptal Onayı");
            if (!confirmed) return;

            try
            {
                _loadingService?.Show("İş emri iptal ediliyor...");

                var result = await _serviceJobCommandService.ChangeStatusAsync(
                    jobId.Value,
                    JobStatus.Cancelled,
                    App.CurrentUser?.Username ?? "Sistem");

                if (result.IsFailure || result.Value is null)
                {
                    _toastService?.ShowError(result.Error);
                    return;
                }

                _toastService?.ShowSuccess($"İş #{jobId.Value} iptal edildi; varsa stok rezervasyonları serbest bırakıldı.");
                await RefreshList();
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

        // ── İş akışı komut durum yardımcıları (butonlar her zaman tıklanabilir;
        //    geçersiz durumlarda açıklayıcı bir uyarı gösterilir) ──

        private ServiceJobRowDto? GetRowFromParam(object? param) => param switch
        {
            ServiceJobRowDto row => row,
            _ => SelectedServiceJob
        };

        /// <summary>
        /// Teklif düzenleme ekranını açar (malzeme, miktar, birim fiyat, iskonto, KDV,
        /// işçilik, nakliye, açıklamalar, garanti, teslim süresi, ödeme şartları).
        /// </summary>
        [RelayCommand]
        private async Task EditQuote(object? param)
        {
            int? jobId = param switch
            {
                ServiceJobRowDto row => row.Id,
                ServiceJobListItemDto dto => dto.Id,
                ServiceJob job => job.Id,
                int id => id,
                _ => SelectedServiceJob?.Id
            };
            if (!jobId.HasValue)
            {
                _toastService?.ShowWarning("Lütfen düzenlenecek iş emrini seçin.");
                return;
            }

            await OpenQuotationEditorAsync(jobId.Value);
        }

        /// <summary>
        /// Teklifi müşteri kabul etti olarak işaretler (Montaj Yapılacak buna bağlıdır).
        /// </summary>
        [RelayCommand]
        private async Task AcceptQuote(object? param)
        {
            int? jobId = param switch
            {
                ServiceJobRowDto row => row.Id,
                ServiceJobListItemDto dto => dto.Id,
                ServiceJob job => job.Id,
                int id => id,
                _ => SelectedServiceJob?.Id
            };
            if (!jobId.HasValue)
            {
                _toastService?.ShowWarning("Lütfen işlem yapılacak iş emrini seçin.");
                return;
            }

            var workflow = await _serviceJobReadService.GetWorkOrderWorkflowAsync(jobId.Value);
            if (workflow.IsFailure || workflow.Value?.Quotation is null)
            {
                _toastService?.ShowError(workflow.Error ?? "Teklif bulunamadı.");
                return;
            }

            var quote = workflow.Value.Quotation;
            if (quote.Status == QuotationStatus.Accepted)
            {
                _toastService?.ShowInfo("Teklif zaten kabul edilmiş durumda.");
                return;
            }

            bool confirmed = await _dialogService.ShowConfirmationAsync(
                $"Teklif {quote.QuotationNumber} ({quote.TotalAmount:N2} ₺) müşteri tarafından kabul edildi olarak işaretlenecek. Devam edilsin mi?",
                "Teklif Kabulü");
            if (!confirmed) return;

            try
            {
                _loadingService?.Show("Teklif kabul ediliyor...");
                var result = await _serviceJobCommandService.AcceptQuotationAsync(
                    quote.Id,
                    App.CurrentUser?.Username ?? "Sistem");
                if (result.IsFailure)
                {
                    _toastService?.ShowError(result.Error);
                    return;
                }

                _toastService?.ShowSuccess("Teklif kabul edildi. Artık 'Montaj Yapılacak' aşamasına geçilebilir.");
                await RefreshList();
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

        /// <summary>
        /// Teklifi müşteri reddetti olarak işaretler (gerekçe istenir).
        /// </summary>
        [RelayCommand]
        private async Task RejectQuote(object? param)
        {
            int? jobId = param switch
            {
                ServiceJobRowDto row => row.Id,
                ServiceJobListItemDto dto => dto.Id,
                ServiceJob job => job.Id,
                int id => id,
                _ => SelectedServiceJob?.Id
            };
            if (!jobId.HasValue)
            {
                _toastService?.ShowWarning("Lütfen işlem yapılacak iş emrini seçin.");
                return;
            }

            var workflow = await _serviceJobReadService.GetWorkOrderWorkflowAsync(jobId.Value);
            if (workflow.IsFailure || workflow.Value?.Quotation is null)
            {
                _toastService?.ShowError(workflow.Error ?? "Teklif bulunamadı.");
                return;
            }

            var quote = workflow.Value.Quotation;
            string? reason = await _dialogService.ShowInputAsync(
                "Red gerekçesi:",
                "Teklif Reddi",
                "Fiyat uygun bulunmadı.");
            if (reason is null) return;

            try
            {
                _loadingService?.Show("Teklif reddediliyor...");
                var result = await _serviceJobCommandService.RejectQuotationAsync(
                    quote.Id,
                    reason.Trim(),
                    App.CurrentUser?.Username ?? "Sistem");
                if (result.IsFailure)
                {
                    _toastService?.ShowError(result.Error);
                    return;
                }

                _toastService?.ShowSuccess("Teklif reddedildi.");
                await RefreshList();
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

        /// <summary>
        /// Teklif düzenleme penceresini açar ve sonucuna göre listeyi yeniler.
        /// </summary>
        private async Task<bool> OpenQuotationEditorAsync(int jobId)
        {
            try
            {
                var vm = new WorkOrderQuotationViewModel(
                    jobId,
                    _serviceJobReadService,
                    _serviceJobCommandService,
                    _pdfService,
                    _dialogService,
                    _toastService);

                if (!await vm.InitializeAsync())
                {
                    _toastService?.ShowError("Teklif verileri yüklenemedi.");
                    return false;
                }

                var window = new WorkOrderQuotationWindow(vm)
                {
                    Owner = System.Windows.Application.Current.MainWindow
                };
                var result = window.ShowDialog();
                if (result == true) await RefreshList();
                return true;
            }
            catch (Exception ex)
            {
                _toastService?.ShowError($"Teklif ekranı açılamadı: {ex.Message}");
                return false;
            }
        }

        [RelayCommand]
        private async Task BrowsePhotos()
        {
            var files = await _dialogService.ShowOpenFilesDialogAsync(
                "Servis Fotoğraflarını Seç",
                "Resim Dosyaları|*.jpg;*.jpeg;*.png;*.webp");
            foreach (var file in files)
            {
                if (!UploadedPhotos.Contains(file)) UploadedPhotos.Add(file);
            }
        }

        public void SetInitialCustomer(Customer customer)
        {
            var lookup = new ServiceJobCustomerLookupDto(customer.Id, customer.FullName, customer.FullAddress);
            if (Customers.All(item => item.Id != lookup.Id)) Customers.Add(lookup);
            SelectedCustomer = Customers.First(item => item.Id == lookup.Id);
        }

        [RelayCommand]
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
        private async Task Refresh()
        {
            if (_loadingService != null) 
                _loadingService.Show("İşler yükleniyor...");

            try
            {
                await LoadWorkspace();
                await LoadServiceJobs();
                await LoadDashboardAsync();
            }
            finally
            {
                _loadingService?.Hide();
            }
        }

        /// <summary>
        /// Müşterileri yükle
        /// </summary>
        private async Task LoadWorkspace()
        {
            var result = await _serviceJobReadService.GetWorkspaceAsync();
            if (result.IsFailure || result.Value is null)
            {
                _toastService.ShowError(result.Error);
                return;
            }

            Customers.Clear();
            Products.Clear();
            Technicians.Clear();
            foreach (var customer in result.Value.Customers) Customers.Add(customer);
            foreach (var product in result.Value.Products) Products.Add(product);
            foreach (var technician in result.Value.Technicians) Technicians.Add(technician);
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
                var result = await _serviceJobReadService.GetCustomerAssetsAsync(SelectedCustomer.Id);
                if (result.IsFailure)
                {
                    _toastService.ShowError(result.Error);
                    return;
                }
                foreach (var asset in result.Value!) CustomerAssets.Add(asset);
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
                var result = await _serviceJobReadService.GetCustomerProjectsAsync(SelectedCustomer.Id);
                if (result.IsFailure)
                {
                    _toastService.ShowError(result.Error);
                    return;
                }
                foreach (var project in result.Value!) CustomerProjects.Add(project);
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
        [RelayCommand]
        private async Task AddAsset()
        {
            if (SelectedCustomer == null)
            {
                await _dialogService.ShowWarningAsync("Lütfen önce müşteri seçin.");
                return;
            }

            var window = new Views.QuickAssetAddWindow(SelectedCustomer.Id);
            if (window.ShowDialog() == true && window.CreatedAsset != null)
            {
                // Listeye ekle ve seç
                var created = window.CreatedAsset;
                var asset = new ServiceJobAssetLookupDto(
                    created.Id,
                    created.Category,
                    created.Brand,
                    created.Model,
                    created.SerialNumber,
                    created.Location);
                CustomerAssets.Add(asset);
                SelectedAsset = asset;
                _toastService.ShowSuccess($"Cihaz eklendi: {window.CreatedAsset.FullName}");
            }
        }

        /// <summary>
        /// İş kayıtlarını yükle
        /// </summary>
        private async Task LoadServiceJobs()
        {
            bool isSlaBreached = SelectedStatusFilter == StatusFilter.SlaBreached;
            JobStatus? status = SelectedStatusFilter switch
            {
                StatusFilter.Pending => JobStatus.Pending,
                StatusFilter.InProgress => JobStatus.InProgress,
                StatusFilter.Completed => JobStatus.Completed,
                StatusFilter.Cancelled => JobStatus.Cancelled,
                _ => null
            };
            var result = await _serviceJobReadService.SearchAsync(new ServiceJobSearchRequest(
                SearchText, status, FilterStartDate, FilterEndDate, 50, isSlaBreached));
            if (result.IsFailure)
            {
                _toastService.ShowError(result.Error);
                return;
            }
            ServiceJobs.Clear();
            foreach (var job in result.Value!) ServiceJobs.Add(job);
        }

        /// <summary>
        /// Seçili işe ait ürünleri yükle
        /// </summary>
        private async Task LoadJobItems()
        {
            CurrentJobItems.Clear();

            if (SelectedServiceJob != null)
            {
                var result = await _serviceJobReadService.GetMaterialsAsync(SelectedServiceJob.Id);
                if (result.IsFailure)
                {
                    _toastService.ShowError(result.Error);
                    return;
                }
                foreach (var item in result.Value!) CurrentJobItems.Add(item);
            }
        }

        #endregion

        #region Service Job Operations

        /// <summary>
        /// İş kaydetme kontrolü
        /// </summary>
        private bool CanSaveServiceJob()
        {
            bool hasCustomer = SelectedCustomer != null ||
                               (IsQuickAddCustomer &&
                                !string.IsNullOrWhiteSpace(QuickCustomerName) &&
                                !string.IsNullOrWhiteSpace(QuickCustomerPhone));

            return !IsSaving && hasCustomer &&
                   !HasErrors &&
                   (IsDiscoveryOnly || !string.IsNullOrWhiteSpace(Description));
        }

        /// <summary>
        /// Yeni iş kaydet (Hibrit Cihaz Desteği ile)
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanSaveServiceJob))]
        private async Task SaveServiceJob()
        {
            ValidateAllProperties();
            if (HasErrors)
            {
                _toastService?.ShowWarning("Lütfen işaretlenen zorunlu alanları kontrol edin.");
                return;
            }

            IsSaving = true;
            SaveServiceJobCommand.NotifyCanExecuteChanged();
            try
            {
                if (!IsQuickAddCustomer && SelectedCustomer == null)
                {
                    _toastService?.ShowWarning("Lütfen müşteri seçin veya oluşturun.");
                    return;
                }

                if (IsQuickAddCustomer &&
                    (string.IsNullOrWhiteSpace(QuickCustomerName) || string.IsNullOrWhiteSpace(QuickCustomerPhone)))
                {
                    _toastService?.ShowWarning("Yeni müşteri için ad soyad ve telefon zorunludur.");
                    return;
                }

                if (IsNewAsset && (string.IsNullOrWhiteSpace(NewAssetBrand) || string.IsNullOrWhiteSpace(NewAssetModel)))
                {
                    _toastService?.ShowWarning("Yeni cihaz için marka ve model zorunludur.");
                    return;
                }

                // === ADIM 2: Kategorileri JSON olarak kaydet ===
                var selectedCategories = CategoryItems?
                    .Where(c => c.IsSelected)
                    .Select(c => (int)c.Category)
                    .ToList() ?? new List<int>();
                string categoriesJson = JsonSerializer.Serialize(selectedCategories);

                // Keşif talebi için Description boşsa otomatik doldur
                if (IsDiscoveryOnly && string.IsNullOrWhiteSpace(Description))
                {
                    Description = "Keşif Talebi";
                }

                var jobToSave = new ServiceJob
                {
                    Id = _isEditing ? SelectedServiceJob?.Id ?? 0 : 0,
                    CustomerId = SelectedCustomer?.Id ?? 0,
                    CustomerAssetId = IsNewAsset ? null : SelectedAsset?.Id,
                    WorkOrderType = IsDiscoveryOnly ? WorkOrderType.Discovery : SelectedWorkOrderType,
                    JobCategory = selectedCategories.Any() ? (JobCategory)selectedCategories.First() : JobCategory.CCTV,
                    CategoriesJson = categoriesJson,
                    Description = Description,
                    Status = _isEditing
                        ? SelectedServiceJob?.Status ?? JobStatus.Pending
                        : IsDiscoveryOnly ? JobStatus.DiscoveryRequest : JobStatus.Pending,
                    CreatedDate = _isEditing ? SelectedServiceJob?.CreatedDate ?? DateTime.UtcNow : DateTime.UtcNow,
                    CompletedDate = _isEditing ? SelectedServiceJob?.CompletedDate : null,
                    ScheduledDate = ScheduledDate,
                    AssignedTechnicianId = SelectedTechnicianId,
                    AssignedTechnician = Technicians.FirstOrDefault(item => item.Id == SelectedTechnicianId)?.FullName,
                    Priority = SelectedPriority,
                    LaborCost = LaborCost,
                    DiscountAmount = DiscountAmount,
                    EstimatedDuration = EstimatedDuration,
                    SlaDeadline = SlaDeadline,
                    TechnicianNotes = TechnicianNotes,
                    PhotoPathsJson = JsonSerializer.Serialize(UploadedPhotos.ToList()),
                    TotalAmount = GrandTotal,
                    TaxAmount = KdvAmount
                };
                var materialItems = CurrentJobItems.Select(item => new ServiceJobItem
                {
                    Id = item.Id,
                    ProductId = item.ProductId,
                    QuantityUsed = item.QuantityUsed,
                    UnitPrice = item.UnitPrice,
                    UnitCost = item.UnitCost
                }).ToList();
                var quickCustomer = IsQuickAddCustomer
                    ? new ServiceJobQuickCustomerInput(QuickCustomerName, QuickCustomerPhone)
                    : null;
                var newAsset = IsNewAsset
                    ? new ServiceJobNewAssetInput(
                        SelectedJobCategory,
                        NewAssetBrand,
                        NewAssetModel,
                        NewAssetSerialNumber,
                        NewAssetLocation)
                    : null;

                bool wasEditing = _isEditing;
                if (_loadingService != null) _loadingService.Show("İş emri kaydediliyor...");
                var saveResult = await _serviceJobCommandService.SaveAsync(new ServiceJobSaveRequest(
                    jobToSave,
                    materialItems,
                    wasEditing,
                    App.CurrentUser?.Username ?? "Sistem",
                    quickCustomer,
                    newAsset));

                if (saveResult.IsFailure || saveResult.Value is null)
                {
                    _toastService?.ShowError(saveResult.Error);
                    return;
                }

                if (quickCustomer is not null || newAsset is not null) await LoadWorkspace();
                await LoadServiceJobs();
                ClearForm();

                string reservationMessage = saveResult.Value.ReservationCount > 0
                    ? $" {saveResult.Value.ReservationCount} stok rezervasyonu oluşturuldu."
                    : string.Empty;
                _toastService?.ShowSuccess(
                    (wasEditing ? "İş kaydı başarıyla güncellendi." : "İş kaydı başarıyla oluşturuldu.") +
                    reservationMessage);
                SaveCompleted?.Invoke();
            }
            catch (Exception ex)
            {
                _toastService.ShowError($"Hata: {ex.Message}");
            }
            finally
            {
                IsSaving = false;
                SaveServiceJobCommand.NotifyCanExecuteChanged();
                _loadingService?.Hide();
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
        [RelayCommand(CanExecute = nameof(CanAddItem))]
        private void AddItemToJob()
        {
            if (SelectedProductToAdd == null) return;

            var newItem = new ServiceJobMaterialDto(
                0,
                SelectedProductToAdd.Id,
                SelectedProductToAdd.ProductName,
                QuantityToAdd,
                UnitPriceToAdd,
                SelectedProductToAdd.PurchasePrice);

            CurrentJobItems.Add(newItem);
            SelectedProductToAdd = null;
            QuantityToAdd = 1;
            UnitPriceToAdd = 0;
        }

        /// <summary>
        /// İşten ürün çıkar
        /// </summary>
        [RelayCommand]
        private void RemoveItemFromJob(ServiceJobMaterialDto? item)
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
                   SelectedServiceJob.Status == JobStatus.InProgress;
        }

        /// <summary>
        /// İşi tamamla - KRİTİK İŞ MANTIĞI: STOK DÜŞME
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanCompleteJob))]
        private async Task CompleteJob()
        {
            if (SelectedServiceJob == null) return;

            try
            {
                var result = await _serviceJobCommandService.ChangeStatusAsync(
                    SelectedServiceJob.Id,
                    JobStatus.Completed,
                    App.CurrentUser?.Username ?? "Sistem");

                if (result.IsFailure || result.Value is null)
                {
                    _toastService.ShowError(result.Error);
                    return;
                }

                SelectedServiceJob.Status = result.Value.CurrentStatus;
                SelectedServiceJob.CompletedDate = result.Value.CompletedDate;

                await LoadServiceJobs();
                await LoadWorkspace();
                await LoadDashboardAsync();

                _toastService.ShowSuccess("İş tamamlandı; stok, müşteri profili ve tarihçe atomik olarak güncellendi.");
            }
            catch (Exception ex)
            {
                _toastService.ShowError($"Hata: {ex.Message}");
            }
        }

        /// <summary>
        /// Formu temizle
        /// </summary>
        [RelayCommand]
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
        /// İş emri belgesini PDF olarak yazdır. Üretilen belge iş emrinin aşamasına göre belirlenir:
        /// Keşif → Keşif Raporu, Teklif → Fiyat Teklifi, Montaj → Montaj İş Emri,
        /// Montaj Tamamlandı → Montaj Tamamlama Formu. Varsayılan olarak Keşif PDF'i üretilmez.
        /// </summary>
        [RelayCommand]
        private async Task PrintServiceForm(object? param)
        {
            int? jobId = param switch
            {
                ServiceJobListItemDto dto => dto.Id,
                ServiceJobRowDto row => row.Id,
                ServiceJob job => job.Id,
                _ => null
            };

            if (!jobId.HasValue) return;

            try
            {
                var document = await _serviceJobReadService.GetDocumentAsync(jobId.Value);
                if (document.IsFailure || document.Value is null)
                {
                    _toastService.ShowError(document.Error);
                    return;
                }

                var workflow = await _serviceJobReadService.GetWorkOrderWorkflowAsync(jobId.Value);
                if (workflow.IsFailure || workflow.Value is null)
                {
                    _toastService.ShowError(workflow.Error);
                    return;
                }

                var jobInfo = document.Value;
                var data = workflow.Value;

                string suggestedName;
                string documentLabel;

                switch (data.JobStatus)
                {
                    case JobStatus.ConvertedToQuote when data.Quotation is not null:
                        suggestedName = $"FiyatTeklifi_{jobInfo.Id:D6}.pdf";
                        documentLabel = "Fiyat Teklifi";
                        if (!await ChooseAndGenerateAsync("Fiyat Teklifini Kaydet", suggestedName, filePath =>
                                _pdfService.GenerateWorkOrderQuotationPdf(data.Quotation!, jobInfo, filePath)))
                            return;
                        break;

                    case JobStatus.InstallationPlanned when data.Installation is not null:
                        suggestedName = $"MontajIsEmri_{jobInfo.Id:D6}.pdf";
                        documentLabel = "Montaj İş Emri";
                        if (!await ChooseAndGenerateAsync("Montaj İş Emrini Kaydet", suggestedName, filePath =>
                                _pdfService.GenerateInstallationOrderPdf(data.Installation!, jobInfo, filePath)))
                            return;
                        break;

                    case JobStatus.InstallationCompleted when data.Installation is not null:
                        suggestedName = $"MontajTamamlama_{jobInfo.Id:D6}.pdf";
                        documentLabel = "Montaj Tamamlama Formu";
                        if (!await ChooseAndGenerateAsync("Montaj Tamamlama Formunu Kaydet", suggestedName, filePath =>
                                _pdfService.GenerateInstallationCompletionFormPdf(data.Installation!, jobInfo, filePath)))
                            return;
                        break;

                    case JobStatus.DiscoveryRequest:
                    case JobStatus.PendingDiscovery:
                    case JobStatus.DiscoveryCompleted:
                        suggestedName = $"KesifRaporu_{jobInfo.Id:D6}.pdf";
                        documentLabel = "Keşif Raporu";
                        if (data.Discovery is not null)
                        {
                            if (!await ChooseAndGenerateAsync("Keşif Raporunu Kaydet", suggestedName, filePath =>
                                    _pdfService.GenerateDiscoveryReportPdf(data.Discovery, jobInfo, filePath)))
                                return;
                        }
                        else
                        {
                            // Henüz dönüştürülmemiş keşif kayıtlarında iş kaydı üzerinden rapor üretilir
                            if (!await ChooseAndGenerateAsync("Keşif Raporunu Kaydet", suggestedName, filePath =>
                                    _pdfService.GenerateServiceJobPdf(jobInfo, filePath)))
                                return;
                        }
                        break;

                    default:
                        // Eski/legacy durumlar için servis formu şablonu kullanılır (keşif PDF'i değil)
                        suggestedName = $"ServisFormu_{jobInfo.Id:D6}.pdf";
                        documentLabel = "Servis Formu";
                        if (!await ChooseAndGenerateAsync("Servis Formunu Kaydet", suggestedName, filePath =>
                                GenerateLegacyServiceFormPdfAsync(jobInfo, filePath)))
                            return;
                        break;
                }

                _toastService.ShowSuccess($"{documentLabel} başarıyla oluşturuldu.");
            }
            catch (Exception ex)
            {
                _toastService.ShowError($"PDF oluşturulurken hata: {ex.Message}");
            }
        }

        private async Task<bool> ChooseAndGenerateAsync(string title, string suggestedName, Action<string> generate)
        {
            var filePath = await _dialogService.ShowSaveFileDialogAsync(
                title,
                "PDF Dosyası (*.pdf)|*.pdf",
                suggestedName);
            if (string.IsNullOrWhiteSpace(filePath)) return false;

            generate(filePath);

            var processInfo = new ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true
            };
            Process.Start(processInfo);
            return true;
        }

        private Task GenerateLegacyServiceFormPdfAsync(ServiceJobDocumentDto jobInfo, string filePath)
        {
            var job = new ServiceJob
            {
                Id = jobInfo.Id,
                WorkOrderType = jobInfo.WorkOrderType,
                Description = jobInfo.Description,
                DiscoveryTechnicalNotes = jobInfo.DiscoveryTechnicalNotes,
                TechnicianNotes = jobInfo.TechnicianNotes,
                AssignedTechnician = jobInfo.AssignedTechnician,
                Priority = jobInfo.Priority,
                ScheduledDate = jobInfo.ScheduledDate,
                CustomerId = jobInfo.CustomerId,
                Customer = new Customer
                {
                    Id = jobInfo.CustomerId,
                    FullName = jobInfo.CustomerName,
                    CompanyName = jobInfo.CustomerCompanyName,
                    PhoneNumber = jobInfo.CustomerPhone,
                    City = jobInfo.CustomerAddress
                }
            };
            _pdfService.GenerateServiceForm(job, filePath);
            return Task.CompletedTask;
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
                var result = await _serviceJobReadService.GetDashboardAsync();
                if (result.IsFailure || result.Value is null) return;
                TotalJobCount = result.Value.TotalJobCount;
                PendingCount = result.Value.PendingCount;
                InProgressCount = result.Value.InProgressCount;
                CompletedCount = result.Value.CompletedCount;
                SlaBreachedCount = result.Value.SlaBreachedCount;
                TodayCreatedCount = result.Value.TodayCreatedCount;
                AvgCompletionHours = result.Value.AvgCompletionHours;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Dashboard stats yüklenemedi: {ex.Message}");
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
                var result = await _serviceJobReadService.GetHistoryAsync(SelectedServiceJob.Id);
                if (result.IsFailure) return;
                foreach (var item in result.Value!) SelectedJobHistory.Add(item);
            }
            catch { /* Tarihçe opsiyonel */ }
        }

        /// <summary>
        /// Wizard ileri adım
        /// </summary>
        [RelayCommand]
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
        [RelayCommand]
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

        private bool CanChangeJobStatus(object? param)
        {
            if (param is ChangeServiceJobStatusCommandParameter typed)
                return typed.Job != null || SelectedServiceJob != null;
            return SelectedServiceJob != null;
        }

        /// <summary>
        /// İş durumunu değiştir (Dashboard / Context Menu)
        /// Fail-Fast & Null-Safe prensiplerine uygun olarak refactor edilmiştir.
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanChangeJobStatus))]
        private async Task ChangeJobStatus(object? param)
        {
            ServiceJobRowDto? targetJob = SelectedServiceJob;
            JobStatus? newStatus = null;

            if (param is ChangeServiceJobStatusCommandParameter typedParam)
            {
                if (typedParam.Job != null) targetJob = typedParam.Job;
                newStatus = typedParam.Status;
            }
            else if (param is JobStatus js)
            {
                newStatus = js;
            }
            else if (param != null && Enum.TryParse<JobStatus>(param.ToString(), out var parsed))
            {
                newStatus = parsed;
            }

            if (targetJob is null)
            {
                _toastService?.ShowWarning("Lütfen durumunu değiştirmek istediğiniz iş emrini seçin.");
                return;
            }

            if (!newStatus.HasValue)
            {
                _toastService?.ShowError("Geçersiz iş durumu.");
                return;
            }

            int targetJobId = targetJob.Id;

            try
            {
                var result = await _serviceJobCommandService.ChangeStatusAsync(
                    targetJobId,
                    newStatus.Value,
                    App.CurrentUser?.Username ?? "Sistem");

                if (result.IsFailure || result.Value is null)
                {
                    _toastService?.ShowError(result.Error);
                    return;
                }

                targetJob.Status = result.Value.CurrentStatus;
                targetJob.CompletedDate = result.Value.CompletedDate;

                await LoadServiceJobs();
                await LoadDashboardAsync();

                _toastService?.ShowSuccess($"İş #{targetJobId} durumu güncellendi: {newStatus}");
            }
            catch (Exception ex)
            {
                _toastService?.ShowError($"Durum güncelleme hatası: {ex.Message}");
            }
        }

        /// <summary>
        /// İş sil (Dashboard context menu & DataGrid action)
        /// </summary>
        [RelayCommand]
        private async Task DeleteJob(ServiceJobRowDto? job = null)
        {
            var target = job ?? SelectedServiceJob;
            if (target == null) return;

            bool confirmed = await _dialogService.ShowConfirmationAsync(
                $"İş #{target.Id} ({target.Description}) silinecek. Emin misiniz?",
                "Silme Onayı");
            if (!confirmed) return;

            try
            {
                var result = await _serviceJobCommandService.DeleteAsync(
                    target.Id,
                    App.CurrentUser?.Username ?? "Sistem");
                if (result.IsFailure)
                {
                    _toastService?.ShowError(result.Error);
                    return;
                }

                await LoadServiceJobs();
                await LoadDashboardAsync();
                _toastService?.ShowSuccess("İş kaydı silindi; stok rezervasyonları güvenle serbest bırakıldı.");
            }
            catch (Exception ex)
            {
                _toastService?.ShowError($"Hata: {ex.Message}");
            }
        }

        [RelayCommand]
        private void Cancel(Window? window = null)
        {
            window?.Close();
        }

        #endregion
    }
}


