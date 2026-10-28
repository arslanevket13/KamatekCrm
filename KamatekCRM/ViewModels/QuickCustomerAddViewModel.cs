using System;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using KamatekCrm.Infrastructure.Data;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Helpers;
using KamatekCrm.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace KamatekCrm.ViewModels
{
    /// <summary>
    /// POS ekranından hızlı yeni müşteri kaydı için ViewModel.
    /// Başarılı kayıt sonucu SavedCustomer set edilir ve pencere kapanır.
    /// </summary>
    public partial class QuickCustomerAddViewModel : ViewModelBase
    {
        private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
        private string _fullName = string.Empty;
        private string _phone = string.Empty;
        private string _email = string.Empty;
        private string _idNumber = string.Empty;
        private string _companyName = string.Empty;
        private string _taxNumber = string.Empty;
        private string _taxOffice = string.Empty;
        private bool _isCorporate;
        private string _duplicatePhoneWarning = string.Empty;
        private string _errorMessage = string.Empty;
        private bool _isBusy;

        #region Properties

        public string FullName
        {
            get => _fullName;
            set
            {
                if (SetProperty(ref _fullName, value))
                {
                    ErrorMessage = string.Empty;
                    SaveCustomerCommand.NotifyCanExecuteChanged();
                }
            }
        }

        public string Phone
        {
            get => _phone;
            set
            {
                if (SetProperty(ref _phone, value))
                {
                    _ = CheckDuplicatePhoneAsync(value);
                }
            }
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

        public string CompanyName
        {
            get => _companyName;
            set => SetProperty(ref _companyName, value);
        }

        public string TaxNumber
        {
            get => _taxNumber;
            set => SetProperty(ref _taxNumber, value);
        }

        public string TaxOffice
        {
            get => _taxOffice;
            set => SetProperty(ref _taxOffice, value);
        }

        public bool IsCorporate
        {
            get => _isCorporate;
            set
            {
                if (SetProperty(ref _isCorporate, value))
                {
                    OnPropertyChanged(nameof(IsIndividual));
                    CustomerType = value ? CustomerType.Corporate : CustomerType.Individual;
                }
            }
        }

        public bool IsIndividual
        {
            get => !_isCorporate;
            set => IsCorporate = !value;
        }

        public CustomerType CustomerType { get; set; } = CustomerType.Individual;

        public string DuplicatePhoneWarning
        {
            get => _duplicatePhoneWarning;
            set
            {
                if (SetProperty(ref _duplicatePhoneWarning, value))
                    OnPropertyChanged(nameof(HasDuplicatePhoneWarning));
            }
        }

        public bool HasDuplicatePhoneWarning => !string.IsNullOrEmpty(DuplicatePhoneWarning);

        public string ErrorMessage
        {
            get => _errorMessage;
            set
            {
                if (SetProperty(ref _errorMessage, value))
                    OnPropertyChanged(nameof(HasError));
            }
        }

        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (SetProperty(ref _isBusy, value))
                    SaveCustomerCommand.NotifyCanExecuteChanged();
            }
        }

        public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

        public Customer? SavedCustomer { get; private set; }

        private Customer? _existingCustomer;
        public Customer? ExistingCustomer
        {
            get => _existingCustomer;
            set
            {
                if (SetProperty(ref _existingCustomer, value))
                {
                    OnPropertyChanged(nameof(HasExistingCustomer));
                    SelectExistingCustomerCommand.NotifyCanExecuteChanged();
                }
            }
        }

        public bool HasExistingCustomer => ExistingCustomer != null;

        [RelayCommand]
        private void SelectExistingCustomer()
        {
            if (ExistingCustomer != null)
            {
                SavedCustomer = ExistingCustomer;
                RequestClose?.Invoke(true);
            }
        }

        #endregion

        public QuickCustomerAddViewModel(IDbContextFactory<AppDbContext> dbContextFactory)
        {
            _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
        }

        /// <summary>
        /// Pencere kapatma isteği — View kodu bu event'i dinler.
        /// </summary>
        public event Action<bool>? RequestClose;

        private bool CanSaveCustomer() => !string.IsNullOrWhiteSpace(FullName) && !IsBusy;

        private async System.Threading.Tasks.Task CheckDuplicatePhoneAsync(string? phone)
        {
            if (string.IsNullOrWhiteSpace(phone) || phone.Trim().Length < 5)
            {
                DuplicatePhoneWarning = string.Empty;
                ExistingCustomer = null;
                return;
            }

            try
            {
                string norm = CustomerValidationHelper.NormalizePhoneNumber(phone);
                string phoneLast10 = norm.Length >= 10 ? norm.Substring(norm.Length - 10) : norm;
                using var context = await _dbContextFactory.CreateDbContextAsync();
                var existing = await context.Customers.AsNoTracking()
                    .FirstOrDefaultAsync(c => !c.IsDeleted && c.PhoneNumber != null && 
                        (c.PhoneNumber == norm || (phoneLast10.Length >= 10 && c.PhoneNumber.Contains(phoneLast10))));

                Application.Current?.Dispatcher.Invoke(() =>
                {
                    if (existing != null)
                    {
                        ExistingCustomer = existing;
                        DuplicatePhoneWarning = $"⚠️ Bu telefon ({phone.Trim()}) ile kayıtlı müşteri bulundu: {existing.FullName} ({existing.CustomerCode})";
                    }
                    else
                    {
                        ExistingCustomer = null;
                        DuplicatePhoneWarning = string.Empty;
                    }
                });
            }
            catch
            {
                DuplicatePhoneWarning = string.Empty;
            }
        }

        [RelayCommand]
        private void Cancel() => RequestClose?.Invoke(false);

        [RelayCommand(CanExecute = nameof(CanSaveCustomer))]
        private async System.Threading.Tasks.Task SaveCustomerAsync()
        {
            if (string.IsNullOrWhiteSpace(FullName))
            {
                ErrorMessage = "Ad Soyad zorunludur.";
                return;
            }

            // Telefon Normalizasyonu ve Format Kontrolü
            string normPhone = CustomerValidationHelper.NormalizePhoneNumber(Phone);
            if (!string.IsNullOrWhiteSpace(Phone) && !CustomerValidationHelper.IsValidPhoneNumber(Phone))
            {
                ErrorMessage = "Geçerli bir telefon numarası giriniz (Örn: 05XX XXX XX XX).";
                return;
            }

            // TCKN / VKN Algoritma Doğrulaması
            string? cleanId = !string.IsNullOrWhiteSpace(IdNumber) ? IdNumber.Trim() : null;
            string? cleanTax = !string.IsNullOrWhiteSpace(TaxNumber) ? TaxNumber.Trim() : null;

            if (!string.IsNullOrEmpty(cleanId))
            {
                if (IsCorporate)
                {
                    if (!CustomerValidationHelper.IsValidTaxNumber(cleanId))
                    {
                        ErrorMessage = "Geçersiz Vergi Kimlik Numarası (VKN 10 haneli olmalı ve resmi algoritmayı sağlamalıdır).";
                        return;
                    }
                }
                else
                {
                    if (!CustomerValidationHelper.IsValidTcKimlikNo(cleanId))
                    {
                        ErrorMessage = "Geçersiz T.C. Kimlik Numarası (11 haneli olmalı ve resmi algoritmayı sağlamalıdır).";
                        return;
                    }
                }
            }

            if (IsCorporate && !string.IsNullOrEmpty(cleanTax))
            {
                if (!CustomerValidationHelper.IsValidTaxNumber(cleanTax))
                {
                    ErrorMessage = "Geçersiz Vergi Kimlik Numarası (VKN 10 haneli olmalı ve resmi algoritmayı sağlamalıdır).";
                    return;
                }
            }

            IsBusy = true;
            ErrorMessage = string.Empty;

            try
            {
                using var context = await _dbContextFactory.CreateDbContextAsync();

                // Split-Brain Ekleme ve Mükerrer Kayıt Engeli (Duplicate Shield)
                string phoneLast10 = normPhone.Length >= 10 ? normPhone.Substring(normPhone.Length - 10) : normPhone;
                var duplicate = await context.Customers.AsNoTracking()
                    .FirstOrDefaultAsync(c => !c.IsDeleted && (
                        (!string.IsNullOrEmpty(normPhone) && (c.PhoneNumber == normPhone || (phoneLast10.Length >= 10 && c.PhoneNumber != null && c.PhoneNumber.Contains(phoneLast10)))) ||
                        (!string.IsNullOrEmpty(cleanId) && (c.TcKimlikNo == cleanId || c.TaxNumber == cleanId)) ||
                        (!string.IsNullOrEmpty(cleanTax) && c.TaxNumber == cleanTax)
                    ));

                if (duplicate != null)
                {
                    ExistingCustomer = duplicate;
                    ErrorMessage = $"Mükerrer kart açılamaz! Sistemde kayıtlı: {duplicate.FullName} ({duplicate.CustomerCode})";
                    DuplicatePhoneWarning = $"⚠️ Bu müşteri ({duplicate.FullName}) zaten mevcut. 'Mevcut Müşteriyi Seç' butonunu kullanabilirsiniz.";
                    return;
                }

                // Benzersiz Müşteri Kodu Üret
                string timestamp = DateTime.UtcNow.ToString("yyMMddHHmmss");
                string customerCode = $"MÜŞ-{timestamp}";

                var customer = new Customer
                {
                    CustomerCode = customerCode,
                    FullName = FullName.Trim(),
                    PhoneNumber = normPhone,
                    Email = Email?.Trim(),
                    Type = CustomerType,
                    TcKimlikNo = !string.IsNullOrWhiteSpace(cleanId) && !IsCorporate ? cleanId : null,
                    TaxNumber = IsCorporate ? (cleanTax ?? cleanId) : null,
                    TaxOffice = !string.IsNullOrWhiteSpace(TaxOffice) && IsCorporate ? TaxOffice.Trim() : null,
                    CompanyName = !string.IsNullOrWhiteSpace(CompanyName) && IsCorporate ? CompanyName.Trim() : null,
                    Notes = IsCorporate ? $"Kurumsal Kayıt. Yetkili: {FullName.Trim()}" : (string.IsNullOrWhiteSpace(cleanId) ? string.Empty : $"TC: {cleanId}"),
                    CreatedDate = DateTime.UtcNow,
                    CreatedBy = App.CurrentUser?.Username ?? "POS-QuickAdd"
                };

                context.Customers.Add(customer);
                await context.SaveChangesAsync();

                SavedCustomer = customer;
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
    }
}
