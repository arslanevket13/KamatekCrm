using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using KamatekCrm.Commands;
using KamatekCrm.Data;
using KamatekCrm.Shared.Models;
using KamatekCrm.Services;
using KamatekCrm.Views;
using KamatekCrm.Shared.Enums;

namespace KamatekCrm.ViewModels
{
    /// <summary>
    /// Kullanıcı listesi ViewModel
    /// </summary>
    public class UsersViewModel : ViewModelBase
    {
        private readonly AppDbContext _context;
        private readonly IAuthService _authService;
        private User? _selectedUser;
        private string _searchText = string.Empty;

        /// <summary>
        /// Kullanıcılar listesi
        /// </summary>
        public ObservableCollection<User> Users { get; } = new ObservableCollection<User>();

        /// <summary>
        /// Seçili kullanıcı
        /// </summary>
        public User? SelectedUser
        {
            get => _selectedUser;
            set => SetProperty(ref _selectedUser, value);
        }

        /// <summary>
        /// Arama metni
        /// </summary>
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    FilterUsers();
                }
            }
        }

        /// <summary>
        /// Mevcut kullanıcı (giriş yapmış)
        /// </summary>
        /// <summary>
        /// Mevcut kullanıcı (giriş yapmış)
        /// </summary>
        public User? CurrentUser => _authService.CurrentUser;

        /// <summary>
        /// Mevcut kullanıcı ad soyad
        /// </summary>
        public string CurrentUserName => _authService.CurrentUser?.AdSoyad ?? "Misafir";

        /// <summary>
        /// Mevcut kullanıcı rol gösterimi
        /// </summary>
        /// <summary>
        /// Mevcut kullanıcı rol gösterimi
        /// </summary>
        public string CurrentUserRole => GetDisplayRole(_authService.CurrentUser?.Role);

        /// <summary>
        /// Admin mi?
        /// </summary>
        public bool IsAdmin => _authService.IsAdmin;

        #region Commands

        /// <summary>
        /// Yeni kullanıcı ekle komutu
        /// </summary>
        public ICommand AddUserCommand { get; }

        /// <summary>
        /// Kullanıcı düzenle komutu
        /// </summary>
        public ICommand EditUserCommand { get; }

        /// <summary>
        /// Kullanıcı sil komutu
        /// </summary>
        public ICommand DeleteUserCommand { get; }

        /// <summary>
        /// Şifre sıfırla komutu (1234)
        /// </summary>
        public ICommand ResetPasswordCommand { get; }

        /// <summary>
        /// Şifre değiştir komutu (özel şifre)
        /// </summary>
        public ICommand SetPasswordCommand { get; }

        /// <summary>
        /// Listeyi yenile komutu
        /// </summary>
        public ICommand RefreshCommand { get; }

        #endregion

        /// <summary>
        /// Constructor
        /// </summary>
        /// <summary>
        /// Constructor
        /// </summary>
        public UsersViewModel(IAuthService authService)
        {
            _authService = authService;
            _context = new AppDbContext();

            AddUserCommand = new RelayCommand(_ => OpenAddUserWindow(), _ => IsAdmin);
            EditUserCommand = new RelayCommand(_ => OpenEditUserWindow(), _ => SelectedUser != null && IsAdmin);
            DeleteUserCommand = new RelayCommand(_ => DeleteUser(), _ => CanDeleteUser());
            ResetPasswordCommand = new RelayCommand(_ => ResetPasswordTo1234(), _ => SelectedUser != null && IsAdmin);
            SetPasswordCommand = new RelayCommand(_ => OpenSetPasswordWindow(), _ => SelectedUser != null && IsAdmin);
            RefreshCommand = new RelayCommand(_ => LoadUsers());

            LoadUsers();
        }

        /// <summary>
        /// Kullanıcıları yükle
        /// </summary>
        private void LoadUsers()
        {
            Users.Clear();

            // Yeni context ile fresh data al
            using var freshContext = new AppDbContext();
            var users = freshContext.Users.OrderBy(u => u.Ad).ThenBy(u => u.Soyad).ToList();

            foreach (var user in users)
            {
                Users.Add(user);
            }
        }

        /// <summary>
        /// Kullanıcıları filtrele
        /// </summary>
        private void FilterUsers()
        {
            Users.Clear();

            using var freshContext = new AppDbContext();
            var query = freshContext.Users.AsQueryable();

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var search = SearchText.ToLower();
                query = query.Where(u =>
                    u.Ad.ToLower().Contains(search) ||
                    u.Soyad.ToLower().Contains(search) ||
                    u.Username.ToLower().Contains(search));
            }

            foreach (var user in query.OrderBy(u => u.Ad).ThenBy(u => u.Soyad))
            {
                Users.Add(user);
            }
        }

        /// <summary>
        /// Yeni kullanıcı penceresi aç
        /// </summary>
        /// <summary>
        /// Yeni kullanıcı penceresi aç
        /// </summary>
        private void OpenAddUserWindow()
        {
            var addUserViewModel = new AddUserViewModel(_authService);
            var addUserView = new AddUserView
            {
                DataContext = addUserViewModel
            };
            addUserView.ShowDialog();
            LoadUsers();
        }

        /// <summary>
        /// Kullanıcı düzenleme penceresi aç
        /// </summary>
        private void OpenEditUserWindow()
        {
            if (SelectedUser == null) return;

            var editUserView = new EditUserView(SelectedUser);
            if (editUserView.ShowDialog() == true)
            {
                LoadUsers();
            }
        }

        /// <summary>
        /// Şifre değiştirme penceresi aç
        /// </summary>
        /// <summary>
        /// Şifre değiştirme penceresi aç
        /// </summary>
        private void OpenSetPasswordWindow()
        {
            if (SelectedUser == null) return;

            var passwordResetViewModel = new PasswordResetViewModel(SelectedUser, _authService);
            var passwordView = new PasswordResetView(SelectedUser, _authService)
            {
                DataContext = passwordResetViewModel
            };
            // Note: PasswordResetView constructor sets DataContext, but we can override or just not set it here if constructor does it.
            // The constructor we refactored does: viewModel = new...; DataContext = viewModel;
            // But we passed authService to View constructor, which passes to VM.
            // So we don't need to instantiate VM here!
            // Wait, we need to correct this. The View constructor handles VM creation.
             passwordView.ShowDialog();
        }

        /// <summary>
        /// Kullanıcı silinebilir mi?
        /// </summary>
        private bool CanDeleteUser()
        {
            if (!IsAdmin || SelectedUser == null) return false;
            // Kendini silemez
            if (SelectedUser.Id == CurrentUser?.Id) return false;
            return true;
        }

        /// <summary>
        /// Kullanıcı sil
        /// </summary>
        private void DeleteUser()
        {
            if (SelectedUser == null) return;

            var result = MessageBox.Show(
                $"{SelectedUser.AdSoyad} kullanıcısını silmek istediğinizden emin misiniz?",
                "Kullanıcı Sil",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    var deletedUsername = SelectedUser.Username;
                    var deletedFullName = SelectedUser.AdSoyad;

                    using var deleteContext = new AppDbContext();
                    var userToDelete = deleteContext.Users.Find(SelectedUser.Id);
                    if (userToDelete != null)
                    {
                        deleteContext.Users.Remove(userToDelete);
                        deleteContext.SaveChanges();
                    }

                    // Audit log kaydet
                    _ = AuditService.LogUserDeletedAsync(deletedUsername, deletedFullName);

                    LoadUsers();
                    SelectedUser = null;
                    MessageBox.Show("Kullanıcı silindi.", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Hata: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// Şifre sıfırla (1234 yap)
        /// </summary>
        private void ResetPasswordTo1234()
        {
            if (SelectedUser == null) return;

            var result = MessageBox.Show(
                $"{SelectedUser.AdSoyad} kullanıcısının şifresini '1234' olarak sıfırlamak istiyor musunuz?",
                "Şifre Sıfırla",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    using var resetContext = new AppDbContext();
                    var user = resetContext.Users.Find(SelectedUser.Id);
                    if (user != null)
                    {
                        user.PasswordHash = _authService.HashPassword("1234");
                        resetContext.SaveChanges();

                        // Audit log kaydet
                        _ = AuditService.LogPasswordResetAsync(user);
                    }

                    MessageBox.Show("Şifre '1234' olarak sıfırlandı.", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Hata: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// Rol adını arayüz gösterimine dönüştür
        /// </summary>
        public static string GetDisplayRole(string? role)
        {
            return role?.ToLower() switch
            {
                "admin" => "Patron",
                "technician" => "Personel",
                "viewer" => "İzleyici",
                _ => role ?? ""
            };
        }
    }
}
