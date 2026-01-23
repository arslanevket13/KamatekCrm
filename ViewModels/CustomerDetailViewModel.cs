using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using KamatekCrm.Commands;
using KamatekCrm.Data;
using KamatekCrm.Enums;
using KamatekCrm.Models;
using Microsoft.EntityFrameworkCore;

namespace KamatekCrm.ViewModels
{
    /// <summary>
    /// Müşteri detay sayfası ViewModel
    /// </summary>
    public class CustomerDetailViewModel : ViewModelBase
    {
        private readonly AppDbContext _context;
        private readonly int _customerId;
        private Customer? _customer;

        // Editable Properties
        private string _fullName = string.Empty;
        private string _phoneNumber = string.Empty;
        private string? _email;
        private string _city = string.Empty;
        private string? _district;
        private string? _neighborhood;
        private string? _street;
        private string? _buildingNo;
        private string? _apartmentNo;
        private string? _notes;
        private CustomerType _customerType = CustomerType.Individual;
        private string _customerCode = string.Empty;
        private string? _tcKimlikNo;
        private string? _companyName;
        private string? _taxNumber;
        private string? _taxOffice;

        // Collections
        public ObservableCollection<ServiceJob> ServiceJobs { get; set; }
        public ObservableCollection<ServiceJob> ActiveJobs { get; set; }
        public ObservableCollection<ServiceJob> PastJobs { get; set; }
        public ObservableCollection<Transaction> Transactions { get; set; }

        // Calculated Properties
        private decimal _totalSpent;
        private decimal _totalBalance;

        public CustomerDetailViewModel(int customerId)
        {
            _context = new AppDbContext();
            _customerId = customerId;
            ServiceJobs = new ObservableCollection<ServiceJob>();
            ActiveJobs = new ObservableCollection<ServiceJob>();
            PastJobs = new ObservableCollection<ServiceJob>();
            Transactions = new ObservableCollection<Transaction>();

            SaveCommand = new RelayCommand(_ => SaveCustomer(), _ => CanSaveCustomer());
            BackCommand = new RelayCommand(_ => NavigateBack());
            NewServiceJobCommand = new RelayCommand(_ => CreateNewServiceJob());

            LoadCustomerData();
        }

        #region Properties

        public string FullName
        {
            get => _fullName;
            set => SetProperty(ref _fullName, value);
        }

        public string PhoneNumber
        {
            get => _phoneNumber;
            set => SetProperty(ref _phoneNumber, value);
        }

        public string? Email
        {
            get => _email;
            set => SetProperty(ref _email, value);
        }

        public string City
        {
            get => _city;
            set => SetProperty(ref _city, value);
        }

        public string? District
        {
            get => _district;
            set => SetProperty(ref _district, value);
        }

        public string? Neighborhood
        {
            get => _neighborhood;
            set => SetProperty(ref _neighborhood, value);
        }

        public string? Street
        {
            get => _street;
            set => SetProperty(ref _street, value);
        }

        public string? BuildingNo
        {
            get => _buildingNo;
            set => SetProperty(ref _buildingNo, value);
        }

        public string? ApartmentNo
        {
            get => _apartmentNo;
            set => SetProperty(ref _apartmentNo, value);
        }

        public string? Notes
        {
            get => _notes;
            set => SetProperty(ref _notes, value);
        }

        public CustomerType CustomerType
        {
            get => _customerType;
            set => SetProperty(ref _customerType, value);
        }

        public string CustomerCode
        {
            get => _customerCode;
            set => SetProperty(ref _customerCode, value);
        }

        public string? TcKimlikNo
        {
            get => _tcKimlikNo;
            set => SetProperty(ref _tcKimlikNo, value);
        }

        public string? CompanyName
        {
            get => _companyName;
            set => SetProperty(ref _companyName, value);
        }

        public string? TaxNumber
        {
            get => _taxNumber;
            set => SetProperty(ref _taxNumber, value);
        }

        public string? TaxOffice
        {
            get => _taxOffice;
            set => SetProperty(ref _taxOffice, value);
        }

        public decimal TotalSpent
        {
            get => _totalSpent;
            private set => SetProperty(ref _totalSpent, value);
        }

        public decimal TotalBalance
        {
            get => _totalBalance;
            private set => SetProperty(ref _totalBalance, value);
        }

        /// <summary>
        /// Aktif iş sayısı
        /// </summary>
        public int ActiveJobCount => ActiveJobs?.Count ?? 0;

        #endregion

        #region Commands

        public ICommand SaveCommand { get; }
        public ICommand BackCommand { get; }
        public ICommand NewServiceJobCommand { get; }

        #endregion

        #region Methods

        private void LoadCustomerData()
        {
            try
            {
                // EF Core Include ile ilişkili verileri yükle
                _customer = _context.Customers
                    .Include(c => c.ServiceJobs)
                    .Include(c => c.Transactions)
                    .FirstOrDefault(c => c.Id == _customerId);

                if (_customer == null)
                {
                    MessageBox.Show("Müşteri bulunamadı!", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Editable alanları doldur
                FullName = _customer.FullName;
                PhoneNumber = _customer.PhoneNumber;
                Email = _customer.Email;
                City = _customer.City;
                District = _customer.District;
                Neighborhood = _customer.Neighborhood;
                Street = _customer.Street;
                BuildingNo = _customer.BuildingNo;
                ApartmentNo = _customer.ApartmentNo;
                Notes = _customer.Notes;
                CustomerType = _customer.Type;
                CustomerCode = _customer.CustomerCode;
                TcKimlikNo = _customer.TcKimlikNo;
                CompanyName = _customer.CompanyName;
                TaxNumber = _customer.TaxNumber;
                TaxOffice = _customer.TaxOffice;

                // ServiceJobs koleksiyonlarını doldur
                ServiceJobs.Clear();
                ActiveJobs.Clear();
                PastJobs.Clear();

                foreach (var job in _customer.ServiceJobs.OrderByDescending(j => j.CreatedDate))
                {
                    ServiceJobs.Add(job);

                    // Aktif mi tamamlanmış mı ayır
                    if (job.Status == Enums.JobStatus.Completed)
                    {
                        PastJobs.Add(job);
                    }
                    else
                    {
                        ActiveJobs.Add(job);
                    }
                }

                OnPropertyChanged(nameof(ActiveJobCount));

                // Transactions koleksiyonunu doldur
                Transactions.Clear();
                foreach (var transaction in _customer.Transactions.OrderByDescending(t => t.Date))
                {
                    Transactions.Add(transaction);
                }

                // Hesaplamaları yap
                CalculateTotals();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Veri yükleme hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CalculateTotals()
        {
            // Toplam harcama (ServiceJob fiyatları toplamı)
            TotalSpent = ServiceJobs.Sum(j => j.Price);

            // Toplam bakiye (Borçlar - Ödemeler)
            var totalDebts = Transactions.Where(t => t.Type == Enums.TransactionType.Debt).Sum(t => t.Amount);
            var totalPayments = Transactions.Where(t => t.Type == Enums.TransactionType.Payment).Sum(t => t.Amount);
            TotalBalance = totalDebts - totalPayments;
        }

        private bool CanSaveCustomer()
        {
            return !string.IsNullOrWhiteSpace(FullName) &&
                   !string.IsNullOrWhiteSpace(PhoneNumber) &&
                   !string.IsNullOrWhiteSpace(City);
        }

        private void SaveCustomer()
        {
            if (_customer == null) return;

            try
            {
                // Müşteri bilgilerini güncelle
                _customer.FullName = FullName;
                _customer.PhoneNumber = PhoneNumber;
                _customer.Email = Email;
                _customer.City = City;
                _customer.District = District;
                _customer.Neighborhood = Neighborhood;
                _customer.Street = Street;
                _customer.BuildingNo = BuildingNo;
                _customer.ApartmentNo = ApartmentNo;
                _customer.Notes = Notes;
                _customer.Type = CustomerType;
                _customer.TcKimlikNo = TcKimlikNo;
                _customer.CompanyName = CompanyName;
                _customer.TaxNumber = TaxNumber;
                _customer.TaxOffice = TaxOffice;

                _context.Customers.Update(_customer);
                _context.SaveChanges();

                MessageBox.Show("Müşteri bilgileri başarıyla güncellendi!", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Kaydetme hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void NavigateBack()
        {
            // MainViewModel'e geri dön
            var mainWindow = Application.Current.MainWindow;
            if (mainWindow?.DataContext is MainViewModel mainViewModel)
            {
                mainViewModel.CurrentView = new CustomersViewModel();
            }
        }

        private void CreateNewServiceJob()
        {
            // ServiceJobViewModel'e git ve bu müşteriyi önceden seç
            var mainWindow = Application.Current.MainWindow;
            if (mainWindow?.DataContext is MainViewModel mainViewModel)
            {
                var serviceJobViewModel = new ServiceJobViewModel();

                // Müşteriyi önceden seç
                if (_customer != null)
                {
                    serviceJobViewModel.SelectedCustomer = _customer;
                }

                mainViewModel.CurrentView = serviceJobViewModel;
            }
        }

        #endregion
    }
}
