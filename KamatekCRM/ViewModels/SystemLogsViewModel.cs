using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using KamatekCrm.Data;
using KamatekCrm.Shared.Models;
using KamatekCrm.Services;

namespace KamatekCrm.ViewModels
{
    /// <summary>
    /// Sistem logları ViewModel
    /// </summary>
    public partial class SystemLogsViewModel : ViewModelBase
    {
        private readonly IAuthService _authService;
        private readonly Microsoft.EntityFrameworkCore.IDbContextFactory<AppDbContext> _dbContextFactory;
        private string _searchText = string.Empty;
        private string _selectedActionFilter = "Tümü";
        private string _selectedEntityFilter = "Tümü";
        private DateTime? _startDate;
        private DateTime? _endDate;

        /// <summary>
        /// Log kayıtları
        /// </summary>
        public ObservableCollection<ActivityLog> Logs { get; } = new ObservableCollection<ActivityLog>();

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
                    Refresh();
                }
            }
        }

        /// <summary>
        /// Seçili işlem filtresi
        /// </summary>
        public string SelectedActionFilter
        {
            get => _selectedActionFilter;
            set
            {
                if (SetProperty(ref _selectedActionFilter, value))
                {
                    Refresh();
                }
            }
        }

        /// <summary>
        /// Seçili entity filtresi
        /// </summary>
        public string SelectedEntityFilter
        {
            get => _selectedEntityFilter;
            set
            {
                if (SetProperty(ref _selectedEntityFilter, value))
                {
                    Refresh();
                }
            }
        }

        /// <summary>
        /// Başlangıç tarihi
        /// </summary>
        public DateTime? StartDate
        {
            get => _startDate;
            set
            {
                if (SetProperty(ref _startDate, value))
                {
                    Refresh();
                }
            }
        }

        /// <summary>
        /// Bitiş tarihi
        /// </summary>
        public DateTime? EndDate
        {
            get => _endDate;
            set
            {
                if (SetProperty(ref _endDate, value))
                {
                    Refresh();
                }
            }
        }

        /// <summary>
        /// İşlem tipleri (filtre için)
        /// </summary>
        public ObservableCollection<string> ActionTypes { get; } = new ObservableCollection<string>
        {
            "Tümü",
            "Login",
            "Logout",
            "Create",
            "Update",
            "Delete",
            "PasswordChange",
            "PasswordReset"
        };

        /// <summary>
        /// Entity tipleri (filtre için)
        /// </summary>
        public ObservableCollection<string> EntityTypes { get; } = new ObservableCollection<string>
        {
            "Tümü",
            "User",
            "Customer",
            "Product",
            "ServiceJob"
        };

        /// <summary>
        /// Admin mi?
        /// </summary>
        public bool IsAdmin => _authService.IsAdmin;

        /// <summary>
        /// Constructor
        /// </summary>
        public SystemLogsViewModel(IAuthService authService, Microsoft.EntityFrameworkCore.IDbContextFactory<AppDbContext> dbContextFactory)
        {
            _authService = authService;
            _dbContextFactory = dbContextFactory;

            // Varsayılan: Son 7 gün
            _startDate = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc).AddDays(-7);
            _endDate = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc).AddDays(1);

            Refresh();
        }

        /// <summary>
        /// Logları yükle
        /// </summary>
        [RelayCommand]
        private void Refresh()
        {
            Logs.Clear();

            using var context = _dbContextFactory.CreateDbContext();
            var query = context.ActivityLogs.AsQueryable();

            // Tarih filtresi
            if (StartDate.HasValue)
            {
                query = query.Where(l => l.Timestamp >= StartDate.Value);
            }
            if (EndDate.HasValue)
            {
                query = query.Where(l => l.Timestamp <= EndDate.Value);
            }

            // İşlem tipi filtresi
            if (SelectedActionFilter != "Tümü")
            {
                // Assuming AuditActionType is an enum and SelectedActionFilter holds its string representation
                // If SelectedActionFilter is directly the string value, the original line is correct.
                // If SelectedActionFilter needs to be converted from an enum, you'd typically parse it or have a different property.
                // Based on the instruction "Convert AuditActionType to string for comparison",
                // and assuming SelectedActionFilter is already the string to compare against l.ActionType,
                // the original line is already performing a string comparison.
                // If the intent was to introduce an enum property, that would be a larger change.
                // For now, keeping the existing logic as it correctly compares strings.
                // Compare as string to avoid Enum vs String mismatch
                query = query.Where(l => l.ActionType.ToString() == SelectedActionFilter);
            }

            // Entity filtresi
            if (SelectedEntityFilter != "Tümü")
            {
                query = query.Where(l => l.EntityName == SelectedEntityFilter);
            }

            // Arama
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var search = SearchText.ToLower();
                query = query.Where(l =>
                    (l.Username != null && l.Username.ToLower().Contains(search)) ||
                    (l.Description != null && l.Description.ToLower().Contains(search)));
            }

            // Son kayıtlar önce
            foreach (var log in query.OrderByDescending(l => l.Timestamp).Take(500))
            {
                Logs.Add(log);
            }
        }

        /// <summary>
        /// Filtreleri temizle
        /// </summary>
        [RelayCommand]
        private void ClearFilters()
        {
            _searchText = string.Empty;
            _selectedActionFilter = "Tümü";
            _selectedEntityFilter = "Tümü";
            _startDate = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc).AddDays(-7);
            _endDate = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc).AddDays(1);

            OnPropertyChanged(nameof(SearchText));
            OnPropertyChanged(nameof(SelectedActionFilter));
            OnPropertyChanged(nameof(SelectedEntityFilter));
            OnPropertyChanged(nameof(StartDate));
            OnPropertyChanged(nameof(EndDate));

            Refresh();
        }
    }
}
