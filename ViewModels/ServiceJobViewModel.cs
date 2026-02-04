using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
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
        private readonly AppDbContext _context;
        private ServiceJob? _selectedServiceJob;
        private Customer? _selectedCustomer;
        private JobType _selectedJobType;
        private JobCategory _selectedJobCategory; // Geriye uyumluluk için
        private string _description = string.Empty;
        private Product? _selectedProductToAdd;
        private int _quantityToAdd = 1;

        // Filtreleme için
        private string _searchText = string.Empty;
        private StatusFilter _selectedStatusFilter = StatusFilter.Tümü;
        private DateTime? _filterStartDate;
        private DateTime? _filterEndDate;
        private ICollectionView? _serviceJobsView;

        // ===== SINGLE-PAGE FORM STATE =====
        private StructureType _selectedStructureType = StructureType.SingleUnit;
        private int _blockCount = 1;
        private int _flatCount = 1;
        private bool _applyToAllUnits = false;
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
                    OnPropertyChanged(nameof(StructureTypeLabel));
                }
            }
        }

        /// <summary>
        /// Blok sayısı (Site için)
        /// </summary>
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

        /// <summary>
        /// Daire sayısı (Apartman/Site için)
        /// </summary>
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

        /// <summary>
        /// Tüm birimlere uygula checkbox
        /// </summary>
        public bool ApplyToAllUnits
        {
            get => _applyToAllUnits;
            set => SetProperty(ref _applyToAllUnits, value);
        }

        /// <summary>
        /// Blok sayısı inputu görünsün mü?
        /// </summary>
        public bool ShowBlockCount => SelectedStructureType == StructureType.Site;

        /// <summary>
        /// Daire sayısı inputu görünsün mü?
        /// </summary>
        public bool ShowFlatCount => SelectedStructureType == StructureType.Apartment || SelectedStructureType == StructureType.Site;

        /// <summary>
        /// Toplam birim sayısı (Malzeme çarpanı için)
        /// </summary>
        public int TotalUnitCount => SelectedStructureType switch
        {
            StructureType.SingleUnit => 1,
            StructureType.Apartment => FlatCount,
            StructureType.Site => BlockCount * FlatCount,
            StructureType.Commercial => 1,
            _ => 1
        };

        /// <summary>
        /// Yapı tipi etiketi (UI için)
        /// </summary>
        public string StructureTypeLabel => SelectedStructureType switch
        {
            StructureType.SingleUnit => "🏠 Müstakil",
            StructureType.Apartment => "🏢 Apartman",
            StructureType.Site => "🏘️ Site",
            StructureType.Commercial => "🏭 İşyeri/Fabrika",
            _ => "Yapı Seçin"
        };

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
            set => SetProperty(ref _isQuickAddCustomer, value);
        }

        /// <summary>
        /// Hızlı müşteri adı
        /// </summary>
        public string QuickCustomerName
        {
            get => _quickCustomerName;
            set => SetProperty(ref _quickCustomerName, value);
        }

        /// <summary>
        /// Hızlı müşteri telefonu
        /// </summary>
        public string QuickCustomerPhone
        {
            get => _quickCustomerPhone;
            set => SetProperty(ref _quickCustomerPhone, value);
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
        public bool IsExistingAsset => !IsNewAsset;

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
                    _serviceJobsView?.Refresh();
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
                    _serviceJobsView?.Refresh();
                }
            }
        }

        /// <summary>
        /// Durum filtre seçenekleri
        /// </summary>
        public ObservableCollection<StatusFilter> StatusFilters { get; } = new ObservableCollection<StatusFilter>
        {
            StatusFilter.Tümü,
            StatusFilter.Bekleyen,
            StatusFilter.DevamEden,
            StatusFilter.Tamamlanan
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
                    _serviceJobsView?.Refresh();
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
                    _serviceJobsView?.Refresh();
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
                    LoadJobItems();
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
                    LoadCustomerAssets();
                    LoadCustomerProjects();
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
        /// PDF Yazdır komutu
        /// </summary>
        public ICommand PrintServiceFormCommand { get; }

        /// <summary>
        /// Hızlı cihaz ekle komutu
        /// </summary>
        public ICommand AddAssetCommand { get; }

        #endregion

        /// <summary>
        /// Constructor
        /// </summary>
        public ServiceJobViewModel()
        {
            _context = new AppDbContext();

            ServiceJobs = new ObservableCollection<ServiceJob>();
            Customers = new ObservableCollection<Customer>();
            Products = new ObservableCollection<Product>();
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

            // Kategori çoklu seçimi için CategoryItems
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

            // Varsayılan kategori (geriye uyumluluk)
            _selectedJobCategory = JobCategory.CCTV;

            // ICollectionView oluştur ve filtre tanımla
            _serviceJobsView = CollectionViewSource.GetDefaultView(ServiceJobs);
            _serviceJobsView.Filter = FilterServiceJobs;

            // Komutları tanımla
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

            // Verileri yükle
            LoadData();
            
            // Varsayılan cihaz tipi seçeneklerini yükle
            UpdateDeviceTypeOptions();
        }

        #region Helper Methods

        /// <summary>
        /// Toplamları güncelle
        /// </summary>
        private void UpdateTotals()
        {
            OnPropertyChanged(nameof(MaterialTotal));
            OnPropertyChanged(nameof(GrandTotal));
        }

        #endregion

        #region Filtering

        /// <summary>
        /// Servis işi filtreleme metodu (Composite Filter)
        /// </summary>
        private bool FilterServiceJobs(object obj)
        {
            if (obj is not ServiceJob job) return false;

            // Status filter
            bool statusMatch = SelectedStatusFilter switch
            {
                StatusFilter.Bekleyen => job.Status == JobStatus.Pending,
                StatusFilter.DevamEden => job.Status == JobStatus.InProgress,
                StatusFilter.Tamamlanan => job.Status == JobStatus.Completed,
                _ => true // Tümü
            };

            if (!statusMatch) return false;

            // Date filter
            if (FilterStartDate.HasValue && job.CreatedDate < FilterStartDate.Value)
                return false;
            if (FilterEndDate.HasValue && job.CreatedDate > FilterEndDate.Value.AddDays(1))
                return false;

            // Search text filter
            if (string.IsNullOrWhiteSpace(SearchText)) return true;

            var searchLower = SearchText.ToLower();
            return job.Customer?.FullName.ToLower().Contains(searchLower) == true ||
                   job.Description.ToLower().Contains(searchLower);
        }

        /// <summary>
        /// Yeni iş formunu aç
        /// </summary>
        private void OpenNewJobForm()
        {
            var window = new NewServiceJobWindow();
            window.Owner = Application.Current.MainWindow;
            var result = window.ShowDialog();

            if (result == true)
            {
                RefreshList();
            }
        }

        /// <summary>
        /// Listeyi yenile
        /// </summary>
        private void RefreshList()
        {
            _context.ChangeTracker.Clear();
            LoadServiceJobs();
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

        #endregion

        #region Data Loading

        /// <summary>
        /// Tüm verileri yükle
        /// </summary>
        private void LoadData()
        {
            LoadCustomers();
            LoadProducts();
            LoadServiceJobs();
        }

        /// <summary>
        /// Müşterileri yükle
        /// </summary>
        private void LoadCustomers()
        {
            Customers.Clear();
            var customers = _context.Customers.ToList();
            foreach (var customer in customers)
            {
                Customers.Add(customer);
            }
        }

        /// <summary>
        /// Ürünleri yükle
        /// </summary>
        private void LoadProducts()
        {
            Products.Clear();
            var products = _context.Products.ToList();
            foreach (var product in products)
            {
                Products.Add(product);
            }
        }

        /// <summary>
        /// Seçilen müşterinin cihazlarını yükle
        /// </summary>
        private void LoadCustomerAssets()
        {
            CustomerAssets.Clear();

            if (SelectedCustomer == null) return;

            try
            {
                var assets = _context.CustomerAssets
                    .Where(a => a.CustomerId == SelectedCustomer.Id)
                    .OrderBy(a => a.Category)
                    .ThenBy(a => a.Brand)
                    .ToList();

                foreach (var asset in assets)
                {
                    CustomerAssets.Add(asset);
                }
            }
            catch
            {
                // Asset tablosu henüz oluşturulmamış olabilir
            }
        }

        /// <summary>
        /// Seçilen müşterinin projelerini yükle
        /// </summary>
        private void LoadCustomerProjects()
        {
            CustomerProjects.Clear();

            if (SelectedCustomer == null) return;

            try
            {
                var projects = _context.ServiceProjects
                    .Where(p => p.CustomerId == SelectedCustomer.Id &&
                               (p.Status == ProjectStatus.Draft ||
                                p.Status == ProjectStatus.Active ||
                                p.Status == ProjectStatus.PendingApproval))
                    .OrderByDescending(p => p.CreatedDate)
                    .ToList();

                foreach (var project in projects)
                {
                    CustomerProjects.Add(project);
                }
            }
            catch
            {
                // Project tablosu henüz oluşturulmamış olabilir
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
                Services.ToastNotificationManager.ShowSuccess($"Cihaz eklendi: {window.CreatedAsset.FullName}");
            }
        }

        /// <summary>
        /// İş kayıtlarını yükle
        /// </summary>
        private void LoadServiceJobs()
        {
            ServiceJobs.Clear();
            var jobs = _context.ServiceJobs
                .Include(j => j.Customer)
                .Include(j => j.ServiceJobItems)
                .ThenInclude(i => i.Product)
                .ToList();

            foreach (var job in jobs)
            {
                ServiceJobs.Add(job);
            }
        }

        /// <summary>
        /// Seçili işe ait ürünleri yükle
        /// </summary>
        private void LoadJobItems()
        {
            CurrentJobItems.Clear();

            if (SelectedServiceJob != null)
            {
                var items = _context.ServiceJobItems
                    .Include(i => i.Product)
                    .Where(i => i.ServiceJobId == SelectedServiceJob.Id)
                    .ToList();

                foreach (var item in items)
                {
                    CurrentJobItems.Add(item);
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
            return SelectedCustomer != null && !string.IsNullOrWhiteSpace(Description);
        }

        /// <summary>
        /// Yeni iş kaydet (Hibrit Cihaz Desteği ile)
        /// </summary>
        private void SaveServiceJob()
        {
            try
            {
                int? assetId = null;

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

                    _context.CustomerAssets.Add(newAsset);
                    _context.SaveChanges();

                    assetId = newAsset.Id;

                    // Listeye ekle
                    CustomerAssets.Add(newAsset);
                    Services.ToastNotificationManager.ShowSuccess($"Cihaz kaydedildi: {newAsset.FullName}");
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

                // === ADIM 3: İş emrini oluştur ===
                var newJob = new ServiceJob
                {
                    CustomerId = SelectedCustomer!.Id,
                    CustomerAssetId = assetId, // Cihaz bağlantısı
                    WorkOrderType = WorkOrderType.Repair, // Arıza
                    JobCategory = selectedCategories.Any() ? (JobCategory)selectedCategories.First() : JobCategory.CCTV,
                    CategoriesJson = categoriesJson,
                    Description = Description,
                    Status = JobStatus.Pending,
                    CreatedDate = DateTime.Now,
                    ScheduledDate = ScheduledDate,
                    AssignedTechnician = AssignedTechnician,
                    Priority = SelectedPriority,
                    LaborCost = LaborCost,
                    DiscountAmount = DiscountAmount
                };

                _context.ServiceJobs.Add(newJob);
                _context.SaveChanges();

                // === ADIM 4: Ürünleri kaydet ===
                foreach (var item in CurrentJobItems)
                {
                    var jobItem = new ServiceJobItem
                    {
                        ServiceJobId = newJob.Id,
                        ProductId = item.ProductId,
                        QuantityUsed = item.QuantityUsed,
                        UnitPrice = item.UnitPrice,
                        UnitCost = item.UnitCost
                    };
                    _context.ServiceJobItems.Add(jobItem);
                }

                _context.SaveChanges();
                LoadServiceJobs();
                ClearForm();

                Services.ToastNotificationManager.ShowSuccess("İş kaydı başarıyla oluşturuldu!");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Hata: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
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
                QuantityUsed = ApplyToAllUnits ? QuantityToAdd * TotalUnitCount : QuantityToAdd,
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
        private void CompleteJob()
        {
            if (SelectedServiceJob == null) return;

            try
            {
                // İşe ait ürünleri yükle
                var jobItems = _context.ServiceJobItems
                    .Include(i => i.Product)
                    .Where(i => i.ServiceJobId == SelectedServiceJob.Id)
                    .ToList();

                // STOK YETERLİLİĞİ KONTROLÜ
                foreach (var item in jobItems)
                {
                    // Note: Stok düşme işlemi artık Inventory üzerinden yapılmalı.
                    // Geçici olarak TotalStockQuantity üzerinden kontrol ediliyor.
                    if (item.Product.TotalStockQuantity < item.QuantityUsed)
                    {
                        MessageBox.Show(
                            $"Yetersiz stok!\n\n" +
                            $"Ürün: {item.Product.ProductName}\n" +
                            $"Gerekli: {item.QuantityUsed}\n" +
                            $"Mevcut: {item.Product.TotalStockQuantity}",
                            "Stok Yetersiz",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                        return;
                    }
                }

                // STOK DÜŞME İŞLEMİ
                foreach (var item in jobItems)
                {
                    item.Product.TotalStockQuantity -= item.QuantityUsed;
                    _context.Products.Update(item.Product);
                }

                // İŞ DURUMUNU GÜNCELLE
                SelectedServiceJob.Status = JobStatus.Completed;
                SelectedServiceJob.CompletedDate = DateTime.Now;
                _context.ServiceJobs.Update(SelectedServiceJob);

                // DEĞİŞİKLİKLERİ KAYDET
                _context.SaveChanges();

                // LİSTELERİ YENİLE
                LoadServiceJobs();
                LoadProducts();

                MessageBox.Show(
                    "İş başarıyla tamamlandı!\nStok miktarları güncellendi.",
                    "Başarılı",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Hata: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Formu temizle
        /// </summary>
        private void ClearForm()
        {
            // Single-page form reset
            SelectedStructureType = StructureType.SingleUnit;
            BlockCount = 1;
            FlatCount = 1;
            ApplyToAllUnits = false;
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
        private void PrintServiceForm(ServiceJob? job)
        {
            if (job == null) return;

            try
            {
                // İş kaydını tam yükle (Customer ve Items ile)
                var fullJob = _context.ServiceJobs
                    .Include(j => j.Customer)
                    .Include(j => j.ServiceJobItems)
                    .ThenInclude(i => i.Product)
                    .FirstOrDefault(j => j.Id == job.Id);

                if (fullJob == null)
                {
                    MessageBox.Show("İş kaydı bulunamadı.", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
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

                    MessageBox.Show("Servis formu başarıyla oluşturuldu.", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"PDF oluşturulurken hata: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion
    }
}
