using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using KamatekCrm.ApplicationCore.DTOs.Users;
using KamatekCrm.ApplicationCore.Interfaces;
using KamatekCrm.Shared.Models;
using KamatekCrm.Services;

namespace KamatekCrm.ViewModels
{
    /// <summary>
    /// Kullanıcı düzenleme ViewModel
    /// </summary>
    public partial class EditUserViewModel : ViewModelBase
    {
        private readonly IUserAppService _userAppService;
        private readonly IToastService _toastService;
        private readonly ILoadingService _loadingService;
        private readonly IAuthService? _authService;

        private int _userId;
        private string _username = string.Empty;
        private string _ad = string.Empty;
        private string _soyad = string.Empty;
        private string _selectedRoleDisplay = "Personel";
        private bool _isActive = true;
        private bool _isLoaded;
        private string _statusMessage = string.Empty;
        private bool _isSuccess;

        // RBAC Permissions
        private bool _canViewFinance;
        private bool _canViewAnalytics;
        private bool _canDeleteRecords;
        private bool _canApprovePurchase;
        private bool _canAccessSettings;

        // Technician Fields
        private string _phone = string.Empty;
        private string _vehiclePlate = string.Empty;
        private string _serviceArea = string.Empty;
        private string _expertiseAreas = string.Empty;

        /// <summary>
        /// Ad
        /// </summary>
        public string Ad
        {
            get => _ad;
            set
            {
                if (SetProperty(ref _ad, value))
                {
                    SaveCommand.NotifyCanExecuteChanged();
                }
            }
        }

        /// <summary>
        /// Soyad
        /// </summary>
        public string Soyad
        {
            get => _soyad;
            set
            {
                if (SetProperty(ref _soyad, value))
                {
                    SaveCommand.NotifyCanExecuteChanged();
                }
            }
        }

        /// <summary>
        /// Kullanıcı adı (salt okunur)
        /// </summary>
        public string Username => _username;

        /// <summary>
        /// Seçili rol gösterimi
        /// </summary>
        public string SelectedRoleDisplay
        {
            get => _selectedRoleDisplay;
            set 
            {
                if (SetProperty(ref _selectedRoleDisplay, value))
                {
                    OnPropertyChanged(nameof(IsTechnicianRole));
                    SaveCommand.NotifyCanExecuteChanged();
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

        public string Phone { get => _phone; set => SetProperty(ref _phone, value); }
        public string VehiclePlate { get => _vehiclePlate; set => SetProperty(ref _vehiclePlate, value); }
        public string ServiceArea { get => _serviceArea; set => SetProperty(ref _serviceArea, value); }
        public string ExpertiseAreas { get => _expertiseAreas; set => SetProperty(ref _expertiseAreas, value); }
        #endregion

        /// <summary>
        /// Aktif mi?
        /// </summary>
        public bool IsActive
        {
            get => _isActive;
            set => SetProperty(ref _isActive, value);
        }

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
        /// Roller listesi
        /// </summary>
        public ObservableCollection<string> RoleDisplayOptions { get; } = new ObservableCollection<string>
        {
            "Patron",
            "Personel",
            "İzleyici"
        };

        /// <summary>
        /// Kaydetme başarılı event
        /// </summary>
        public event Action? SaveSuccessful;

        /// <summary>
        /// İptal talebi event
        /// </summary>
        public event Action? CancelRequested;

        /// <summary>
        /// Constructor
        /// </summary>
        public EditUserViewModel(
            IUserAppService userAppService,
            IToastService toastService,
            ILoadingService loadingService,
            IAuthService? authService = null)
        {
            _userAppService = userAppService;
            _toastService = toastService;
            _loadingService = loadingService;
            _authService = authService;
        }

        /// <summary>
        /// Pencere yüklenene kadar önizleme değerlerini doldurur; tam veriler LoadAsync ile çekilir.
        /// </summary>
        public void SeedFromPreview(User user)
        {
            _userId = user.Id;
            _username = user.Username;
            OnPropertyChanged(nameof(Username));

            Ad = user.Ad;
            Soyad = user.Soyad;
            IsActive = user.IsActive;
            SelectedRoleDisplay = GetDisplayRole(user.Role);

            CanViewFinance = user.CanViewFinance;
            CanViewAnalytics = user.CanViewAnalytics;
            CanDeleteRecords = user.CanDeleteRecords;
            CanApprovePurchase = user.CanApprovePurchase;
            CanAccessSettings = user.CanAccessSettings;

            Phone = user.Phone ?? string.Empty;
            VehiclePlate = user.VehiclePlate ?? string.Empty;
            ServiceArea = user.ServiceArea ?? string.Empty;
            ExpertiseAreas = user.ExpertiseAreas ?? string.Empty;
        }

        /// <summary>
        /// Kullanıcının tüm RBAC ve teknisyen alanlarını IUserAppService üzerinden taze yükler.
        /// </summary>
        public async Task LoadAsync(int userId)
        {
            _userId = userId;
            _loadingService.Show();
            try
            {
                var result = await _userAppService.GetByIdAsync(userId);
                if (result.IsFailure || result.Value is null)
                {
                    IsSuccess = false;
                    StatusMessage = $"❌ {result.Error ?? "Kullanıcı bilgileri yüklenemedi."}";
                    _toastService.ShowError("Hata", result.Error ?? "Kullanıcı bilgileri yüklenemedi.");
                    return;
                }

                var dto = result.Value;
                _username = dto.Username;
                OnPropertyChanged(nameof(Username));

                Ad = dto.Ad;
                Soyad = dto.Soyad;
                IsActive = dto.IsActive;
                SelectedRoleDisplay = GetDisplayRole(dto.Role);

                CanViewFinance = dto.CanViewFinance;
                CanViewAnalytics = dto.CanViewAnalytics;
                CanDeleteRecords = dto.CanDeleteRecords;
                CanApprovePurchase = dto.CanApprovePurchase;
                CanAccessSettings = dto.CanAccessSettings;

                Phone = dto.Phone ?? string.Empty;
                VehiclePlate = dto.VehiclePlate ?? string.Empty;
                ServiceArea = dto.ServiceArea ?? string.Empty;
                ExpertiseAreas = dto.ExpertiseAreas ?? string.Empty;

                _isLoaded = true;
                SaveCommand.NotifyCanExecuteChanged();
            }
            catch (Exception ex)
            {
                IsSuccess = false;
                StatusMessage = $"❌ Kullanıcı yüklenemedi: {ex.Message}";
                _toastService.ShowError("Hata", ex.Message ?? "Kullanıcı yüklenemedi.");
            }
            finally
            {
                _loadingService.Hide();
            }
        }

        private bool IsAuthorizedAdmin()
        {
            return (_authService?.IsAdmin ?? false) || 
                   string.Equals(App.CurrentUser?.Role, "Admin", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Kayıt yapılabilir mi?
        /// </summary>
        private bool CanSaveUser()
        {
            return _isLoaded &&
                   _userId > 0 &&
                   IsAuthorizedAdmin() &&
                   !string.IsNullOrWhiteSpace(Ad) &&
                   !string.IsNullOrWhiteSpace(Soyad) &&
                   !string.IsNullOrWhiteSpace(SelectedRoleDisplay);
        }

        /// <summary>
        /// Değişiklikleri kaydet
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanSaveUser))]
        private async Task Save()
        {
            if (!IsAuthorizedAdmin())
            {
                IsSuccess = false;
                StatusMessage = "❌ Yetkisiz işlem: Kullanıcı düzenlemek için yönetici yetkisi gereklidir.";
                _toastService.ShowError("Yetkisiz İşlem", "Kullanıcı düzenlemek için yönetici yetkisi gereklidir.");
                return;
            }

            _loadingService.Show();
            try
            {
                var dto = new UserCreateUpdateDto
                {
                    Id = _userId,
                    Username = _username,
                    Ad = Ad.Trim(),
                    Soyad = Soyad.Trim(),
                    Role = MapDisplayRoleToDbRole(SelectedRoleDisplay),
                    IsActive = IsActive,
                    CanViewFinance = CanViewFinance,
                    CanViewAnalytics = CanViewAnalytics,
                    CanDeleteRecords = CanDeleteRecords,
                    CanApprovePurchase = CanApprovePurchase,
                    CanAccessSettings = CanAccessSettings,
                    IsTechnician = IsTechnicianRole,
                    Phone = string.IsNullOrWhiteSpace(Phone) ? null : Phone.Trim(),
                    VehiclePlate = IsTechnicianRole && !string.IsNullOrWhiteSpace(VehiclePlate) ? VehiclePlate.Trim() : null,
                    ServiceArea = IsTechnicianRole && !string.IsNullOrWhiteSpace(ServiceArea) ? ServiceArea.Trim() : null,
                    ExpertiseAreas = IsTechnicianRole && !string.IsNullOrWhiteSpace(ExpertiseAreas) ? ExpertiseAreas.Trim() : null
                };

                var result = await _userAppService.UpdateAsync(dto);
                if (result.IsSuccess)
                {
                    IsSuccess = true;
                    StatusMessage = "✅ Kullanıcı güncellendi!";
                    _toastService.ShowSuccess("Kullanıcı güncellendi", "Başarılı");
                    SaveSuccessful?.Invoke();
                }
                else
                {
                    IsSuccess = false;
                    StatusMessage = $"❌ {result.Error ?? "Kullanıcı güncellenemedi."}";
                    _toastService.ShowError("Hata", result.Error ?? "Kullanıcı güncellenemedi.");
                }
            }
            catch (Exception ex)
            {
                IsSuccess = false;
                StatusMessage = $"❌ Sistem Hatası: {ex.Message}";
                _toastService.ShowError("Hata", ex.Message ?? "Hata");
            }
            finally
            {
                _loadingService.Hide();
            }
        }

        /// <summary>
        /// Rol adını arayüz gösterimine dönüştür
        /// </summary>
        private static string GetDisplayRole(string? role)
        {
            return role?.ToLowerInvariant() switch
            {
                "admin" => "Patron",
                "technician" => "Personel",
                "viewer" => "İzleyici",
                _ => "Personel"
            };
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
                "İzleyici" => "Viewer",
                _ => "Technician"
            };
        }

        [RelayCommand]
        private void Cancel() => CancelRequested?.Invoke();
    }
}

