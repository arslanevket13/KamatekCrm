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
using KamatekCrm.Shared.Helpers;
using KamatekCrm.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using KamatekCrm.ApplicationCore.Interfaces;
using KamatekCrm.ApplicationCore.Security;
using KamatekCrm.ApplicationCore.Services;
using KamatekCrm.Events;

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

        private City? _selectedCity;
        private District? _selectedDistrict;
        private Neighborhood? _selectedNeighborhood;
        private bool _isAddressLoading;

        public ObservableCollection<City> Cities { get; set; }
        public ObservableCollection<District> Districts { get; set; }
        public ObservableCollection<Neighborhood> Neighborhoods { get; set; }

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
        private decimal _totalDebt;
        private decimal _totalCredit;
        private decimal _netBalance;

        private readonly NavigationService _navigationService;
        private readonly IToastService _toastService;
        private readonly ILoadingService _loadingService;
        private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
        private readonly IPersonalDataProtectionService _personalDataProtection;
        private readonly IAuditTrailService _auditTrail;
        private readonly PdfService? _pdfService;

        private int _customerId;

        public CustomerDetailViewModel(
            NavigationService navigationService, 
            IToastService toastService, 
            ILoadingService loadingService,
            IDbContextFactory<AppDbContext> dbContextFactory,
            IPersonalDataProtectionService personalDataProtection,
            IAuditTrailService auditTrail,
            PdfService? pdfService = null)
        {
            _navigationService = navigationService;
            _toastService = toastService;
            _loadingService = loadingService;
            _dbContextFactory = dbContextFactory;
            _personalDataProtection = personalDataProtection;
            _auditTrail = auditTrail;
            _pdfService = pdfService;
            
            Cities = new ObservableCollection<City>(AddressService.GetCities());
            Districts = new ObservableCollection<District>();
            Neighborhoods = new ObservableCollection<Neighborhood>();

            ServiceJobs = new ObservableCollection<ServiceJob>();
            ActiveJobs = new ObservableCollection<ServiceJob>();
            PastJobs = new ObservableCollection<ServiceJob>();
            Transactions = new ObservableCollection<Transaction>();
            SalesOrders = new ObservableCollection<SalesOrder>();
            Activities = new ObservableCollection<CustomerActivity>();

            EventAggregator.Instance.Subscribe<CustomerInteractionChangedEvent>(OnInteractionChanged);
        }

        private void OnInteractionChanged(CustomerInteractionChangedEvent evt)
        {
            if (_customerId > 0 && evt.CustomerId == _customerId)
            {
                Application.Current?.Dispatcher.InvokeAsync(async () =>
                {
                    await LoadCustomerDataAsync();
                });
            }
        }

        public void Initialize(int customerId)
        {
            _ = InitializeAsync(customerId);
        }

        public async Task InitializeAsync(int customerId)
        {
            _customerId = customerId;
            await LoadCustomerDataAsync();
        }


        #region Properties

        public string FullName
        {
            get => _fullName;
            set => SetProperty(ref _fullName, value);
        }

        public string PhoneNumber
        {
            get => _personalDataProtection.Protect(_phoneNumber, PersonalDataKind.Phone);
            set
            {
                if (CanViewContactData) SetProperty(ref _phoneNumber, value);
            }
        }

        public string? Email
        {
            get => _personalDataProtection.Protect(_email, PersonalDataKind.Email);
            set
            {
                if (CanViewContactData) SetProperty(ref _email, value);
            }
        }

        public string City
        {
            get => _personalDataProtection.Protect(_city, PersonalDataKind.Address);
            set
            {
                if (CanViewContactData) SetProperty(ref _city, value);
            }
        }

        public string? District
        {
            get => _personalDataProtection.Protect(_district, PersonalDataKind.Address);
            set
            {
                if (CanViewContactData) SetProperty(ref _district, value);
            }
        }

        public string? Neighborhood
        {
            get => _personalDataProtection.Protect(_neighborhood, PersonalDataKind.Address);
            set
            {
                if (CanViewContactData) SetProperty(ref _neighborhood, value);
            }
        }

        public string? Street
        {
            get => _personalDataProtection.Protect(_street, PersonalDataKind.Address);
            set
            {
                if (CanViewContactData) SetProperty(ref _street, value);
            }
        }

        public string? BuildingNo
        {
            get => _personalDataProtection.Protect(_buildingNo, PersonalDataKind.Address);
            set
            {
                if (CanViewContactData) SetProperty(ref _buildingNo, value);
            }
        }

        public string? ApartmentNo
        {
            get => _personalDataProtection.Protect(_apartmentNo, PersonalDataKind.Address);
            set
            {
                if (CanViewContactData) SetProperty(ref _apartmentNo, value);
            }
        }

        public City? SelectedCity
        {
            get => _selectedCity;
            set
            {
                if (SetProperty(ref _selectedCity, value))
                {
                    if (_isAddressLoading) return;

                    Districts.Clear();
                    Neighborhoods.Clear();
                    SelectedDistrict = null;
                    SelectedNeighborhood = null;

                    if (value != null)
                    {
                        _city = value.Name;
                        OnPropertyChanged(nameof(City));
                        var districts = AddressService.GetDistricts(value.Name);
                        foreach (var d in districts) Districts.Add(d);
                    }
                    else
                    {
                        _city = string.Empty;
                        OnPropertyChanged(nameof(City));
                    }
                }
            }
        }

        public District? SelectedDistrict
        {
            get => _selectedDistrict;
            set
            {
                if (SetProperty(ref _selectedDistrict, value))
                {
                    if (_isAddressLoading) return;

                    Neighborhoods.Clear();
                    SelectedNeighborhood = null;

                    if (value != null && SelectedCity != null)
                    {
                        _district = value.Name;
                        OnPropertyChanged(nameof(District));
                        var neighborhoods = AddressService.GetNeighborhoods(SelectedCity.Name, value.Name);
                        foreach (var n in neighborhoods) Neighborhoods.Add(n);
                    }
                    else
                    {
                        _district = null;
                        OnPropertyChanged(nameof(District));
                    }
                }
            }
        }

        public Neighborhood? SelectedNeighborhood
        {
            get => _selectedNeighborhood;
            set
            {
                if (SetProperty(ref _selectedNeighborhood, value))
                {
                    if (value != null)
                    {
                        _neighborhood = value.Name;
                        OnPropertyChanged(nameof(Neighborhood));
                    }
                }
            }
        }

        public void SetAddress(string? cityName, string? districtName, string? neighborhoodName)
        {
            _isAddressLoading = true;
            try
            {
                Districts.Clear();
                Neighborhoods.Clear();

                if (string.IsNullOrWhiteSpace(cityName))
                {
                    _selectedCity = null;
                    _selectedDistrict = null;
                    _selectedNeighborhood = null;
                    _city = string.Empty;
                    _district = null;
                    _neighborhood = null;
                    OnPropertyChanged(nameof(SelectedCity));
                    OnPropertyChanged(nameof(SelectedDistrict));
                    OnPropertyChanged(nameof(SelectedNeighborhood));
                    OnPropertyChanged(nameof(City));
                    OnPropertyChanged(nameof(District));
                    OnPropertyChanged(nameof(Neighborhood));
                    return;
                }

                var city = Cities.FirstOrDefault(c => AddressService.TurkishEquals(c.Name, cityName)) ?? AddressService.FindCity(cityName);
                _selectedCity = city;
                _city = city?.Name ?? cityName;
                OnPropertyChanged(nameof(SelectedCity));
                OnPropertyChanged(nameof(City));

                if (city != null)
                {
                    var districts = AddressService.GetDistricts(city.Name);
                    foreach (var d in districts) Districts.Add(d);

                    var district = Districts.FirstOrDefault(d => AddressService.TurkishEquals(d.Name, districtName));
                    _selectedDistrict = district;
                    _district = district?.Name ?? districtName;
                    OnPropertyChanged(nameof(SelectedDistrict));
                    OnPropertyChanged(nameof(District));

                    if (district != null)
                    {
                        var neighborhoods = AddressService.GetNeighborhoods(city.Name, district.Name);
                        foreach (var n in neighborhoods) Neighborhoods.Add(n);

                        var neighborhood = Neighborhoods.FirstOrDefault(n => AddressService.TurkishEquals(n.Name, neighborhoodName));
                        _selectedNeighborhood = neighborhood;
                        _neighborhood = neighborhood?.Name ?? neighborhoodName;
                        OnPropertyChanged(nameof(SelectedNeighborhood));
                        OnPropertyChanged(nameof(Neighborhood));
                    }
                    else
                    {
                        _selectedNeighborhood = null;
                        _neighborhood = neighborhoodName;
                        OnPropertyChanged(nameof(SelectedNeighborhood));
                        OnPropertyChanged(nameof(Neighborhood));
                    }
                }
                else
                {
                    _selectedDistrict = null;
                    _selectedNeighborhood = null;
                    _district = districtName;
                    _neighborhood = neighborhoodName;
                    OnPropertyChanged(nameof(SelectedDistrict));
                    OnPropertyChanged(nameof(SelectedNeighborhood));
                    OnPropertyChanged(nameof(District));
                    OnPropertyChanged(nameof(Neighborhood));
                }
            }
            finally
            {
                _isAddressLoading = false;
            }
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
            get => _personalDataProtection.Protect(_tcKimlikNo, PersonalDataKind.NationalIdentity);
            set
            {
                if (CanViewIdentityData) SetProperty(ref _tcKimlikNo, value);
            }
        }

        public string? CompanyName
        {
            get => _companyName;
            set => SetProperty(ref _companyName, value);
        }

        public string? TaxNumber
        {
            get => _personalDataProtection.Protect(_taxNumber, PersonalDataKind.TaxNumber);
            set
            {
                if (CanViewIdentityData) SetProperty(ref _taxNumber, value);
            }
        }

        public bool CanViewIdentityData =>
            _personalDataProtection.CanView(PersonalDataKind.NationalIdentity);

        public bool CanViewContactData =>
            _personalDataProtection.CanView(PersonalDataKind.Phone);

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

        public decimal TotalDebt
        {
            get => _totalDebt;
            private set => SetProperty(ref _totalDebt, value);
        }

        public decimal TotalCredit
        {
            get => _totalCredit;
            private set => SetProperty(ref _totalCredit, value);
        }

        public decimal NetBalance
        {
            get => _netBalance;
            private set => SetProperty(ref _netBalance, value);
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

        public async Task LoadCustomerDataAsync()
        {
            try
            {
                _loadingService.Show("Müşteri profili yükleniyor...");

                Customer? customer = null;
                List<SalesOrder> salesOrders = new();
                List<CustomerActivity> manualActivities = new();
                List<CustomerInteraction> interactions = new();
                List<Quote> quotes = new();

                var context = await _dbContextFactory.CreateDbContextAsync().ConfigureAwait(false);
                await using (context.ConfigureAwait(false))
                {
                    // EF Core AsSplitQuery ile ilişkili verileri thread-safe asenkron yükle
                    customer = await context.Customers.AsNoTracking()
                        .Include(c => c.ServiceJobs)
                        .Include(c => c.Transactions)
                        .AsSplitQuery()
                        .FirstOrDefaultAsync(c => c.Id == _customerId).ConfigureAwait(false);

                    if (customer == null)
                    {
                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            MessageBox.Show("Müşteri bulunamadı!", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                        });
                        return;
                    }

                    if (CanViewContactData || CanViewIdentityData)
                    {
                        var auditResult = await _auditTrail.WriteAsync(
                            AuditActionType.View,
                            "CustomerPersonalData",
                            _customerId.ToString(),
                            "Müşteri kişisel veri detayları görüntülendi.").ConfigureAwait(false);
                        if (auditResult.IsFailure)
                            System.Diagnostics.Debug.WriteLine(auditResult.Error);
                    }

                    salesOrders = await context.SalesOrders.AsNoTracking()
                        .Where(s => s.CustomerId == _customerId)
                        .ToListAsync().ConfigureAwait(false);

                    manualActivities = await context.CustomerActivities.AsNoTracking()
                        .Where(a => a.CustomerId == _customerId)
                        .OrderByDescending(a => a.CreatedDate)
                        .Take(50)
                        .ToListAsync().ConfigureAwait(false);

                    interactions = await context.CustomerInteractions.AsNoTracking()
                        .Where(i => i.CustomerId == _customerId)
                        .OrderByDescending(i => i.InteractionDate)
                        .Take(50)
                        .ToListAsync().ConfigureAwait(false);

                    quotes = await context.Quotes.AsNoTracking()
                        .Where(q => q.CustomerId == _customerId)
                        .OrderByDescending(q => q.Date)
                        .Take(50)
                        .ToListAsync().ConfigureAwait(false);
                }

                // UI koleksiyonlarını Dispatcher üzerinde sorunsuz doldur
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    _customer = customer;

                    // Editable alanları doldur
                    FullName = _customer.FullName;
                    _phoneNumber = _customer.PhoneNumber;
                    _email = _customer.Email;
                    SetAddress(_customer.City, _customer.District, _customer.Neighborhood);
                    _street = _customer.Street;
                    _buildingNo = _customer.BuildingNo;
                    _apartmentNo = _customer.ApartmentNo;
                    OnPropertyChanged(nameof(PhoneNumber));
                    OnPropertyChanged(nameof(Email));
                    OnPropertyChanged(nameof(Street));
                    OnPropertyChanged(nameof(BuildingNo));
                    OnPropertyChanged(nameof(ApartmentNo));
                    Notes = _customer.Notes;
                    CustomerType = _customer.Type;
                    CustomerCode = _customer.CustomerCode;
                    _tcKimlikNo = _customer.TcKimlikNo;
                    OnPropertyChanged(nameof(TcKimlikNo));
                    CompanyName = _customer.CompanyName;
                    _taxNumber = _customer.TaxNumber;
                    OnPropertyChanged(nameof(TaxNumber));
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

                    if (_customer.ServiceJobs != null)
                    {
                        foreach (var job in _customer.ServiceJobs.OrderByDescending(j => j.CreatedDate))
                        {
                            ServiceJobs.Add(job);
                            if (job.Status == JobStatus.Completed)
                            {
                                PastJobs.Add(job);
                            }
                            else
                            {
                                ActiveJobs.Add(job);
                            }
                        }
                    }

                    OnPropertyChanged(nameof(ActiveJobCount));

                    // Transactions koleksiyonunu doldur
                    Transactions.Clear();
                    if (_customer.Transactions != null)
                    {
                        foreach (var transaction in _customer.Transactions.OrderByDescending(t => t.Date))
                        {
                            Transactions.Add(transaction);
                        }
                    }

                    // SalesOrders koleksiyonunu doldur
                    SalesOrders.Clear();
                    foreach (var order in salesOrders.OrderByDescending(o => o.Date))
                    {
                        SalesOrders.Add(order);
                    }

                    // 360° Müşteri Zaman Çizelgesi (Timeline)
                    var allActivities = new System.Collections.Generic.List<CustomerActivity>();
                    allActivities.AddRange(manualActivities);

                    foreach (var i in interactions)
                    {
                        allActivities.Add(new CustomerActivity
                        {
                            CustomerId = _customerId,
                            Type = i.Channel == InteractionChannel.Email ? ActivityType.EmailSent : (i.Channel == InteractionChannel.InPerson ? ActivityType.VisitMade : ActivityType.CallMade),
                            Description = $"Görüşme ({i.Channel}): {i.Subject} - {i.Summary}",
                            CreatedDate = i.InteractionDate,
                            CreatedBy = string.IsNullOrWhiteSpace(i.CreatedByUserId) ? "İletişim Merkezi" : i.CreatedByUserId,
                            RelatedId = i.Id,
                            RelatedType = "CustomerInteraction"
                        });
                    }

                    if (_customer.ServiceJobs != null)
                    {
                        foreach (var job in _customer.ServiceJobs)
                        {
                            allActivities.Add(new CustomerActivity
                            {
                                CustomerId = _customerId,
                                Type = job.Status == JobStatus.Completed ? ActivityType.ServiceJobCompleted : ActivityType.ServiceJobCreated,
                                Description = $"Servis İşi #{job.Id} ({job.JobCategory}): {job.Description} - {job.Price:C2} [{job.Status}]",
                                CreatedDate = job.CreatedDate,
                                CreatedBy = "Teknik Servis",
                                RelatedId = job.Id,
                                RelatedType = "ServiceJob"
                            });
                        }
                    }

                    foreach (var order in salesOrders)
                    {
                        allActivities.Add(new CustomerActivity
                        {
                            CustomerId = _customerId,
                            Type = ActivityType.Sale,
                            Description = $"Satış #{order.OrderNumber}: {order.TotalAmount:C2} ({order.PaymentMethod}) [{order.Status}]",
                            CreatedDate = order.Date,
                            CreatedBy = "Satış",
                            RelatedId = order.Id,
                            RelatedType = "SalesOrder"
                        });
                    }

                    foreach (var quote in quotes)
                    {
                        allActivities.Add(new CustomerActivity
                        {
                            CustomerId = _customerId,
                            Type = ActivityType.Created,
                            Description = $"Teklif #{quote.QuoteNumber} ({quote.QuoteTitle}): {quote.GrandTotal:C2} [{quote.Status}]",
                            CreatedDate = quote.Date,
                            CreatedBy = "Teklif",
                            RelatedId = quote.Id,
                            RelatedType = "Quote"
                        });
                    }

                    Activities.Clear();
                    foreach (var activity in allActivities.OrderByDescending(a => a.CreatedDate).Take(100))
                    {
                        Activities.Add(activity);
                    }

                    // Hesaplamaları yap
                    CalculateTotals();
                });
            }
            catch (Exception ex)
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    MessageBox.Show($"Veri yükleme hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
            finally
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    _loadingService.Hide();
                });
            }
        }

        private void CalculateTotals()
        {
            // İşlem servisleri satış ve iadelerde bu alanı atomik olarak günceller.
            TotalSpent = _customer?.TotalSpent ?? 0m;

            // Pozitif bakiye = Müşteri Borçlu (Kırmızı)
            // Negatif bakiye = Müşteri Alacaklı (Yeşil)
            // Reconcile TotalSpent: sum of completed sales + completed service jobs
            decimal salesTotal = SalesOrders?.Where(s => s.Status != SalesOrderStatus.Cancelled).Sum(s => s.TotalAmount) ?? 0m;
            decimal jobsTotal = ServiceJobs?.Where(j => j.Status == JobStatus.Completed).Sum(j => j.TotalAmount) ?? 0m;
            decimal calculatedSpent = salesTotal + jobsTotal;
            if (calculatedSpent > 0) TotalSpent = calculatedSpent;

            // Reconcile Ledger: TotalDebt, TotalCredit, NetBalance
            TotalDebt = Transactions.Where(t => t.Type == TransactionType.Debt || t.Type == TransactionType.Refund).Sum(t => t.Amount);
            TotalCredit = Transactions.Where(t => t.Type == TransactionType.Payment || t.Type == TransactionType.CreditNote).Sum(t => t.Amount);
            NetBalance = TotalDebt - TotalCredit;
            TotalBalance = NetBalance;
        }

        [RelayCommand]
        private async Task AddPayment() => await AddTransactionAsync(TransactionType.Payment);

        [RelayCommand]
        private async Task AddDebt() => await AddTransactionAsync(TransactionType.Debt);

        private async Task AddTransactionAsync(TransactionType type)
        {
            var input = "0";
            if (string.IsNullOrWhiteSpace(input)) return;

            decimal amount = 0;
            string description = type == TransactionType.Payment ? "Tahsilat" : "Borç Yansıtma";

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
                var context = await _dbContextFactory.CreateDbContextAsync().ConfigureAwait(false);
                var transaction = new Transaction
                {
                    CustomerId = _customerId,
                    Type = type,
                    Amount = amount,
                    Date = DateTime.UtcNow,
                    Description = description
                };

                await using (context.ConfigureAwait(false))
                {
                    context.Transactions.Add(transaction);
                    await context.SaveChangesAsync().ConfigureAwait(false);
                }

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    Transactions.Insert(0, transaction);
                    CalculateTotals();
                });
            }
            catch (Exception ex)
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    MessageBox.Show($"İşlem eklenirken hata: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
        }

        private bool CanSaveCustomer()
        {
            return !string.IsNullOrWhiteSpace(FullName) &&
                   !string.IsNullOrWhiteSpace(_phoneNumber) &&
                   !string.IsNullOrWhiteSpace(_city);
        }

        [RelayCommand(CanExecute = nameof(CanSaveCustomer))]
        private async Task SaveCustomerAsync()
        {
            if (_customerId <= 0) return;

            try
            {
                using var context = await _dbContextFactory.CreateDbContextAsync();

                // Kanonik Telefon Normalizasyonu
                string normPhone = CustomerValidationHelper.NormalizePhoneNumber(_phoneNumber);
                if (!string.IsNullOrWhiteSpace(_phoneNumber) && !CustomerValidationHelper.IsValidPhoneNumber(_phoneNumber))
                {
                    _toastService.ShowError("Hata", "Geçerli bir telefon numarası giriniz.");
                    return;
                }

                string? cleanTc = CustomerType == CustomerType.Individual && !string.IsNullOrWhiteSpace(_tcKimlikNo) ? _tcKimlikNo.Trim() : null;
                string? cleanTax = CustomerType == CustomerType.Corporate && !string.IsNullOrWhiteSpace(_taxNumber) ? _taxNumber.Trim() : null;

                if (!string.IsNullOrEmpty(cleanTc) && !CustomerValidationHelper.IsValidTcKimlikNo(cleanTc))
                {
                    _toastService.ShowError("Hata", "Geçersiz T.C. Kimlik Numarası (11 haneli ve algoritmayı sağlamalıdır).");
                    return;
                }

                if (!string.IsNullOrEmpty(cleanTax) && !CustomerValidationHelper.IsValidTaxNumber(cleanTax))
                {
                    _toastService.ShowError("Hata", "Geçersiz Vergi Kimlik Numarası (10 haneli ve algoritmayı sağlamalıdır).");
                    return;
                }

                // Mükerrer Kontrolü (Başka müşteri ile çakışma olmamalı)
                string phoneLast10 = normPhone.Length >= 10 ? normPhone.Substring(normPhone.Length - 10) : normPhone;
                var duplicate = await context.Customers.AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id != _customerId && !c.IsDeleted && (
                        (!string.IsNullOrEmpty(normPhone) && (c.PhoneNumber == normPhone || (phoneLast10.Length >= 10 && c.PhoneNumber != null && c.PhoneNumber.Contains(phoneLast10)))) ||
                        (!string.IsNullOrEmpty(cleanTc) && c.TcKimlikNo == cleanTc) ||
                        (!string.IsNullOrEmpty(cleanTax) && c.TaxNumber == cleanTax)
                    ));

                if (duplicate != null)
                {
                    _toastService.ShowError("Mükerrer Kayıt", $"Bu telefon veya kimlik numarası başka bir müşteride ({duplicate.FullName} - {duplicate.CustomerCode}) kayıtlıdır!");
                    return;
                }
                var existingCustomer = await context.Customers.FindAsync(_customerId);

                if (existingCustomer != null)
                {
                    existingCustomer.FullName = FullName;
                    existingCustomer.PhoneNumber = normPhone;
                    _phoneNumber = normPhone;
                    existingCustomer.Email = _email;
                    existingCustomer.City = _city;
                    existingCustomer.District = _district;
                    existingCustomer.Neighborhood = _neighborhood;
                    existingCustomer.Street = _street;
                    existingCustomer.BuildingNo = _buildingNo;
                    existingCustomer.ApartmentNo = _apartmentNo;
                    existingCustomer.Notes = Notes;
                    existingCustomer.Type = CustomerType;
                    existingCustomer.TcKimlikNo = cleanTc;
                    existingCustomer.CompanyName = CompanyName;
                    existingCustomer.TaxNumber = cleanTax;
                    existingCustomer.TaxOffice = TaxOffice;

                    await context.SaveChangesAsync();

                    if (_customer != null)
                    {
                        _customer.FullName = FullName;
                        _customer.PhoneNumber = _phoneNumber;
                        _customer.Email = _email;
                        _customer.City = _city;
                        _customer.District = _district;
                        _customer.Neighborhood = _neighborhood;
                        _customer.Street = _street;
                        _customer.BuildingNo = _buildingNo;
                        _customer.ApartmentNo = _apartmentNo;
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

            serviceJobVm.SetInitialCustomer(_customer);

            var window = new NewServiceJobWindow(serviceJobVm)
            {
                Owner = Application.Current?.MainWindow,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            var result = window.ShowDialog();
            if (result == true)
            {
                await LoadCustomerDataAsync();
            }
        }

        [RelayCommand]
        private async Task AddNote()
        {
            var note = Notes;
            if (string.IsNullOrWhiteSpace(note))
            {
                MessageBox.Show("Lütfen bir not girin!", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var context = await _dbContextFactory.CreateDbContextAsync().ConfigureAwait(false);
                var activity = new CustomerActivity
                {
                    CustomerId = _customerId,
                    Type = ActivityType.NoteAdded,
                    Description = note,
                    CreatedDate = DateTime.UtcNow,
                    CreatedBy = "Kullanıcı"
                };

                await using (context.ConfigureAwait(false))
                {
                    context.CustomerActivities.Add(activity);

                    var customerInDb = await context.Customers.FindAsync(_customerId).ConfigureAwait(false);
                    if (customerInDb != null)
                    {
                        customerInDb.Notes = string.IsNullOrEmpty(customerInDb.Notes) 
                            ? note 
                            : customerInDb.Notes + "\n" + DateTime.UtcNow.ToString("dd.MM.yyyy") + ": " + note;
                        customerInDb.LastInteractionDate = DateTime.UtcNow;
                    }

                    await context.SaveChangesAsync().ConfigureAwait(false);
                }

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    Activities.Insert(0, activity);
                    Notes = string.Empty;
                    _toastService.ShowSuccess("Not başarıyla eklendi!");
                });
            }
            catch (Exception ex)
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    MessageBox.Show($"Not ekleme hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
        }

        [RelayCommand(CanExecute = nameof(CanSaveTags))]
        private async Task SaveTags()
        {
            if (_customerId <= 0) return;

            try
            {
                var context = await _dbContextFactory.CreateDbContextAsync().ConfigureAwait(false);
                var activity = new CustomerActivity
                {
                    CustomerId = _customerId,
                    Type = ActivityType.TagAdded,
                    Description = $"Etiketler güncellendi: {Tags}",
                    CreatedDate = DateTime.UtcNow,
                    CreatedBy = "Kullanıcı"
                };

                await using (context.ConfigureAwait(false))
                {
                    var customerInDb = await context.Customers.FindAsync(_customerId).ConfigureAwait(false);
                    if (customerInDb != null)
                    {
                        customerInDb.Tags = Tags;
                        customerInDb.LastInteractionDate = DateTime.UtcNow;
                    }

                    context.CustomerActivities.Add(activity);
                    await context.SaveChangesAsync().ConfigureAwait(false);
                }

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    Activities.Insert(0, activity);
                    _toastService.ShowSuccess("Etiketler kaydedildi!");
                });
            }
            catch (Exception ex)
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    MessageBox.Show($"Etiket kaydetme hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
        }

        [RelayCommand(CanExecute = nameof(CanSaveSegment))]
        private async Task SaveSegment()
        {
            if (_customerId <= 0) return;

            try
            {
                var context = await _dbContextFactory.CreateDbContextAsync().ConfigureAwait(false);
                await using (context.ConfigureAwait(false))
                {
                    var customerInDb = await context.Customers.FindAsync(_customerId).ConfigureAwait(false);
                    if (customerInDb != null)
                    {
                        customerInDb.Segment = Segment;
                        customerInDb.LastInteractionDate = DateTime.UtcNow;
                        await context.SaveChangesAsync().ConfigureAwait(false);
                    }
                }

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    _toastService.ShowSuccess($"Müşteri segmenti '{Segment}' olarak güncellendi!");
                });
            }
            catch (Exception ex)
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    MessageBox.Show($"Segment güncelleme hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
        }

        [RelayCommand]
        private async Task ExportAccountStatementPdfAsync()
        {
            if (_customer == null)
            {
                _toastService.ShowError("Hata", "Müşteri verisi bulunamadı.");
                return;
            }

            if (_pdfService == null)
            {
                _toastService.ShowError("Hata", "PDF servisi başlatılamadı.");
                return;
            }

            try
            {
                var cleanName = string.Join("_", _customer.FullName.Split(System.IO.Path.GetInvalidFileNameChars()));
                var saveDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "Cari Hesap Ekstresini Kaydet",
                    Filter = "PDF Dosyası (*.pdf)|*.pdf",
                    FileName = $"CariEkstre_{cleanName}_{DateTime.Now:yyyyMMdd}.pdf"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    await Task.Run(() =>
                    {
                        _pdfService.GenerateCustomerStatement(_customer, Transactions.ToList(), TotalBalance, saveDialog.FileName);
                    });

                    _toastService.ShowSuccess("Cari ekstre PDF olarak oluşturuldu.");

                    try
                    {
                        new System.Diagnostics.Process
                        {
                            StartInfo = new System.Diagnostics.ProcessStartInfo(saveDialog.FileName) { UseShellExecute = true }
                        }.Start();
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                _toastService.ShowError("PDF Hatası", $"Ekstre oluşturulurken hata: {ex.Message}");
            }
        }

        #endregion
    }
}
