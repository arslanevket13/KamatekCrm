using System;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using KamatekCrm.Infrastructure.Data;
using KamatekCrm.Shared.Models;
using KamatekCrm.Services;
using KamatekCrm.Views;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.DTOs;
using System.Threading.Tasks;
using KamatekCrm.ApplicationCore.Interfaces;
using CommunityToolkit.Mvvm.Input;

namespace KamatekCrm.ViewModels
{
    /// <summary>
    /// Kullanıcı listesi ViewModel
    /// </summary>
    public partial class UsersViewModel : ViewModelBase
    {
        private readonly IUserAppService _userAppService;
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

        public User? CurrentUser => _authService.CurrentUser;
        public string CurrentUserName => _authService.CurrentUser?.AdSoyad ?? "Misafir";
        public string CurrentUserRole => GetDisplayRole(_authService.CurrentUser?.Role);
        public bool IsAdmin => _authService.IsAdmin;

        /// <summary>
        /// Constructor
        /// </summary>
        public UsersViewModel(
            IAuthService authService,
            IUserAppService userAppService,
            IToastService toastService,
            ILoadingService loadingService)
        {
            _authService = authService;
            _userAppService = userAppService;
            _toastService = toastService;
            _loadingService = loadingService;

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
                var result = await _userAppService.GetAllAsync();
                if (result.IsSuccess && result.Value != null)
                {
                    _allUsers = result.Value.Select(u => new User
                    {
                        Id = u.Id,
                        Username = u.Username,
                        Ad = u.Ad,
                        Soyad = u.Soyad,
                        Role = u.Role,
                        Phone = u.Phone,
                        IsActive = u.IsActive,
                        CreatedDate = u.CreatedDate
                    }).ToList();
                    FilterUsers();
                }
                else
                {
                    _toastService.ShowError("Hata", result.Error ?? "Kullanıcılar yüklenemedi.");
                }
            }
            catch (Exception ex)
            {
                _toastService.ShowError("Hata", ex.Message ?? "Hata");
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

        private void OpenAddUserWindow()
        {
            // Note: View constructors resolve required services via DI
            var view = new AddUserView();
            view.ShowDialog();
            _ = LoadUsersAsync();
        }

        private void OpenEditUserWindow(User user)
        {
            var view = new EditUserView(user);
            view.Owner = Application.Current.MainWindow;
            view.ShowDialog();
            _ = LoadUsersAsync();
        }

        private void OpenSetPasswordWindow()
        {
            if (SelectedUser == null) return;
            var passwordView = new PasswordResetView(SelectedUser);
            passwordView.ShowDialog();
        }

        private bool CanDeleteUser()
        {
            if (!IsAdmin || SelectedUser == null) return false;
            if (SelectedUser.Id == CurrentUser?.Id) return false;
            return true;
        }

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
                    var res = await _userAppService.DeactivateAsync(SelectedUser.Id);
                    if (res.IsSuccess)
                    {
                        _toastService.ShowSuccess("Silindi", $"{SelectedUser.Username} kullanıcısı pasife alındı.");
                        await LoadUsersAsync();
                        SelectedUser = null;
                    }
                    else
                    {
                        _toastService.ShowError("Hata", res.Error ?? "Kullanıcı pasife alınamadı.");
                    }
                }
                catch (Exception ex)
                {
                    _toastService.ShowError("Hata oluştu", ex.Message ?? "Hata");
                }
                finally
                {
                    _loadingService.Hide();
                }
            }
        }

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
                    var updateDto = new KamatekCrm.ApplicationCore.DTOs.Users.UserCreateUpdateDto
                    {
                        Id = SelectedUser.Id,
                        Username = SelectedUser.Username,
                        Ad = SelectedUser.Ad,
                        Soyad = SelectedUser.Soyad,
                        Phone = SelectedUser.Phone,
                        Role = SelectedUser.Role,
                        Password = "1234",
                        IsActive = SelectedUser.IsActive
                    };

                    var res = await _userAppService.UpdateAsync(updateDto);
                    if (res.IsSuccess)
                    {
                        _toastService.ShowSuccess("Başarılı", "Kullanıcının parolası 1234 olarak sıfırlandı.");
                    }
                    else
                    {
                        _toastService.ShowError("Sıfırlama Başarısız", res.Error ?? "Hata");
                    }
                }
                catch (Exception ex)
                {
                    _toastService.ShowError("Hata", ex.Message ?? "Hata");
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


