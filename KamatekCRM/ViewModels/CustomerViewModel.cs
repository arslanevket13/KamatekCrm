using System;
using System.Collections.Generic; // List i�in gerekli
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Net; // UrlEncode için System.Net kullanlmalı
using System.Text; // UTF8 encoding için gerekli
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using KamatekCrm.Commands;
using KamatekCrm.Data;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;
using KamatekCrm.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KamatekCrm.ViewModels
{
    /// <summary>
    /// Mteri ynetimi ViewModel
    /// </summary>
    // DZELTME 1: Snf ad 'CustomersViewModel' yapld (Sonunda 's' var)
    public class CustomersViewModel : KamatekCrm.ViewModels.Common.PaginationViewModel
    {
        private readonly ApiClient _apiClient;
        private Customer? _selectedCustomer;
        private string _fullName = string.Empty;
        private string _phoneNumber = string.Empty;
        private string? _email;

        // Yap�sal adres alanlar�
        private string _city = string.Empty;
        private string? _district;
        private string? _neighborhood;
        private string? _street;
        private string? _buildingNo;
        private string? _apartmentNo;

        // Filtreleme i�in
        private string _searchText = string.Empty;
        private ICollectionView? _customersView;

        // Müşteri tipi filtreleme
        private CustomerType? _selectedTypeFilter;
        private int _totalCustomers;
        private int _individualCount;
        private int _corporateCount;
        private int _walkInCount;

        // Müşteri tipi ve ilgili alanlar (Yeni müşteri eklerken kullanılacak)
        private CustomerType _newCustomerType = CustomerType.Individual;
        private string? _newTcKimlikNo;
        private string? _newCompanyName;
        private string? _newTaxNumber;
        private string? _newTaxOffice;

        // Cascading Dropdown i�in
        private City? _selectedCity;
        private District? _selectedDistrict;
        private Neighborhood? _selectedNeighborhood;

        // D�ZELTME 2: Ba�lang�� de�eri bo� sayfa atandı
        private string _mapUrl = "about:blank";

        public ObservableCollection<Customer> Customers { get; set; }
        public ObservableCollection<City> Cities { get; set; }
        public ObservableCollection<District> Districts { get; set; }
        public ObservableCollection<Neighborhood> Neighborhoods { get; set; }

        public ICollectionView CustomersView => _customersView!;

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    // Arama metni değişince sayfa 1'e dön ve yenile
                    CurrentPage = 1; 
                }
            }
        }

        public CustomerType? SelectedTypeFilter
        {
            get => _selectedTypeFilter;
            set
            {
                if (SetProperty(ref _selectedTypeFilter, value))
                {
                    CurrentPage = 1;
                    _ = RefreshDataAsync();
                }
            }
        }

        public int TotalCustomers
        {
            get => _totalCustomers;
            set => SetProperty(ref _totalCustomers, value);
        }

        public int IndividualCount
        {
            get => _individualCount;
            set => SetProperty(ref _individualCount, value);
        }

        public int CorporateCount
        {
            get => _corporateCount;
            set => SetProperty(ref _corporateCount, value);
        }

        public int WalkInCount
        {
            get => _walkInCount;
            set => SetProperty(ref _walkInCount, value);
        }

        public Customer? SelectedCustomer
        {
            get => _selectedCustomer;
            set
            {
                if (SetProperty(ref _selectedCustomer, value) && value != null)
                {
                    // Se�ili m��teriyi forma y�kle
                    FullName = value.FullName;
                    PhoneNumber = value.PhoneNumber;
                    Email = value.Email;

                    // �ehir se�imini y�kle
                    if (!string.IsNullOrWhiteSpace(value.City))
                        SelectedCity = Cities.FirstOrDefault(c => c.Name == value.City);

                    // �l�e se�imini y�kle
                    if (!string.IsNullOrWhiteSpace(value.District) && SelectedCity != null)
                        SelectedDistrict = Districts.FirstOrDefault(d => d.Name == value.District);

                    // Mahalle se�imini y�kle
                    if (!string.IsNullOrWhiteSpace(value.Neighborhood) && SelectedDistrict != null)
                        SelectedNeighborhood = Neighborhoods.FirstOrDefault(n => n.Name == value.Neighborhood);

                    Street = value.Street;
                    BuildingNo = value.BuildingNo;
                    ApartmentNo = value.ApartmentNo;

                    // Haritay� g�ncelle (Mevcut m��teri y�klendi�inde)
                    UpdateMapUrl();
                }
            }
        }

        public string FullName { get => _fullName; set => SetProperty(ref _fullName, value); }
        public string PhoneNumber { get => _phoneNumber; set => SetProperty(ref _phoneNumber, value); }
        public string? Email { get => _email; set => SetProperty(ref _email, value); }
        public string City { get => _city; set => SetProperty(ref _city, value); }
        public string? District { get => _district; set => SetProperty(ref _district, value); }
        public string? Neighborhood { get => _neighborhood; set => SetProperty(ref _neighborhood, value); }
        public string? Street { get => _street; set => SetProperty(ref _street, value); }
        public string? BuildingNo { get => _buildingNo; set => SetProperty(ref _buildingNo, value); }

        public string? ApartmentNo
        {
            get => _apartmentNo;
            set
            {
                if (SetProperty(ref _apartmentNo, value))
                    UpdateMapUrl();
            }
        }

        public City? SelectedCity
        {
            get => _selectedCity;
            set
            {
                if (SetProperty(ref _selectedCity, value))
                {
                    Districts.Clear();
                    Neighborhoods.Clear();
                    SelectedDistrict = null;
                    SelectedNeighborhood = null;

                    if (value != null)
                    {
                        City = value.Name;
                        var districts = AddressService.GetDistricts(value.Name);
                        foreach (var d in districts) Districts.Add(d);
                    }
                    else City = string.Empty;

                    UpdateMapUrl();
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
                    Neighborhoods.Clear();
                    SelectedNeighborhood = null;

                    if (value != null && SelectedCity != null)
                    {
                        District = value.Name;
                        var neighborhoods = AddressService.GetNeighborhoods(SelectedCity.Name, value.Name);
                        foreach (var n in neighborhoods) Neighborhoods.Add(n);
                    }
                    else District = null;

                    UpdateMapUrl();
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
                    if (value != null) Neighborhood = value.Name;
                    else Neighborhood = null;

                    UpdateMapUrl();
                }
            }
        }

        public string MapUrl
        {
            get => _mapUrl;
            set
            {
                // WebView2 null değer kabul etmez, güvenli varsayılan değer kullan
                var safeValue = string.IsNullOrWhiteSpace(value) ? "about:blank" : value;
                SetProperty(ref _mapUrl, safeValue);
            }
        }

        public CustomerType NewCustomerType
        {
            get => _newCustomerType;
            set => SetProperty(ref _newCustomerType, value);
        }

        public string? NewTcKimlikNo
        {
            get => _newTcKimlikNo;
            set => SetProperty(ref _newTcKimlikNo, value);
        }

        public string? NewCompanyName
        {
            get => _newCompanyName;
            set => SetProperty(ref _newCompanyName, value);
        }

        public string? NewTaxNumber
        {
            get => _newTaxNumber;
            set => SetProperty(ref _newTaxNumber, value);
        }

        public string? NewTaxOffice
        {
            get => _newTaxOffice;
            set => SetProperty(ref _newTaxOffice, value);
        }

        public ICommand SaveCustomerCommand { get; }
        public ICommand DeleteCustomerCommand { get; }
        public ICommand ClearFormCommand { get; }
        public ICommand ViewProfileCommand { get; }
        public ICommand ClearTypeFilterCommand { get; }
        public ICommand OpenAddCustomerWindowCommand { get; }
        public ICommand OpenCustomerProfileCommand { get; }

        private void ClearTypeFilter()
        {
            SelectedTypeFilter = null;
        }


        private readonly NavigationService _navigationService;
        private readonly ILogger<CustomersViewModel> _logger;
        private readonly IToastService _toastService;
        private readonly ILoadingService _loadingService;

        public CustomersViewModel(NavigationService navigationService, ILogger<CustomersViewModel> logger, IToastService toastService, ILoadingService loadingService, ApiClient apiClient)
        {
            _navigationService = navigationService;
            _logger = logger;
            _toastService = toastService;
            _loadingService = loadingService;
            _apiClient = apiClient;
            Customers = new ObservableCollection<Customer>();
            Cities = new ObservableCollection<City>();
            Districts = new ObservableCollection<District>();
            Neighborhoods = new ObservableCollection<Neighborhood>();

            _customersView = CollectionViewSource.GetDefaultView(Customers);
            // _customersView.Filter = FilterCustomers; // DB Paging kullandığımız için kaldırıldı

            SaveCustomerCommand = new RelayCommand(_ => SaveCustomer(), _ => CanSaveCustomer());
            DeleteCustomerCommand = new RelayCommand(_ => DeleteCustomer(), _ => SelectedCustomer != null);
            ClearFormCommand = new RelayCommand(_ => ClearForm());
            ViewProfileCommand = new RelayCommand(_ => ViewProfile(), _ => SelectedCustomer != null);
            ClearTypeFilterCommand = new RelayCommand(_ => ClearTypeFilter());
            OpenAddCustomerWindowCommand = new RelayCommand(_ => OpenAddCustomerWindow());
            OpenCustomerProfileCommand = new RelayCommand(_ => OpenCustomerProfile(), _ => SelectedCustomer != null);

            _logger.LogInformation("CustomersViewModel initialized");

            LoadCities();
            // LoadCustomers(); replaced by RefreshDataAsync
            _ = RefreshDataAsync();
        }

        protected override async Task RefreshDataAsync()
        {
            try
            {
                _loadingService.Show("Müşteriler yükleniyor...");

                // İstatistikleri yükle
                var statsResponse = await _apiClient.GetAsync<dynamic>("api/customers/stats");
                if (statsResponse != null && statsResponse.Success && statsResponse.Data != null)
                {
                    try
                    {
                        if (statsResponse.Data is System.Text.Json.JsonElement json)
                        {
                            TotalCustomers = json.GetProperty("TotalCustomers").GetInt32();
                            IndividualCount = json.GetProperty("IndividualCount").GetInt32();
                            CorporateCount = json.GetProperty("CorporateCount").GetInt32();
                            WalkInCount = json.GetProperty("WalkInCount").GetInt32();
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not parse customer stats");
                    }
                }

                // Listeyi yükle
                var url = $"api/customers?page={CurrentPage}&pageSize={PageSize}";
                if (SelectedTypeFilter.HasValue) 
                    url += $"&type={(int)SelectedTypeFilter.Value}";
                if (!string.IsNullOrWhiteSpace(SearchText)) 
                    url += $"&search={Uri.EscapeDataString(SearchText)}";
                
                var customersResponse = await _apiClient.GetAsync<List<Customer>>(url);
                
                if (customersResponse != null && customersResponse.Success)
                {
                    TotalCount = customersResponse.Meta?.Pagination?.TotalCount ?? 0;
                    Customers.Clear();
                    if (customersResponse.Data != null)
                    {
                        foreach (var item in customersResponse.Data) Customers.Add(item);
                    }
                    _toastService.ShowSuccess("Müşteri verileri güncellendi.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing customer data");
                _toastService.ShowError($"Veriler yenilenirken hata oluştu: {ex.Message}");
            }
            finally
            {
                _loadingService.Hide();
            }
        }

        // Eski metodların yerine RefreshDataAsync kullanılıyor
        // private bool FilterCustomers... (Kaldırıldı)
        // private void LoadCustomers... (Kaldırıldı)

        private bool CanSaveCustomer()
        {
            return !string.IsNullOrWhiteSpace(FullName) &&
                   !string.IsNullOrWhiteSpace(PhoneNumber) &&
                   !string.IsNullOrWhiteSpace(City);
        }

        private async void SaveCustomer()
        {
            try
            {
                _loadingService.Show("Müşteri kaydediliyor...");

                if (SelectedCustomer != null)
                {
                    SelectedCustomer.FullName = FullName;
                    SelectedCustomer.PhoneNumber = PhoneNumber;
                    SelectedCustomer.Email = Email;
                    SelectedCustomer.City = City;
                    SelectedCustomer.District = District;
                    SelectedCustomer.Neighborhood = Neighborhood;
                    SelectedCustomer.Street = Street;
                    SelectedCustomer.BuildingNo = BuildingNo;
                    SelectedCustomer.ApartmentNo = ApartmentNo;

                    var response = await _apiClient.PutAsync<Customer>($"api/customers/{SelectedCustomer.Id}", SelectedCustomer);
                    if (!response.Success) throw new Exception(response.Message);
                }
                else
                {
                    var newCustomer = new Customer
                    {
                        Type = NewCustomerType,
                        FullName = FullName,
                        PhoneNumber = PhoneNumber,
                        Email = Email,
                        City = City,
                        District = District,
                        Neighborhood = Neighborhood,
                        Street = Street,
                        BuildingNo = BuildingNo,
                        ApartmentNo = ApartmentNo,
                        
                        // Bireysel müşteri için
                        TcKimlikNo = NewCustomerType == CustomerType.Individual ? NewTcKimlikNo : null,
                        
                        // Kurumsal müşteri için
                        CompanyName = NewCustomerType == CustomerType.Corporate ? NewCompanyName : null,
                        TaxNumber = NewCustomerType == CustomerType.Corporate ? NewTaxNumber : null,
                        TaxOffice = NewCustomerType == CustomerType.Corporate ? NewTaxOffice : null
                    };

                    var response = await _apiClient.PostAsync<Customer>("api/customers", newCustomer);
                    if (!response.Success) throw new Exception(response.Message);
                }

                _ = RefreshDataAsync();
                ClearForm();
                _toastService.ShowSuccess("Müşteri başarıyla kaydedildi!");
            }
            catch (Exception ex)
            {
                _toastService.ShowError($"Hata: {ex.Message}");
            }
            finally
            {
                _loadingService.Hide();
            }
        }

        private async void DeleteCustomer()
        {
            if (SelectedCustomer == null) return;

            var result = MessageBox.Show($"{SelectedCustomer.FullName} adlı müşteriyi silmek istediğinize emin misiniz?",
                "Onay", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    _loadingService.Show("Müşteri siliniyor...");
                    var response = await _apiClient.DeleteAsync<object>($"api/customers/{SelectedCustomer.Id}");
                    if (!response.Success) throw new Exception(response.Message);

                    _ = RefreshDataAsync();
                    ClearForm();
                    _toastService.ShowSuccess("Müşteri başarıyla silindi!");
                }
                catch (Exception ex)
                {
                    _toastService.ShowError($"Hata: {ex.Message}");
                }
                finally
                {
                    _loadingService.Hide();
                }
            }
        }

        private void ClearForm()
        {
            SelectedCustomer = null;
            FullName = string.Empty;
            PhoneNumber = string.Empty;
            Email = null;

            // Dropdownları temizlerken tetiklemeyi önlemek için
            SelectedCity = null;

            City = string.Empty;
            District = null;
            Neighborhood = null;
            Street = null;
            BuildingNo = null;
            ApartmentNo = null;
            MapUrl = "about:blank";
            
            // Yeni müşteri tipi alanlarını temizle
            NewCustomerType = CustomerType.Individual;
            NewTcKimlikNo = null;
            NewCompanyName = null;
            NewTaxNumber = null;
            NewTaxOffice = null;
        }

        private void LoadCities()
        {
            Cities.Clear();
            var cities = AddressService.GetCities();
            foreach (var city in cities) Cities.Add(city);
        }

        private void UpdateMapUrl()
        {
            var addressParts = new List<string>();

            // Adres parçalarını topla
            if (!string.IsNullOrWhiteSpace(Street)) addressParts.Add(Street);
            if (!string.IsNullOrWhiteSpace(Neighborhood)) addressParts.Add(Neighborhood);
            if (!string.IsNullOrWhiteSpace(District)) addressParts.Add(District);
            if (!string.IsNullOrWhiteSpace(City)) addressParts.Add(City);

            if (addressParts.Count > 0)
            {
                // 1. Adresi tek bir metin haline getir
                string fullAddress = string.Join(" ", addressParts);

                // 2. URL için güvenli hale getir (Türkçe karakterleri vb. kodlar)
                string query = WebUtility.UrlEncode(fullAddress);

                // 3. Google Maps Embed URL'ini oluştur
                string embedUrl = $"https://www.google.com/maps?q={query}&output=embed&hl=tr&z=15";

                // 4. iframe içeren HTML oluştur
                string htmlContent = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body, html {{
            margin: 0;
            padding: 0;
            width: 100%;
            height: 100%;
            overflow: hidden;
        }}
        iframe {{
            width: 100%;
            height: 100%;
            border: none;
        }}
    </style>
</head>
<body>
    <iframe src=""{embedUrl}"" allowfullscreen></iframe>
</body>
</html>";

                // 5. HTML'i Base64'e çevir
                byte[] htmlBytes = Encoding.UTF8.GetBytes(htmlContent);
                string base64Html = Convert.ToBase64String(htmlBytes);

                // 6. Data URI formatında MapUrl'e ata
                MapUrl = $"data:text/html;base64,{base64Html}";
            }
            else
            {
                // Adres yoksa boş sayfa göster
                MapUrl = "about:blank";
            }
        }

        private void ViewProfile()
        {
            if (SelectedCustomer == null) return;
            OpenCustomerProfile();
        }

        private void OpenAddCustomerWindow()
        {
            var window = new Views.CustomerAddWindow();
            window.Owner = System.Windows.Application.Current.MainWindow;
            
            var vm = (ViewModels.CustomerAddViewModel)window.DataContext;
            vm.RequestClose += success =>
            {
                if (success)
                {
                    _ = RefreshDataAsync();
                }
            };
            
            window.ShowDialog();
        }

        private void OpenCustomerProfile()
        {
            if (SelectedCustomer == null) return;

            var window = new Views.CustomerDetailWindow();
            window.Owner = System.Windows.Application.Current.MainWindow;
            
            if (window.DetailView.DataContext is ViewModels.CustomerDetailViewModel detailVm)
            {
                detailVm.Initialize(SelectedCustomer.Id);
            }
            
            window.ShowDialog();
            
            _ = RefreshDataAsync();
        }


    }
}
