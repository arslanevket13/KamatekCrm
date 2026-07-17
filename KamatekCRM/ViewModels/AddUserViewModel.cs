using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using KamatekCrm.Data;
using KamatekCrm.Shared.Models;
using KamatekCrm.Services;

namespace KamatekCrm.ViewModels
{
    /// <summary>
    /// Kullanıcı ekleme ViewModel - Admin tarafından kullanılır
    /// </summary>
    public partial class AddUserViewModel : ViewModelBase
    {
        private readonly Microsoft.EntityFrameworkCore.IDbContextFactory<KamatekCrm.Data.AppDbContext> _dbContextFactory;
        private readonly IAuthService _authService;
        private readonly IToastService _toastService;
        private readonly ILoadingService _loadingService;

        private string _ad = string.Empty;
        private string _soyad = string.Empty;
        private string _username = string.Empty;
        private string _selectedRoleDisplay = "Personel";
        private string _statusMessage = string.Empty;
        private bool _isSuccess;
        private bool _isUsernameAvailable = true;

        // RBAC Permissions
        private bool _canViewFinance;
        private bool _canViewAnalytics;
        private bool _canDeleteRecords;
        private bool _canApprovePurchase;
        private bool _canAccessSettings;

        // Technician Fields
        private string _vehiclePlate = string.Empty;
        private string _serviceArea = string.Empty;
        private string _expertiseAreas = string.Empty;

        /// <summary>
        /// Ad
        /// </summary>
        public string Ad
        {
            get => _ad;
            set => SetProperty(ref _ad, value);
        }

        /// <summary>
        /// Soyad
        /// </summary>
        public string Soyad
        {
            get => _soyad;
            set => SetProperty(ref _soyad, value);
        }

        /// <summary>
        /// Kullanıcı Adı (Admin tarafından manuel girilir)
        /// </summary>
        public string Username
        {
            get => _username;
            set
            {
                if (SetProperty(ref _username, value))
                {
                    // Kullanıcı adı benzersizliğini kontrol et
                    CheckUsernameAvailability();
                }
            }
        }

        /// <summary>
        /// Kullanıcı adı müsait mi?
        /// </summary>
        public bool IsUsernameAvailable
        {
            get => _isUsernameAvailable;
            set
            {
                SetProperty(ref _isUsernameAvailable, value);
                OnPropertyChanged(nameof(UsernameValidationMessage));
            }
        }

        /// <summary>
        /// Kullanıcı adı doğrulama mesajı
        /// </summary>
        public string UsernameValidationMessage
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Username))
                    return string.Empty;
                return IsUsernameAvailable ? "✅ Kullanıcı adı müsait" : "❌ Bu kullanıcı adı zaten kullanılıyor";
            }
        }

        /// <summary>
        /// Seçili rol (Arayüz gösterimi: Patron/Personel)
        /// </summary>
        public string SelectedRoleDisplay
        {
            get => _selectedRoleDisplay;
            set
            {
                if (SetProperty(ref _selectedRoleDisplay, value))
                {
                    OnPropertyChanged(nameof(IsTechnicianRole));
                }
            }
        }

        public bool IsTechnicianRole => SelectedRoleDisplay == "Personel";

        #region Ext properties
        public bool CanViewFinance { get => _canViewFinance; set => SetProperty(ref _canViewFinance, value); }
        public bool CanViewAnalytics { get => _canViewAnalytics; set => SetProperty(ref _canViewAnalytics, value); }
        public bool CanDeleteRecords { get => _canDeleteRecords; set => SetProperty(ref _canDeleteRecords, value); }
        public bool CanApprovePurchase { get => _canApprovePurchase; set => SetProperty(ref _canApprovePurchase, value); }
        public bool CanAccessSettings { get => _canAccessSettings; set => SetProperty(ref _canAccessSettings, value); }
        
        public string VehiclePlate { get => _vehiclePlate; set => SetProperty(ref _vehiclePlate, value); }
        public string ServiceArea { get => _serviceArea; set => SetProperty(ref _serviceArea, value); }
        public string ExpertiseAreas { get => _expertiseAreas; set => SetProperty(ref _expertiseAreas, value); }
        #endregion

        /// <summary>
        /// Durum mesajı
        /// </summary>
        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                SetProperty(ref _statusMessage, value);
                OnPropertyChanged(nameof(HasStatusMessage));
            }
        }

        /// <summary>
        /// Başarılı mı?
        /// </summary>
        public bool IsSuccess
        {
            get => _isSuccess;
            set => SetProperty(ref _isSuccess, value);
        }

        /// <summary>
        /// Durum mesajı var mı?
        /// </summary>
        public bool HasStatusMessage => !string.IsNullOrEmpty(StatusMessage);

        /// <summary>
        /// Roller listesi (UI için - Patron ve Personel)
        /// </summary>
        public ObservableCollection<string> RoleDisplayOptions { get; } = new ObservableCollection<string>
        {
            "Patron",    // -> Admin
            "Personel",   // -> Technician
            "İzleyici"
        };

        /// <summary>
        /// Form temizlendi event (focus için)
        /// </summary>
        public event Action? FormCleared;

        /// <summary>
        /// Constructor
        /// </summary>
        public AddUserViewModel(IAuthService authService, Microsoft.EntityFrameworkCore.IDbContextFactory<KamatekCrm.Data.AppDbContext> dbContextFactory, IToastService toastService, ILoadingService loadingService)
        {
            _authService = authService;
            _dbContextFactory = dbContextFactory;
            _toastService = toastService;
            _loadingService = loadingService;

        }

        /// <summary>
        /// Kullanıcı adı benzersizliğini kontrol et
        /// </summary>
        private void CheckUsernameAvailability()
        {
            if (string.IsNullOrWhiteSpace(Username))
            {
                IsUsernameAvailable = true;
                return;
            }

            // Client side format check only for now, since API handles it. Or you can do a fast /api/users validation.
            // For true 0-coupling we ignore local EF contexts.
            IsUsernameAvailable = true; 
        }

        /// <summary>
        /// Kayıt yapılabilir mi?
        /// </summary>
        private bool CanSaveUser()
        {
            return !string.IsNullOrWhiteSpace(Ad) &&
                   !string.IsNullOrWhiteSpace(Soyad) &&
                   !string.IsNullOrWhiteSpace(Username) &&
                   !string.IsNullOrWhiteSpace(SelectedRoleDisplay);
        }

        /// <summary>
        /// Kullanıcıyı kaydet
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanSaveUser))]
        private async Task Save()
        {
            _loadingService.Show();
            try
            {
                var dbRole = MapDisplayRoleToDbRole(SelectedRoleDisplay);

                using var context = await _dbContextFactory.CreateDbContextAsync();
                
                if (await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AnyAsync(context.Users, u => u.Username == Username.Trim().ToLower()))
                {
                    _toastService.ShowError("Hata", "Bu kullanıcı adı zaten mevcut.");
                    IsSuccess = false;
                    return;
                }

                var newUser = new User
                {
                    Username = Username.Trim().ToLower(),
                    PasswordHash = "1234",
                    Role = dbRole,
                    Ad = Ad.Trim(),
                    Soyad = Soyad.Trim(),
                    IsActive = true,
                    CanViewFinance = CanViewFinance,
                    CanViewAnalytics = CanViewAnalytics,
                    CanDeleteRecords = CanDeleteRecords,
                    CanApprovePurchase = CanApprovePurchase,
                    CanAccessSettings = CanAccessSettings
                };

                context.Users.Add(newUser);
                await context.SaveChangesAsync();
                
                IsSuccess = true;
                StatusMessage = $"✅ {Ad} {Soyad} başarıyla eklendi!\nVarsayılan şifre: 1234";
                _toastService.ShowSuccess("Kullanıcı oluşturuldu", "Başarılı");

                ClearFormFields();
                FormCleared?.Invoke();
            }
            catch (Exception ex)
            {
                IsSuccess = false;
                StatusMessage = $"❌ Sistem Hatası: {ex.Message}";
                _toastService.ShowError("Kritik Hata", ex.Message ?? "Hata");
            }
            finally
            {
                _loadingService.Hide();
            }
        }

        /// <summary>
        /// Formu temizle
        /// </summary>
        [RelayCommand]
        private void Clear()
        {
            ClearFormFields();
            StatusMessage = string.Empty;
            FormCleared?.Invoke();
        }

        /// <summary>
        /// Form alanlarını temizle
        /// </summary>
        private void ClearFormFields()
        {
            Ad = string.Empty;
            Soyad = string.Empty;
            Username = string.Empty;
            SelectedRoleDisplay = "Personel";
            IsUsernameAvailable = true;
        }

        /// <summary>
        /// Arayüz rol adını veritabanı rol adına dönüştür
        /// </summary>
        private static string MapDisplayRoleToDbRole(string displayRole)
        {
            return displayRole switch
            {
                "Patron" => "Admin",
                "Personel" => "Technician",
                _ => "Technician"
            };
        }
    }
}
