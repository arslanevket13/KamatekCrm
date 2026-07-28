using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using KamatekCrm.Infrastructure.Data;
using KamatekCrm.Services;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;
using KamatekCrm.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KamatekCrm.ViewModels
{
    /// <summary>
    /// Müşteri detay sayfası ViewModel - 360 Derece Görünüm
    /// </summary>
    public partial class CustomerDetailViewModel : ViewModelBase
    {
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
        public ObservableCollection<SalesOrder> SalesOrders { get; set; }
        public ObservableCollection<CustomerActivity> Activities { get; set; }

        // Yeni Alanlar
        private string? _tags;
        private CustomerSegment _segment;
        private DateTime? _birthDate;
        private string? _loyaltyLevel;

        // Calculated Properties
        private decimal _totalSpent;
        private decimal _totalBalance;

        private readonly NavigationService _navigationService;
        private readonly IToastService _toastService;
        private readonly ILoadingService _loadingService;
        private readonly IDbContextFactory<AppDbContext> _dbContextFactory;

        private int _customerId;

        public CustomerDetailViewModel(
            NavigationService navigationService, 
            IToastService toastService, 
            ILoadingService loadingService,
            IDbContextFactory<AppDbContext> dbContextFactory)
        {
            _navigationService = navigationService;
            _toastService = toastService;
            _loadingService = loadingService;
            _dbContextFactory = dbContextFactory;
            
            ServiceJobs = new ObservableCollection<ServiceJob>();
            ActiveJobs = new ObservableCollection<ServiceJob>();
            PastJobs = new ObservableCollection<ServiceJob>();
            Transactions = new ObservableCollection<Transaction>();
            SalesOrders = new ObservableCollection<SalesOrder>();
            Activities = new ObservableCollection<CustomerActivity>();
        }

        public void Initialize(int customerId)
        {
            _customerId = customerId;
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
            private set 
            {
               if(SetProperty(ref _totalBalance, value))
               {
                   OnPropertyChanged(nameof(BalanceColor));
               }
            }
        }

        public string BalanceColor => TotalBalance > 0 ? "#F44336" : (TotalBalance < 0 ? "#2E7D32" : "#757575");
        
        /// <summary>
        /// Aktif iş sayısı
        /// </summary>
        public int ActiveJobCount => ActiveJobs?.Count ?? 0;

        // Yeni Alanlar için Property'ler
        public string? Tags
        {
            get => _tags;
            set => SetProperty(ref _tags, value);
        }

        public CustomerSegment Segment
        {
            get => _segment;
            set => SetProperty(ref _segment, value);
        }

        public DateTime? BirthDate
        {
            get => _birthDate;
            set => SetProperty(ref _birthDate, value);
        }

        public string? LoyaltyLevel
        {
            get => _loyaltyLevel;
            private set => SetProperty(ref _loyaltyLevel, value);
        }

        /// <summary>
        /// Doğum günü yaklaşıyor mu?
        /// </summary>
        public bool HasUpcomingBirthday
        {
            get
            {
                if (!BirthDate.HasValue) return false;
                var today = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);
                var bday = BirthDate.Value;
                var thisYearBirthday = new DateTime(today.Year, bday.Month, bday.Day, 0, 0, 0, DateTimeKind.Utc);
                var daysUntil = (thisYearBirthday - today).Days;
                return daysUntil >= 0 && daysUntil <= 30;
            }
        }

        public IRelayCommand BackCommand => NavigateBackCommand;
        public IRelayCommand NewServiceJobCommand => CreateNewServiceJobCommand;
        public IRelayCommand SaveCommand => SaveCustomerCommand;

        #endregion

        private bool CanSaveTags() => _customer != null;
        private bool CanSaveSegment() => _customer != null;

        #region Methods

        private void LoadCustomerData()
        {
            try
            {
                using var context = _dbContextFactory.CreateDbContext();
                // EF Core Include ile ilişkili verileri yükle
                _customer = context.Customers
                    .Include(c => c.ServiceJobs)
                    .Include(c => c.Transactions)
                    .FirstOrDefault(c => c.Id == _customerId);

                if (_customer == null)
                {
                    MessageBox.Show("Müşteri bulunamadı!", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Customer modelinde SalesOrders koleksiyonu yoksa manuel yükle
                var salesOrders = context.SalesOrders.Where(s => s.CustomerId == _customerId).ToList();

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

                // Yeni alanları doldur
                Tags = _customer.Tags;
                Segment = _customer.Segment;
                BirthDate = _customer.BirthDate;
                LoyaltyLevel = _customer.LoyaltyLevel;

                // ServiceJobs koleksiyonlarını doldur
                ServiceJobs.Clear();
                ActiveJobs.Clear();
                PastJobs.Clear();

                foreach (var job in _customer.ServiceJobs.OrderByDescending(j => j.CreatedDate))
                {
                    ServiceJobs.Add(job);

                    // Aktif mi tamamlanmış mı ayır
                    if (job.Status == JobStatus.Completed)
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

                // SalesOrders koleksiyonunu doldur
                SalesOrders.Clear();
                foreach(var order in salesOrders.OrderByDescending(o => o.Date))
                {
                    SalesOrders.Add(order);
                }

                // Customer Activities (Timeline) yükle
                Activities.Clear();
                var activities = context.CustomerActivities
                    .Where(a => a.CustomerId == _customerId)
                    .OrderByDescending(a => a.CreatedDate)
                    .Take(50)
                    .ToList();
                foreach (var activity in activities)
                {
                    Activities.Add(activity);
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
            // Toplam harcama (Servis + Satış)
            var serviceTotal = ServiceJobs.Sum(j => j.Price);
            var salesTotal = SalesOrders.Sum(s => (decimal)s.TotalAmount); // SalesOrder TotalAmount double olabilir
            TotalSpent = serviceTotal + salesTotal;

            // Toplam bakiye (Borçlar - Ödemeler)
            var totalDebts = Transactions.Where(t => t.Type == TransactionType.Debt).Sum(t => t.Amount);
            var totalPayments = Transactions.Where(t => t.Type == TransactionType.Payment).Sum(t => t.Amount);
            
            // Pozitif bakiye = Müşteri Borçlu (Kırmızı)
            // Negatif bakiye = Müşteri Alacaklı (Yeşil)
            TotalBalance = totalDebts - totalPayments;
        }

        [RelayCommand]
        private void AddPayment() => AddTransaction(TransactionType.Payment);

        [RelayCommand]
        private void AddDebt() => AddTransaction(TransactionType.Debt);

        private void AddTransaction(TransactionType type)
        {
            // Safety check for empty or invalid input without using legacy VisualBasic InputBox
            var input = "0";

            if (string.IsNullOrWhiteSpace(input)) return;

            decimal amount = 0;
            string description = type == TransactionType.Payment ? "Tahsilat" : "Borç Yansıtma";

            // Parse input format "Amount - Description"
            if (input.Contains("-"))
            {
                var parts = input.Split('-', 2);
                if (decimal.TryParse(parts[0].Trim(), out decimal parsedAmount))
                {
                    amount = parsedAmount;
                    description = parts[1].Trim();
                }
            }
            else
            {
                decimal.TryParse(input, out amount);
            }

            if (amount <= 0)
            {
                MessageBox.Show("Geçerli bir tutar giriniz.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using var context = _dbContextFactory.CreateDbContext();
                var transaction = new Transaction
                {
                    CustomerId = _customerId,
                    Type = type,
                    Amount = amount,
                    Date = DateTime.UtcNow,
                    Description = description
                };

                context.Transactions.Add(transaction);
                context.SaveChanges();

                Transactions.Insert(0, transaction);
                CalculateTotals();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"İşlem eklenirken hata: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool CanSaveCustomer()
        {
            return !string.IsNullOrWhiteSpace(FullName) &&
                   !string.IsNullOrWhiteSpace(PhoneNumber) &&
                   !string.IsNullOrWhiteSpace(City);
        }

        [RelayCommand(CanExecute = nameof(CanSaveCustomer))]
        private async Task SaveCustomerAsync()
        {
            if (_customerId <= 0) return;

            try
            {
                using var context = await _dbContextFactory.CreateDbContextAsync();
                var existingCustomer = await context.Customers.FindAsync(_customerId);

                if (existingCustomer != null)
                {
                    existingCustomer.FullName = FullName;
                    existingCustomer.PhoneNumber = PhoneNumber;
                    existingCustomer.Email = Email;
                    existingCustomer.City = City;
                    existingCustomer.District = District;
                    existingCustomer.Neighborhood = Neighborhood;
                    existingCustomer.Street = Street;
                    existingCustomer.BuildingNo = BuildingNo;
                    existingCustomer.ApartmentNo = ApartmentNo;
                    existingCustomer.Notes = Notes;
                    existingCustomer.Type = CustomerType;
                    existingCustomer.TcKimlikNo = TcKimlikNo;
                    existingCustomer.CompanyName = CompanyName;
                    existingCustomer.TaxNumber = TaxNumber;
                    existingCustomer.TaxOffice = TaxOffice;

                    await context.SaveChangesAsync();

                    if (_customer != null)
                    {
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
                    }

                    _toastService.ShowSuccess("Müşteri bilgileri başarıyla güncellendi!");
                }
            }
            catch (Exception ex)
            {
                _toastService.ShowError($"Kaydetme hatası: {ex.Message}");
            }
        }

        [RelayCommand]
        private void NavigateBack()
        {
            // CustomersViewModel'e geri dön (DI üzerinden yeniden oluşturulur ve veri yüklenir)
            _navigationService.NavigateTo<CustomersViewModel>();
        }

        [RelayCommand]
        private async Task CreateNewServiceJobAsync()
        {
            if (_customer == null) return;

            var serviceJobVm = App.ServiceProvider.GetService<ServiceJobViewModel>();
            if (serviceJobVm == null) return;

            serviceJobVm.SelectedCustomer = _customer;

            var window = new NewServiceJobWindow(serviceJobVm)
            {
                Owner = Application.Current?.MainWindow,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            var result = window.ShowDialog();
            if (result == true)
            {
                LoadCustomerData();
            }
        }

        [RelayCommand]
        private void AddNote()
        {
            var note = Notes;
            if (string.IsNullOrWhiteSpace(note))
            {
                MessageBox.Show("Lütfen bir not girin!", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using var context = _dbContextFactory.CreateDbContext();
                var activity = new CustomerActivity
                {
                    CustomerId = _customerId,
                    Type = ActivityType.NoteAdded,
                    Description = note,
                    CreatedDate = DateTime.UtcNow,
                    CreatedBy = "Kullanıcı"
                };

                context.CustomerActivities.Add(activity);

                var customerInDb = context.Customers.Find(_customerId);
                if (customerInDb != null)
                {
                    customerInDb.Notes = string.IsNullOrEmpty(customerInDb.Notes) 
                        ? note 
                        : customerInDb.Notes + "\n" + DateTime.UtcNow.ToString("dd.MM.yyyy") + ": " + note;
                    customerInDb.LastInteractionDate = DateTime.UtcNow;
                }

                context.SaveChanges();

                Activities.Insert(0, activity);
                Notes = string.Empty;
                _toastService.ShowSuccess("Not başarıyla eklendi!");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Not ekleme hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand(CanExecute = nameof(CanSaveTags))]
        private void SaveTags()
        {
            if (_customerId <= 0) return;

            try
            {
                using var context = _dbContextFactory.CreateDbContext();
                var customerInDb = context.Customers.Find(_customerId);
                if (customerInDb != null)
                {
                    customerInDb.Tags = Tags;
                    customerInDb.LastInteractionDate = DateTime.UtcNow;
                }

                var activity = new CustomerActivity
                {
                    CustomerId = _customerId,
                    Type = ActivityType.TagAdded,
                    Description = $"Etiketler güncellendi: {Tags}",
                    CreatedDate = DateTime.UtcNow,
                    CreatedBy = "Kullanıcı"
                };
                context.CustomerActivities.Add(activity);
                context.SaveChanges();

                Activities.Insert(0, activity);
                _toastService.ShowSuccess("Etiketler kaydedildi!");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Etiket kaydetme hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand(CanExecute = nameof(CanSaveSegment))]
        private void SaveSegment()
        {
            if (_customerId <= 0) return;

            try
            {
                using var context = _dbContextFactory.CreateDbContext();
                var customerInDb = context.Customers.Find(_customerId);
                if (customerInDb != null)
                {
                    customerInDb.Segment = Segment;
                    customerInDb.LastInteractionDate = DateTime.UtcNow;
                    context.SaveChanges();
                }

                _toastService.ShowSuccess($"Müşteri segmenti '{Segment}' olarak güncellendi!");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Segment güncelleme hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion
    }
}
