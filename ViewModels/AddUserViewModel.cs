using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using KamatekCrm.Commands;
using KamatekCrm.Data;
using KamatekCrm.Models;
using KamatekCrm.Services;

namespace KamatekCrm.ViewModels
{
    /// <summary>
    /// Kullanıcı ekleme ViewModel - Admin tarafından kullanılır
    /// </summary>
    public class AddUserViewModel : ViewModelBase
    {
        private readonly AppDbContext _context;
        private string _ad = string.Empty;
        private string _soyad = string.Empty;
        private string _username = string.Empty;
        private string _selectedRoleDisplay = "Personel";
        private string _statusMessage = string.Empty;
        private bool _isSuccess;
        private bool _isUsernameAvailable = true;

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
            set => SetProperty(ref _selectedRoleDisplay, value);
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
        /// Roller listesi (UI için - Patron ve Personel)
        /// </summary>
        public ObservableCollection<string> RoleDisplayOptions { get; } = new ObservableCollection<string>
        {
            "Patron",    // -> Admin
            "Personel"   // -> Technician
        };

        /// <summary>
        /// Kaydet komutu
        /// </summary>
        public ICommand SaveCommand { get; }

        /// <summary>
        /// Formu temizle komutu
        /// </summary>
        public ICommand ClearCommand { get; }

        /// <summary>
        /// Form temizlendi event (focus için)
        /// </summary>
        public event Action? FormCleared;

        /// <summary>
        /// Constructor
        /// </summary>
        public AddUserViewModel()
        {
            _context = new AppDbContext();
            SaveCommand = new RelayCommand(_ => SaveUser(), _ => CanSaveUser());
            ClearCommand = new RelayCommand(_ => ClearForm());
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

            var normalizedUsername = Username.ToLower().Trim();
            IsUsernameAvailable = !_context.Users.Any(u => u.Username.ToLower() == normalizedUsername);
        }

        /// <summary>
        /// Kayıt yapılabilir mi?
        /// </summary>
        private bool CanSaveUser()
        {
            return !string.IsNullOrWhiteSpace(Ad) &&
                   !string.IsNullOrWhiteSpace(Soyad) &&
                   !string.IsNullOrWhiteSpace(Username) &&
                   !string.IsNullOrWhiteSpace(SelectedRoleDisplay) &&
                   IsUsernameAvailable;
        }

        /// <summary>
        /// Kullanıcıyı kaydet
        /// </summary>
        private void SaveUser()
        {
            try
            {
                // Son bir kez daha benzersizlik kontrolü
                CheckUsernameAvailability();
                if (!IsUsernameAvailable)
                {
                    IsSuccess = false;
                    StatusMessage = "❌ Bu kullanıcı adı zaten kullanılıyor!";
                    return;
                }

                // UI'dan gelen rol adını veritabanı değerine dönüştür
                var dbRole = MapDisplayRoleToDbRole(SelectedRoleDisplay);

                var newUser = new User
                {
                    Username = Username.Trim().ToLower(),
                    PasswordHash = AuthService.HashPassword("1234"), // Varsayılan şifre
                    Role = dbRole,
                    Ad = Ad.Trim(),
                    Soyad = Soyad.Trim(),
                    IsActive = true,
                    CreatedDate = DateTime.Now
                };

                _context.Users.Add(newUser);
                _context.SaveChanges();

                IsSuccess = true;
                StatusMessage = $"✅ {newUser.AdSoyad} ({newUser.Username}) başarıyla eklendi!\nVarsayılan şifre: 1234";

                // Formu temizle
                ClearFormFields();
                FormCleared?.Invoke();
            }
            catch (Exception ex)
            {
                IsSuccess = false;
                StatusMessage = $"❌ Hata: {ex.Message}";
            }
        }

        /// <summary>
        /// Formu temizle
        /// </summary>
        private void ClearForm()
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
