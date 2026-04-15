using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using KamatekCrm.Commands;
using KamatekCrm.Data;
using KamatekCrm.Shared.Models;
using KamatekCrm.Services;
using CommunityToolkit.Mvvm.Input;

namespace KamatekCrm.ViewModels
{
    /// <summary>
    /// Kullanıcı düzenleme ViewModel
    /// </summary>
    public class EditUserViewModel : ViewModelBase
    {
        private readonly ApiClient _apiClient;
        private readonly IToastService _toastService;
        private readonly ILoadingService _loadingService;
        private readonly User _user;

        private string _ad;
        private string _soyad;
        private string _selectedRoleDisplay;
        private bool _isActive;
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
        /// Kullanıcı adı (salt okunur)
        /// </summary>
        public string Username => _user.Username;

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
        public string ServiceArea { get =>  _serviceArea; set => SetProperty(ref _serviceArea, value); }
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
        /// Kaydet komutu
        /// </summary>
        public IAsyncRelayCommand SaveCommand { get; }

        /// <summary>
        /// İptal komutu
        /// </summary>
        public ICommand CancelCommand { get; }

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
        public EditUserViewModel(User user, ApiClient apiClient, IToastService toastService, ILoadingService loadingService)
        {
            _apiClient = apiClient;
            _toastService = toastService;
            _loadingService = loadingService;
            _user = user;

            // Mevcut değerleri yükle
            _ad = user.Ad;
            _soyad = user.Soyad;
            _isActive = user.IsActive;
            _selectedRoleDisplay = GetDisplayRole(user.Role);
            
            _canViewFinance = user.CanViewFinance;
            _canViewAnalytics = user.CanViewAnalytics;
            _canDeleteRecords = user.CanDeleteRecords;
            _canApprovePurchase = user.CanApprovePurchase;
            _canAccessSettings = user.CanAccessSettings;

            _phone = user.Phone ?? "";
            _vehiclePlate = user.VehiclePlate ?? "";
            _serviceArea = user.ServiceArea ?? "";
            _expertiseAreas = user.ExpertiseAreas ?? "";

            SaveCommand = new AsyncRelayCommand(SaveUserAsync, CanSaveUser);
            CancelCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(() => CancelRequested?.Invoke());
        }

        /// <summary>
        /// Kayıt yapılabilir mi?
        /// </summary>
        private bool CanSaveUser()
        {
            return !string.IsNullOrWhiteSpace(Ad) &&
                   !string.IsNullOrWhiteSpace(Soyad) &&
                   !string.IsNullOrWhiteSpace(SelectedRoleDisplay);
        }

        /// <summary>
        /// Değişiklikleri kaydet
        /// </summary>
        private async System.Threading.Tasks.Task SaveUserAsync()
        {
            _loadingService.Show();
            try
            {
                var req = new
                {
                    Ad = Ad.Trim(),
                    Soyad = Soyad.Trim(),
                    Role = MapDisplayRoleToDbRole(SelectedRoleDisplay),
                    IsActive = IsActive,
                    CanViewFinance,
                    CanViewAnalytics,
                    CanDeleteRecords,
                    CanApprovePurchase,
                    CanAccessSettings,
                    IsTechnician = IsTechnicianRole,
                    Phone,
                    VehiclePlate = IsTechnicianRole ? VehiclePlate : null,
                    ServiceArea = IsTechnicianRole ? ServiceArea : null,
                    ExpertiseAreas = IsTechnicianRole ? ExpertiseAreas : null
                };

                var response = await _apiClient.PutAsync<object>($"api/users/{_user.Id}", req);

                if (response.Success)
                {
                    IsSuccess = true;
                    StatusMessage = "✅ Kullanıcı güncellendi!";
                    _toastService.ShowSuccess("Kullanıcı güncellendi", "Başarılı");
                    SaveSuccessful?.Invoke();
                }
                else
                {
                    IsSuccess = false;
                    StatusMessage = $"❌ Hata: {response.Message}";
                    _toastService.ShowError("Hata", response.Message ?? "Bilinmeyen Hata");
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
        private static string GetDisplayRole(string role)
        {
            return role?.ToLower() switch
            {
                "admin" => "Patron",
                "technician" => "Personel",
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
                _ => "Technician"
            };
        }
    }
}
