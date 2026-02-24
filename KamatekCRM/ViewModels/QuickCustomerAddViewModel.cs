using System;
using System.Windows;
using System.Windows.Input;
using KamatekCrm.Commands;
using KamatekCrm.Data;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;

namespace KamatekCrm.ViewModels
{
    /// <summary>
    /// POS ekranından hızlı yeni müşteri kaydı için ViewModel.
    /// Başarılı kayıt sonucu SavedCustomer set edilir ve pencere kapanır.
    /// </summary>
    public class QuickCustomerAddViewModel : ViewModelBase
    {
        private readonly AppDbContext _context;
        private string _fullName = string.Empty;
        private string _phone = string.Empty;
        private string _email = string.Empty;
        private string _idNumber = string.Empty;
        private string _errorMessage = string.Empty;
        private bool _isBusy;

        #region Properties

        public string FullName
        {
            get => _fullName;
            set
            {
                SetProperty(ref _fullName, value);
                System.Windows.Input.CommandManager.InvalidateRequerySuggested();
            }
        }

        public string Phone
        {
            get => _phone;
            set => SetProperty(ref _phone, value);
        }

        public string Email
        {
            get => _email;
            set => SetProperty(ref _email, value);
        }

        public string IdNumber
        {
            get => _idNumber;
            set => SetProperty(ref _idNumber, value);
        }

        public CustomerType CustomerType { get; set; } = CustomerType.WalkIn;

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

        /// <summary>
        /// Başarılı kayıt sonrası oluşturulan müşteri — çağıran ViewModel bunu okur.
        /// </summary>
        public Customer? SavedCustomer { get; private set; }

        #endregion

        #region Commands

        public ICommand SaveCustomerCommand { get; }
        public ICommand CancelCommand { get; }

        #endregion

        public QuickCustomerAddViewModel()
        {
            _context = new AppDbContext();

            SaveCustomerCommand = new RelayCommand(
                _ => ExecuteSaveCustomer(),
                _ => !string.IsNullOrWhiteSpace(FullName) && !IsBusy);

            CancelCommand = new RelayCommand(_ => RequestClose?.Invoke(false));
        }

        /// <summary>
        /// Pencere kapatma isteği — View kodu bu event'i dinler.
        /// </summary>
        public event Action<bool>? RequestClose;

        private void ExecuteSaveCustomer()
        {
            if (string.IsNullOrWhiteSpace(FullName))
            {
                ErrorMessage = "Ad Soyad zorunludur.";
                OnPropertyChanged(nameof(HasError));
                return;
            }

            IsBusy = true;
            ErrorMessage = string.Empty;
            OnPropertyChanged(nameof(HasError));

            try
            {
                var customer = new Customer
                {
                    FullName = FullName.Trim(),
                    PhoneNumber = Phone.Trim(),
                    Email = Email.Trim(),
                    Type = CustomerType,
                    Notes = string.IsNullOrEmpty(IdNumber) ? string.Empty : $"TC: {IdNumber.Trim()}",
                    CreatedDate = DateTime.UtcNow,
                    CreatedBy = "POS-QuickAdd"
                };

                _context.Customers.Add(customer);
                _context.SaveChanges();

                SavedCustomer = customer;
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
    }
}
