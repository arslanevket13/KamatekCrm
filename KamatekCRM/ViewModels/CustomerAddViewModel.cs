using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using KamatekCrm.Commands;
using KamatekCrm.Data;
using KamatekCrm.Services;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;

namespace KamatekCrm.ViewModels
{
    public class CustomerAddViewModel : ViewModelBase
    {
        private readonly AppDbContext _context;
        
        private string _fullName = string.Empty;
        private string _phoneNumber = string.Empty;
        private string? _email;
        private string _city = string.Empty;
        private string? _district;
        private string? _neighborhood;
        private string? _street;
        private string? _buildingNo;
        private string? _apartmentNo;
        private DateTime? _birthDate;
        
        private CustomerType _newCustomerType = CustomerType.Individual;
        private string? _newTcKimlikNo;
        private string? _newCompanyName;
        private string? _newTaxNumber;
        private string? _newTaxOffice;
        
        private City? _selectedCity;
        private District? _selectedDistrict;
        private Neighborhood? _selectedNeighborhood;
        
        private string _errorMessage = string.Empty;
        private bool _isBusy;

        public ObservableCollection<City> Cities { get; set; }
        public ObservableCollection<District> Districts { get; set; }
        public ObservableCollection<Neighborhood> Neighborhoods { get; set; }

        public string FullName
        {
            get => _fullName;
            set
            {
                SetProperty(ref _fullName, value);
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public string PhoneNumber
        {
            get => _phoneNumber;
            set
            {
                SetProperty(ref _phoneNumber, value);
                CommandManager.InvalidateRequerySuggested();
            }
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

        public DateTime? BirthDate
        {
            get => _birthDate;
            set => SetProperty(ref _birthDate, value);
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
                    else
                    {
                        City = string.Empty;
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
                    Neighborhoods.Clear();
                    SelectedNeighborhood = null;

                    if (value != null && SelectedCity != null)
                    {
                        District = value.Name;
                        var neighborhoods = AddressService.GetNeighborhoods(SelectedCity.Name, value.Name);
                        foreach (var n in neighborhoods) Neighborhoods.Add(n);
                    }
                    else
                    {
                        District = null;
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
                    Neighborhood = value?.Name;
                }
            }
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

        public ICommand SaveCustomerCommand { get; }
        public ICommand ClearFormCommand { get; }
        public ICommand CancelCommand { get; }

        public event Action<bool>? RequestClose;

        public CustomerAddViewModel()
        {
            _context = new AppDbContext();
            Cities = new ObservableCollection<City>();
            Districts = new ObservableCollection<District>();
            Neighborhoods = new ObservableCollection<Neighborhood>();

            SaveCustomerCommand = new RelayCommand(
                _ => ExecuteSaveCustomer(),
                _ => CanSaveCustomer() && !IsBusy);

            ClearFormCommand = new RelayCommand(_ => ClearForm());
            CancelCommand = new RelayCommand(_ => RequestClose?.Invoke(false));

            LoadCities();
        }

        private bool CanSaveCustomer()
        {
            return !string.IsNullOrWhiteSpace(FullName) &&
                   !string.IsNullOrWhiteSpace(PhoneNumber) &&
                   !string.IsNullOrWhiteSpace(City);
        }

        private void ExecuteSaveCustomer()
        {
            if (!CanSaveCustomer())
            {
                ErrorMessage = "Lütfen zorunlu alanları doldurun (Ad Soyad, Telefon, Şehir).";
                OnPropertyChanged(nameof(HasError));
                return;
            }

            IsBusy = true;
            ErrorMessage = string.Empty;
            OnPropertyChanged(nameof(HasError));

            try
            {
                string customerCode = GenerateCustomerCode();

                var customer = new Customer
                {
                    CustomerCode = customerCode,
                    Type = NewCustomerType,
                    FullName = FullName.Trim(),
                    PhoneNumber = PhoneNumber.Trim(),
                    Email = string.IsNullOrWhiteSpace(Email) ? null : Email.Trim(),
                    City = City,
                    District = District,
                    Neighborhood = Neighborhood,
                    Street = Street,
                    BuildingNo = BuildingNo,
                    ApartmentNo = ApartmentNo,
                    BirthDate = BirthDate,
                    CreatedDate = DateTime.UtcNow,

                    TcKimlikNo = NewCustomerType == CustomerType.Individual ? NewTcKimlikNo : null,
                    CompanyName = NewCustomerType == CustomerType.Corporate ? NewCompanyName : null,
                    TaxNumber = NewCustomerType == CustomerType.Corporate ? NewTaxNumber : null,
                    TaxOffice = NewCustomerType == CustomerType.Corporate ? NewTaxOffice : null
                };

                _context.Customers.Add(customer);
                _context.SaveChanges();

                RequestClose?.Invoke(true);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Kayıt hatası: {ex.Message}";
                OnPropertyChanged(nameof(HasError));
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void ClearForm()
        {
            FullName = string.Empty;
            PhoneNumber = string.Empty;
            Email = null;
            SelectedCity = null;
            City = string.Empty;
            District = null;
            Neighborhood = null;
            Street = null;
            BuildingNo = null;
            ApartmentNo = null;
            BirthDate = null;

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

        private string GenerateCustomerCode()
        {
            int year = DateTime.Now.Year;
            int customerCount = _context.Customers
                .Count(c => c.CustomerCode.StartsWith($"MŞ-{year}-"));
            int nextNumber = customerCount + 1;
            return $"MŞ-{year}-{nextNumber:D4}";
        }
    }
}
