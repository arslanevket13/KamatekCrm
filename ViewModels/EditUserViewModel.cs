using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using KamatekCrm.Commands;
using KamatekCrm.Data;
using KamatekCrm.Models;
using KamatekCrm.Services;

namespace KamatekCrm.ViewModels
{
    /// <summary>
    /// Kullanıcı düzenleme ViewModel
    /// </summary>
    public class EditUserViewModel : ViewModelBase
    {
        private readonly AppDbContext _context;
        private readonly User _user;

        private string _ad;
        private string _soyad;
        private string _selectedRoleDisplay;
        private bool _isActive;
        private string _statusMessage = string.Empty;
        private bool _isSuccess;

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
            set => SetProperty(ref _selectedRoleDisplay, value);
        }

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
            "Personel"
        };

        /// <summary>
        /// Kaydet komutu
        /// </summary>
        public ICommand SaveCommand { get; }

        /// <summary>
        /// Kaydetme başarılı event
        /// </summary>
        public event Action? SaveSuccessful;

        /// <summary>
        /// Constructor
        /// </summary>
        public EditUserViewModel(User user)
        {
            _context = new AppDbContext();
            _user = user;

            // Mevcut değerleri yükle
            _ad = user.Ad;
            _soyad = user.Soyad;
            _isActive = user.IsActive;
            _selectedRoleDisplay = GetDisplayRole(user.Role);

            SaveCommand = new RelayCommand(_ => SaveUser(), _ => CanSaveUser());
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
        private void SaveUser()
        {
            try
            {
                // Veritabanından kullanıcıyı al
                var dbUser = _context.Users.Find(_user.Id);
                if (dbUser == null)
                {
                    IsSuccess = false;
                    StatusMessage = "❌ Kullanıcı bulunamadı!";
                    return;
                }

                // Değerleri güncelle
                dbUser.Ad = Ad.Trim();
                dbUser.Soyad = Soyad.Trim();
                dbUser.Role = MapDisplayRoleToDbRole(SelectedRoleDisplay);
                dbUser.IsActive = IsActive;

                _context.SaveChanges();

                IsSuccess = true;
                StatusMessage = "✅ Kullanıcı güncellendi!";

                SaveSuccessful?.Invoke();
            }
            catch (Exception ex)
            {
                IsSuccess = false;
                StatusMessage = $"❌ Hata: {ex.Message}";
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
