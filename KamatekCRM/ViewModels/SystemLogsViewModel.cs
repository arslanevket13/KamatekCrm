using KamatekCrm.Helpers;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using KamatekCrm.Infrastructure.Data;
using KamatekCrm.Shared.Models;
using KamatekCrm.Services;
using Microsoft.EntityFrameworkCore;

namespace KamatekCrm.ViewModels
{
    /// <summary>
    /// Sistem logları ViewModel
    /// </summary>
    public partial class SystemLogsViewModel : ViewModelBase
    {
        private readonly IAuthService _authService;
        private readonly Microsoft.EntityFrameworkCore.IDbContextFactory<AppDbContext> _dbContextFactory;
        private CancellationTokenSource? _refreshCts;
        private string _searchText = string.Empty;
        private string _selectedActionFilter = "Tümü";
        private string _selectedEntityFilter = "Tümü";
        private DateTime? _startDate;
        private DateTime? _endDate;
        private string _integritySummary = "Bütünlük denetimi bekleniyor";

        /// <summary>
        /// Log kayıtları
        /// </summary>
        public ObservableCollection<ActivityLog> Logs { get; } = new ObservableCollection<ActivityLog>();

        /// <summary>
        /// Görüntülenen kayıtların kriptografik bütünlük durumunu özetler.
        /// </summary>
        public string IntegritySummary
        {
            get => _integritySummary;
            private set => SetProperty(ref _integritySummary, value);
        }

        /// <summary>
        /// Arama metni (300ms debounce ile UI thread bloklamadan asenkron sorgular)
        /// </summary>
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    TriggerDebouncedRefresh(300);
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
                    TriggerDebouncedRefresh(0);
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
                    TriggerDebouncedRefresh(0);
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
                    TriggerDebouncedRefresh(0);
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
                    TriggerDebouncedRefresh(0);
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
            "View",
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
            "ServiceJob",
            "CustomerPersonalData",
            "CustomerReport",
            "CustomerDocument"
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

            TriggerDebouncedRefresh(0);
        }

        private void TriggerDebouncedRefresh(int delayMs)
        {
            _refreshCts?.Cancel();
            _refreshCts = new CancellationTokenSource();
            RefreshInternalAsync(_refreshCts.Token, delayMs).SafeFireAndForget("SystemLogsViewModel.RefreshInternalAsync");
        }

        /// <summary>
        /// Logları asenkron yükle
        /// </summary>
        [RelayCommand]
        private async Task Refresh()
        {
            _refreshCts?.Cancel();
            _refreshCts = new CancellationTokenSource();
            await RefreshInternalAsync(_refreshCts.Token, 0);
        }

        private async Task RefreshInternalAsync(CancellationToken ct, int delayMs)
        {
            try
            {
                if (delayMs > 0)
                {
                    await Task.Delay(delayMs, ct);
                }

                await using var context = await _dbContextFactory.CreateDbContextAsync(ct);
                var query = context.ActivityLogs.AsNoTracking().AsQueryable();

                // Tarih filtresi (PostgreSQL timestamptz uyumu için UTC normalizasyonu)
                if (StartDate.HasValue)
                {
                    var startUtc = StartDate.Value.Kind == DateTimeKind.Utc
                        ? StartDate.Value
                        : DateTime.SpecifyKind(StartDate.Value.Date, DateTimeKind.Utc);
                    query = query.Where(l => l.Timestamp >= startUtc);
                }
                if (EndDate.HasValue)
                {
                    var endUtc = EndDate.Value.Kind == DateTimeKind.Utc
                        ? EndDate.Value
                        : DateTime.SpecifyKind(EndDate.Value.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);
                    query = query.Where(l => l.Timestamp <= endUtc);
                }

                // İşlem tipi filtresi
                if (SelectedActionFilter != "Tümü")
                {
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
                    var search = SearchText.Trim().ToLower();
                    query = query.Where(l =>
                        (l.Username != null && l.Username.ToLower().Contains(search)) ||
                        (l.Description != null && l.Description.ToLower().Contains(search)));
                }

                var visibleLogs = await query
                    .OrderByDescending(l => l.Timestamp)
                    .Take(500)
                    .ToListAsync(ct);

                if (ct.IsCancellationRequested) return;

                Logs.Clear();
                foreach (var log in visibleLogs)
                {
                    Logs.Add(log);
                }

                var verified = visibleLogs.Count(ActivityLogIntegrity.Verify);
                var legacy = visibleLogs.Count(log =>
                    log.IntegrityVersion == 0 && string.IsNullOrWhiteSpace(log.IntegrityHash));
                var invalid = visibleLogs.Count - verified - legacy;
                IntegritySummary = invalid > 0
                    ? $"⚠ {invalid} şüpheli · {verified} doğrulandı · {legacy} eski kayıt"
                    : $"✓ {verified} doğrulandı · {legacy} mühürleme öncesi kayıt";
            }
            catch (OperationCanceledException catchEx) { Serilog.Log.Debug(catchEx, "İşlem sırasında beklenen/ikincil hata yakalandı: {Source}", "SystemLogsViewModel.cs"); }
            catch (Exception ex)
            {
                IntegritySummary = $"⚠ Loglar yüklenemedi: {ex.Message}";
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

            TriggerDebouncedRefresh(0);
        }
    }
}
