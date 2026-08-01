using System;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using KamatekCrm.Infrastructure.Data;
using KamatekCrm.Services;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;
using KamatekCrm.Validation;

namespace KamatekCrm.ViewModels
{
    public partial class CustomerAddViewModel : ViewModelBase
    {
        
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

        [Required(ErrorMessage = "Ad soyad zorunludur.")]
        [StringLength(150, MinimumLength = 2, ErrorMessage = "Ad soyad 2-150 karakter olmalıdır.")]
        public string FullName
        {
            get => _fullName;
            set
            {
                if (SetProperty(ref _fullName, value))
                {
                    SaveCommand.NotifyCanExecuteChanged();
                }
            }
        }

        [Required(ErrorMessage = "Telefon numarası zorunludur.")]
        [RegularExpression(@"^\+?[0-9\s()\-]{10,20}$", ErrorMessage = "Geçerli bir telefon numarası girin.")]
        public string PhoneNumber
        {
            get => _phoneNumber;
            set
            {
                if (SetProperty(ref _phoneNumber, value))
                {
                    SaveCommand.NotifyCanExecuteChanged();
                }
            }
        }

        [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi girin.")]
        public string? Email
        {
            get => _email;
            set
            {
                if (SetProperty(ref _email, value))
                {
                    SaveCommand.NotifyCanExecuteChanged();
                }
            }
        }

        public string City
        {
            get => _city;
            set
            {
                if (SetProperty(ref _city, value))
                {
                    SaveCommand.NotifyCanExecuteChanged();
                }
            }
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
            set
            {
                if (SetProperty(ref _newCustomerType, value))
                {
                    ValidateProperty(NewTcKimlikNo, nameof(NewTcKimlikNo));
                    ValidateProperty(NewCompanyName, nameof(NewCompanyName));
                    ValidateProperty(NewTaxNumber, nameof(NewTaxNumber));
                    SaveCommand.NotifyCanExecuteChanged();
                }
            }
        }

        [RegularExpression(@"^\d{11}$", ErrorMessage = "T.C. Kimlik No 11 rakam olmalıdır.")]
        public string? NewTcKimlikNo
        {
            get => _newTcKimlikNo;
            set
            {
                if (SetProperty(ref _newTcKimlikNo, value))
                {
                    SaveCommand.NotifyCanExecuteChanged();
                }
            }
        }

        [RequiredWhen(nameof(NewCustomerType), CustomerType.Corporate, ErrorMessage = "Şirket tam ünvanı zorunludur.")]
        [StringLength(200, ErrorMessage = "Şirket ünvanı en fazla 200 karakter olabilir.")]
        public string? NewCompanyName
        {
            get => _newCompanyName;
            set
            {
                if (SetProperty(ref _newCompanyName, value))
                {
                    SaveCommand.NotifyCanExecuteChanged();
                }
            }
        }

        [RegularExpression(@"^\d{10}$", ErrorMessage = "Vergi numarası 10 rakam olmalıdır.")]
        public string? NewTaxNumber
        {
            get => _newTaxNumber;
            set
            {
                if (SetProperty(ref _newTaxNumber, value))
                {
                    SaveCommand.NotifyCanExecuteChanged();
                }
            }
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
                    SaveCommand.NotifyCanExecuteChanged();
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
            set
            {
                if (SetProperty(ref _errorMessage, value))
                {
                    OnPropertyChanged(nameof(HasError));
                }
            }
        }

        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    SaveCommand.NotifyCanExecuteChanged();
                }
            }
        }

        public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

        public IRelayCommand SaveCustomerCommand => SaveCommand;

        public event Action<bool>? RequestClose;

        private readonly Microsoft.EntityFrameworkCore.IDbContextFactory<AppDbContext> _dbContextFactory;

        public CustomerAddViewModel(Microsoft.EntityFrameworkCore.IDbContextFactory<AppDbContext> dbContextFactory)
        {
            _dbContextFactory = dbContextFactory;
            Cities = new ObservableCollection<City>();
            Districts = new ObservableCollection<District>();
            Neighborhoods = new ObservableCollection<Neighborhood>();

            ErrorsChanged += (_, _) => SaveCommand.NotifyCanExecuteChanged();

            LoadCities();
        }

        private bool CanSaveCustomer()
        {
            return !string.IsNullOrWhiteSpace(FullName) &&
                   !string.IsNullOrWhiteSpace(PhoneNumber) &&
                   (NewCustomerType != CustomerType.Corporate || !string.IsNullOrWhiteSpace(NewCompanyName)) &&
                   !HasErrors &&
                   !IsBusy;
        }

        [RelayCommand(CanExecute = nameof(CanSaveCustomer))]
        private async Task SaveAsync()
        {
            ValidateAllProperties();
            if (HasErrors || !CanSaveCustomer())
            {
                ErrorMessage = "Lütfen işaretlenen alanları kontrol edin.";
                return;
            }

            IsBusy = true;
            ErrorMessage = string.Empty;

            try
            {
                using var context = await _dbContextFactory.CreateDbContextAsync();
                string customerCode = GenerateCustomerCode(context);

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

                context.Customers.Add(customer);
                await context.SaveChangesAsync();

                RequestClose?.Invoke(true);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Kayıt hatası: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
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
            ErrorMessage = string.Empty;
            ClearErrors();
        }

        private void LoadCities()
        {
            Cities.Clear();
            var cities = AddressService.GetCities();
            foreach (var city in cities) Cities.Add(city);
        }

        private string GenerateCustomerCode(AppDbContext context)
        {
            int year = DateTime.UtcNow.Year;
            int customerCount = context.Customers
                .Count(c => c.CustomerCode.StartsWith($"MŞ-{year}-"));
            int nextNumber = customerCount + 1;
            return $"MŞ-{year}-{nextNumber:D4}";
        }

        [RelayCommand]
        private void Cancel()
        {
            RequestClose?.Invoke(false);
        }
    }
}
