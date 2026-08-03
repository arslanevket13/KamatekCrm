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
            ErrorsChanged += (_, _) => SaveServiceJobCommand.NotifyCanExecuteChanged();

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
                    SaveServiceJobCommand.NotifyCanExecuteChanged();
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
                    SaveServiceJobCommand.NotifyCanExecuteChanged();
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
                    SaveServiceJobCommand.NotifyCanExecuteChanged();
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
                    SaveServiceJobCommand.NotifyCanExecuteChanged();
                }
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
                    SaveServiceJobCommand.NotifyCanExecuteChanged();
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
            new(StatusFilter.Cancelled, "İptal Edildi")
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
            _serviceJobsView?.Refresh();
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
            if (job == null) return;

            ClearForm();
            SelectedServiceJob = job;
            await LoadJobItems();
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
            
            // Edit işlemi için yeni pencereyi kendi contextimiz ile açıyoruz
            var window = new NewServiceJobWindow(this);
            window.Owner = System.Windows.Application.Current.MainWindow;
            var result = window.ShowDialog();

            if (result == true) await RefreshList();
        }

        /// <summary>
        /// Keşfi onayla ve malzeme seçimiyle işe dönüştür
        /// </summary>
        [RelayCommand]
        private async Task ApproveDiscovery(ServiceJobRowDto? job)
        {
            if (job == null) return;

            ClearForm();
            
            _isEditing = true;
            Description = job.Description ?? string.Empty;
            IsDiscoveryOnly = false;
            
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
                var jobCats = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.List<int>>(job.CategoriesJson);
                if (jobCats != null)
                {
                    foreach (var cat in CategoryItems)
                        cat.IsSelected = jobCats.Contains((int)cat.Category);
                }
            }

            _selectedServiceJob = job;
            await LoadJobItems();

            var photos = job.PhotoPathsList;
            if (photos != null)
            {
                foreach (var p in photos) UploadedPhotos.Add(p);
            }

            CurrentWizardStep = 3;
            
            var window = new NewServiceJobWindow(this)
            {
                Owner = System.Windows.Application.Current.MainWindow
            };
            var result = window.ShowDialog();

            if (result == true) _ = LoadServiceJobs();
        }

        /// <summary>
        /// Keşif kaydını teklife dönüştürür ve Teklif Ekranına (QuotationViewModel) yönlendirir.
        /// </summary>
        [RelayCommand]
        private async Task ConvertToQuote(object? param)
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
                _loadingService?.Show("Keşif teklife dönüştürülüyor...");

                var conversion = await _serviceJobCommandService.ConvertToQuoteAsync(
                    jobId.Value,
                    App.CurrentUser?.Username ?? "Sistem");
                if (conversion.IsFailure)
                {
                    _toastService?.ShowError(conversion.Error);
                    return;
                }

                _toastService?.ShowSuccess($"İş #{conversion.Value!.JobId} teklif aşamasına alındı. Teklif ekranına yönlendiriliyorsunuz.");

                // Teklif ekranına yönlendir ve müşteriyi otomatik seç
                var quoteVm = _navigationService?.NavigateTo<QuotationViewModel>();
                if (quoteVm != null && conversion.Value.CustomerId > 0)
                {
                    await quoteVm.InitializeFromServiceJobAsync(
                        conversion.Value.JobId,
                        conversion.Value.CustomerId);
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
            JobStatus? status = SelectedStatusFilter switch
            {
                StatusFilter.Pending => JobStatus.Pending,
                StatusFilter.InProgress => JobStatus.InProgress,
                StatusFilter.Completed => JobStatus.Completed,
                StatusFilter.Cancelled => JobStatus.Cancelled,
                _ => null
            };
            var result = await _serviceJobReadService.SearchAsync(new ServiceJobSearchRequest(
                SearchText, status, FilterStartDate, FilterEndDate, 50));
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
                    Status = _isEditing ? SelectedServiceJob?.Status ?? JobStatus.Pending : JobStatus.Pending,
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
        /// Servis formunu PDF olarak yazdır
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

                var filePath = await _dialogService.ShowSaveFileDialogAsync(
                    "Servis Formunu Kaydet",
                    "PDF Dosyası (*.pdf)|*.pdf",
                    $"ServisFormu_{document.Value.Id:D6}.pdf");
                if (!string.IsNullOrWhiteSpace(filePath))
                {
                    _pdfService.GenerateServiceJobPdf(document.Value, filePath);

                    var processInfo = new ProcessStartInfo
                    {
                        FileName = filePath,
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

        private bool CanChangeJobStatus(object? param) => SelectedServiceJob != null;

        /// <summary>
        /// İş durumunu değiştir (Dashboard / Context Menu)
        /// Fail-Fast & Null-Safe prensiplerine uygun olarak refactor edilmiştir.
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanChangeJobStatus))]
        private async Task ChangeJobStatus(object? param)
        {
            // ── Guard Clause 1: Parametre ve Seçili İş Kontrolü (Fail-Fast) ──
            var targetJob = SelectedServiceJob;
            if (targetJob is null)
            {
                _toastService?.ShowWarning("Lütfen durumunu değiştirmek istediğiniz iş emrini seçin.");
                return;
            }

            if (param is null) return;

            // ── Guard Clause 2: Yeni Durum Parse Kontrolü ──
            JobStatus newStatus;
            if (param is JobStatus js)
            {
                newStatus = js;
            }
            else if (Enum.TryParse<JobStatus>(param.ToString(), out var parsed))
            {
                newStatus = parsed;
            }
            else
            {
                _toastService?.ShowError("Geçersiz iş durumu.");
                return;
            }

            // ── Local Capture (Async işlem sırasında NullReference olmasını engeller) ──
            int targetJobId = targetJob.Id;

            try
            {
                var result = await _serviceJobCommandService.ChangeStatusAsync(
                    targetJobId,
                    newStatus,
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


