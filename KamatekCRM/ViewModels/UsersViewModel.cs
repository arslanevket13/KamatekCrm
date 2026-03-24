using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using KamatekCrm.Data;
using KamatekCrm.Shared.Models;
using KamatekCrm.Services;
using KamatekCrm.Views;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.DTOs;
using System.Threading.Tasks;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.Input;

namespace KamatekCrm.ViewModels
{
    /// <summary>
    /// Kullanıcı listesi ViewModel
    /// </summary>
    public class UsersViewModel : ViewModelBase
    {
        private readonly ApiClient _apiClient;
        private readonly IAuthService _authService;
        private readonly IToastService _toastService;
        private readonly ILoadingService _loadingService;
        
        private User? _selectedUser;
        private string _searchText = string.Empty;
        private List<User> _allUsers = new List<User>();

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

        private string _selectedCategory = "Tümü";
        public string SelectedCategory
        {
            get => _selectedCategory;
            set { if (SetProperty(ref _selectedCategory, value)) FilterUsers(); }
        }

        private string _selectedStatus = "Tümü";
        public string SelectedStatus
        {
            get => _selectedStatus;
            set { if (SetProperty(ref _selectedStatus, value)) FilterUsers(); }
        }

        public ObservableCollection<string> CategoryItems { get; } = new ObservableCollection<string> { "Tümü", "Patron", "Personel", "İzleyici" };
        public ObservableCollection<string> StatusItems { get; } = new ObservableCollection<string> { "Tümü", "Aktif", "Pasif" };
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

        public ICommand AddUserCommand { get; }
        public ICommand EditUserCommand { get; }
        public IAsyncRelayCommand DeleteUserCommand { get; }
        public IAsyncRelayCommand ResetPasswordCommand { get; }
        public ICommand SetPasswordCommand { get; }
        public IAsyncRelayCommand RefreshCommand { get; }

        #endregion

        /// <summary>
        /// Constructor
        /// </summary>
        /// <summary>
        /// Constructor
        /// </summary>
        public UsersViewModel(
            IAuthService authService,
            ApiClient apiClient,
            IToastService toastService,
            ILoadingService loadingService)
        {
            _authService = authService;
            _apiClient = apiClient;
            _toastService = toastService;
            _loadingService = loadingService;

            AddUserCommand = new RelayCommand(() => OpenAddUserWindow(), () => IsAdmin);
            EditUserCommand = new RelayCommand(() => OpenEditUserWindow(SelectedUser!), () => SelectedUser != null && IsAdmin);
            DeleteUserCommand = new AsyncRelayCommand(DeleteUserAsync, () => CanDeleteUser());
            ResetPasswordCommand = new AsyncRelayCommand(ResetPasswordTo1234Async, () => SelectedUser != null && IsAdmin);
            SetPasswordCommand = new RelayCommand(() => OpenSetPasswordWindow(), () => SelectedUser != null && IsAdmin);
            RefreshCommand = new AsyncRelayCommand(LoadUsersAsync);

            // Execute initial load
            _ = LoadUsersAsync();
        }

        /// <summary>
        /// Kullanıcıları yükle
        /// </summary>
        private async Task LoadUsersAsync()
        {
            _loadingService.Show();
            try
            {
                var response = await _apiClient.GetAsync<List<User>>("api/users");
                if (response.Success && response.Data != null)
                {
                    _allUsers = response.Data;
                    FilterUsers();
                }
                else
                {
                    _toastService.ShowError("Kullanıcılar Alınamadı", response.Message);
                }
            }
            catch (Exception ex)
            {
                _toastService.ShowError("Hata", ex.Message);
            }
            finally
            {
                _loadingService.Hide();
            }
        }

        /// <summary>
        /// Toplam Kullanıcı Sayısı
        /// </summary>
        public int TotalUsersCount => Users.Count;

        /// <summary>
        /// Aktif Teknisyen Sayısı
        /// </summary>
        public int ActiveTechniciansCount => Users.Count(u => u.Role == "Technician" && u.IsActive);

        /// <summary>
        /// Yönetici Sayısı
        /// </summary>
        public int AdminCount => Users.Count(u => u.Role == "Admin");

        /// <summary>
        /// Kullanıcıları filtrele
        /// </summary>
        private void FilterUsers()
        {
            Users.Clear();
            var query = _allUsers.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var search = SearchText.ToLower();
                query = query.Where(u =>
                    (u.Ad?.ToLower() ?? "").Contains(search) ||
                    (u.Soyad?.ToLower() ?? "").Contains(search) ||
                    (u.Username?.ToLower() ?? "").Contains(search));
            }

            if (SelectedCategory != "Tümü" && !string.IsNullOrEmpty(SelectedCategory))
            {
                string roleFilter = SelectedCategory switch
                {
                    "Patron" => "Admin",
                    "Personel" => "Technician",
                    "İzleyici" => "Viewer",
                    _ => ""
                };
                if (!string.IsNullOrEmpty(roleFilter))
                    query = query.Where(u => u.Role == roleFilter);
            }

            if (SelectedStatus != "Tümü" && !string.IsNullOrEmpty(SelectedStatus))
            {
                bool isActiveFilter = SelectedStatus == "Aktif";
                query = query.Where(u => u.IsActive == isActiveFilter);
            }

            foreach (var user in query.OrderBy(u => u.Ad).ThenBy(u => u.Soyad))
            {
                Users.Add(user);
            }

            OnPropertyChanged(nameof(TotalUsersCount));
            OnPropertyChanged(nameof(ActiveTechniciansCount));
            OnPropertyChanged(nameof(AdminCount));
        }

        /// <summary>
        /// Yeni kullanıcı penceresi aç
        /// </summary>
        /// <summary>
        /// Yeni kullanıcı penceresi aç
        /// </summary>
        private void OpenAddUserWindow()
        {
            var addUserViewModel = new AddUserViewModel(_authService, _apiClient, _toastService, _loadingService);
            var addUserView = new AddUserView
            {
                DataContext = addUserViewModel
            };
            addUserView.ShowDialog();
            _ = LoadUsersAsync();
        }

        /// <summary>
        /// Kullanıcı düzenleme penceresi aç
        /// </summary>
        private void OpenEditUserWindow(User user)
        {
            var viewModel = new EditUserViewModel(user, _apiClient, _toastService, _loadingService);
            var view = new EditUserView { DataContext = viewModel };

            viewModel.SaveSuccessful += () =>
            {
                view.Close();
                _ = LoadUsersAsync();
            };

            viewModel.CancelRequested += () => view.Close();

            view.Owner = Application.Current.MainWindow;
            view.ShowDialog();
        }

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
        private async Task DeleteUserAsync()
        {
            if (SelectedUser == null) return;

            var result = MessageBox.Show(
                $"{SelectedUser.AdSoyad} kullanıcısını sistemden kaldırmak istediğinize emin misiniz?",
                "Kullanıcı Sil",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                _loadingService.Show();
                try
                {
                    var response = await _apiClient.DeleteAsync<object>($"api/users/{SelectedUser.Id}");
                    if (response.Success)
                    {
                        await LoadUsersAsync();
                        _toastService.ShowSuccess("Kullanıcı başarıyla sistemden kaldırıldı.", "Silme Başarılı");
                        SelectedUser = null;
                    }
                    else
                    {
                        _toastService.ShowError("Kullanıcı bulunamadı veya silinemedi.", response.Message);
                    }
                }
                catch (Exception ex)
                {
                    _toastService.ShowError("Hata oluştu", ex.Message);
                }
                finally
                {
                    _loadingService.Hide();
                }
            }
        }

        /// <summary>
        /// Şifre sıfırla (1234 yap)
        /// </summary>
        private async Task ResetPasswordTo1234Async()
        {
            if (SelectedUser == null) return;

            var result = MessageBox.Show(
                $"{SelectedUser.AdSoyad} kullanıcısının şifresini '1234' olarak sıfırlamak istiyor musunuz?",
                "Şifre Sıfırla",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _loadingService.Show();
                try
                {
                    var req = new { NewPassword = "1234" };
                    var response = await _apiClient.PostAsync<object>($"api/users/{SelectedUser.Id}/change-password", req);
                    
                    if (response.Success)
                    {
                        _toastService.ShowSuccess("Şifre başarıyla '1234' olarak sıfırlandı.", "Güvenlik İşlemi");
                    }
                    else
                    {
                        _toastService.ShowError("Sıfırlama Başarısız", response.Message);
                    }
                }
                catch (Exception ex)
                {
                    _toastService.ShowError("Hata", ex.Message);
                }
                finally
                {
                    _loadingService.Hide();
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
