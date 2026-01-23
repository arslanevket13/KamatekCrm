using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using KamatekCrm.Data;
using KamatekCrm.Enums;
using KamatekCrm.Commands;
using KamatekCrm.Models;
using Microsoft.EntityFrameworkCore;

namespace KamatekCrm.ViewModels
{
    /// <summary>
    /// Arıza & Servis Kaydı ViewModel
    /// Hızlı arıza/servis kaydı için optimize edilmiş basit akış
    /// </summary>
    public class FaultTicketViewModel : ViewModelBase
    {
        private readonly AppDbContext _context;

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

        #endregion

        #region Collections

        public ObservableCollection<Customer> Customers { get; } = new();
        public ObservableCollection<CustomerAsset> CustomerAssets { get; } = new();
        public ObservableCollection<JobCategory> Categories { get; } = new(
            Enum.GetValues(typeof(JobCategory)).Cast<JobCategory>());
        public ObservableCollection<JobPriority> Priorities { get; } = new(
            Enum.GetValues(typeof(JobPriority)).Cast<JobPriority>());

        #endregion

        #region Properties

        public Customer? SelectedCustomer
        {
            get => _selectedCustomer;
            set
            {
                if (SetProperty(ref _selectedCustomer, value))
                {
                    LoadCustomerAssets();
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
                {
                    SelectedCategory = value.Category;
                }
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
                {
                    OnPropertyChanged(nameof(IsExistingAsset));
                }
            }
        }

        public bool IsExistingAsset => !IsNewAsset;

        public string NewAssetBrand
        {
            get => _newAssetBrand;
            set => SetProperty(ref _newAssetBrand, value);
        }

        public string NewAssetModel
        {
            get => _newAssetModel;
            set => SetProperty(ref _newAssetModel, value);
        }

        public string NewAssetSerialNumber
        {
            get => _newAssetSerialNumber;
            set => SetProperty(ref _newAssetSerialNumber, value);
        }

        public string NewAssetLocation
        {
            get => _newAssetLocation;
            set => SetProperty(ref _newAssetLocation, value);
        }

        #endregion

        #region Cost Properties

        public decimal LaborCost
        {
            get => _laborCost;
            set
            {
                if (SetProperty(ref _laborCost, value))
                    OnPropertyChanged(nameof(TotalEstimate));
            }
        }

        public decimal EstimatedPartsTotal
        {
            get => _estimatedPartsTotal;
            set
            {
                if (SetProperty(ref _estimatedPartsTotal, value))
                    OnPropertyChanged(nameof(TotalEstimate));
            }
        }

        public decimal TotalEstimate => LaborCost + EstimatedPartsTotal;

        #endregion

        #region Commands

        public ICommand SaveFaultTicketCommand { get; }
        public ICommand CancelCommand { get; }

        #endregion

        #region Constructor

        public FaultTicketViewModel()
        {
            _context = new AppDbContext();

            SaveFaultTicketCommand = new RelayCommand(_ => SaveFaultTicket(), _ => CanSave());
            CancelCommand = new RelayCommand(_ => Cancel());

            LoadCustomers();
        }

        #endregion

        #region Private Methods

        private void LoadCustomers()
        {
            Customers.Clear();
            foreach (var customer in _context.Customers.OrderBy(c => c.FullName).ToList())
            {
                Customers.Add(customer);
            }
        }

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
            catch { /* Asset tablosu henüz oluşturulmamış olabilir */ }
        }

        private bool CanSave()
        {
            return SelectedCustomer != null &&
                   !string.IsNullOrWhiteSpace(Description);
        }

        private void SaveFaultTicket()
        {
            try
            {
                int? assetId = null;

                // Yeni cihaz mı?
                if (IsNewAsset)
                {
                    if (string.IsNullOrWhiteSpace(NewAssetBrand) || string.IsNullOrWhiteSpace(NewAssetModel))
                    {
                        MessageBox.Show("Yeni cihaz için Marka ve Model zorunludur.", "Uyarı",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    var newAsset = new CustomerAsset
                    {
                        CustomerId = SelectedCustomer!.Id,
                        Category = SelectedCategory,
                        Brand = NewAssetBrand.Trim(),
                        Model = NewAssetModel.Trim(),
                        SerialNumber = string.IsNullOrWhiteSpace(NewAssetSerialNumber) ? null : NewAssetSerialNumber.Trim(),
                        Location = string.IsNullOrWhiteSpace(NewAssetLocation) ? null : NewAssetLocation.Trim(),
                        Status = AssetStatus.NeedsRepair,
                        CreatedDate = DateTime.Now
                    };

                    _context.CustomerAssets.Add(newAsset);
                    _context.SaveChanges();
                    assetId = newAsset.Id;
                }
                else if (SelectedAsset != null)
                {
                    assetId = SelectedAsset.Id;

                    // Cihaz durumunu güncelle
                    SelectedAsset.Status = AssetStatus.NeedsRepair;
                }

                // Arıza kaydı oluştur
                var faultTicket = new ServiceJob
                {
                    CustomerId = SelectedCustomer!.Id,
                    CustomerAssetId = assetId,
                    ServiceJobType = ServiceJobType.Fault,
                    WorkOrderType = WorkOrderType.Repair,
                    WorkflowStatus = WorkflowStatus.Draft,
                    JobCategory = SelectedCategory,
                    Priority = SelectedPriority,
                    Description = $"ARIZA: {FaultSymptom}\n\n{Description}",
                    Status = JobStatus.Pending,
                    LaborCost = LaborCost,
                    CreatedDate = DateTime.Now
                };

                _context.ServiceJobs.Add(faultTicket);
                _context.SaveChanges();

                Services.ToastNotificationManager.ShowSuccess($"Arıza kaydı oluşturuldu: #{faultTicket.Id}");

                // Formu temizle
                ClearForm();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Hata: {ex.Message}", "Kayıt Hatası",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Cancel()
        {
            ClearForm();
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
        }

        #endregion
    }
}
