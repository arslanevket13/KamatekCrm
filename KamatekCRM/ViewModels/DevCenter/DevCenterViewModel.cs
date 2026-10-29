using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using KamatekCrm.Diagnostics.Authorization;

namespace KamatekCrm.ViewModels.DevCenter
{
    /// <summary>
    /// Geliştirici Merkezi'nin kabuğu: sekmeleri, seçimi ve yenileme akışını yönetir.
    /// Yetki kapısı hem burada hem de <see cref="Diagnostics.Core.DiagnosticDataService"/>
    /// içinde ayrıca denetlenir.
    /// </summary>
    public sealed partial class DevCenterViewModel : ViewModelBase, IDisposable
    {
        private readonly DiagnosticAccessGuard _guard;

        private DevCenterTabViewModel _selectedTab;
        private string _statusText = string.Empty;
        private bool _disposed;

        public DevCenterViewModel(
            OverviewTabViewModel overview,
            LiveLogsTabViewModel liveLogs,
            ErrorCenterTabViewModel errorCenter,
            CrashReportsTabViewModel crashReports,
            PerformanceTabViewModel performance,
            BindingIssuesTabViewModel bindingIssues,
            BuildAnalysisTabViewModel buildAnalysis,
            AutomatedTestsTabViewModel automatedTests,
            HealthTabViewModel health,
            DiagnosticPackageTabViewModel diagnosticPackage,
            DiagnosticAccessGuard guard)
        {
            _guard = guard;

            Tabs = new DevCenterTabViewModel[]
            {
                overview,
                liveLogs,
                errorCenter,
                crashReports,
                performance,
                bindingIssues,
                buildAnalysis,
                automatedTests,
                health,
                diagnosticPackage
            };

            _selectedTab = Tabs[0];
        }

        public IReadOnlyList<DevCenterTabViewModel> Tabs { get; }

        public DevCenterTabViewModel SelectedTab
        {
            get => _selectedTab;
            set
            {
                if (value is null || !SetProperty(ref _selectedTab, value))
                {
                    return;
                }

                _ = SafeRefreshAsync(value);
            }
        }

        public bool IsAuthorized => _guard.IsAuthorized;

        public string DenialReason => _guard.DenialReason;

        public string StatusText
        {
            get => _statusText;
            private set => SetProperty(ref _statusText, value);
        }

        /// <summary>Pencere açıldığında çağrılır; yetkisizse hiçbir veri okunmaz.</summary>
        public async Task InitializeAsync()
        {
            try
            {
                _guard.EnsureAuthorized();
                await _selectedTab.RefreshAsync();
                StatusText = $"Hazır · {DateTime.Now:HH:mm:ss}";
            }
            catch (UnauthorizedAccessException ex)
            {
                StatusText = ex.Message;
            }
            catch (Exception ex)
            {
                StatusText = $"Başlatma hatası: {ex.GetType().Name}";
            }
        }

        [RelayCommand]
        private async Task RefreshAsync()
        {
            await SafeRefreshAsync(SelectedTab);
        }

        private async Task SafeRefreshAsync(DevCenterTabViewModel tab)
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                _guard.EnsureAuthorized();
                await tab.RefreshAsync();
                StatusText = $"{tab.Title} yenilendi · {DateTime.Now:HH:mm:ss}";
            }
            catch (UnauthorizedAccessException ex)
            {
                StatusText = ex.Message;
            }
            catch (Exception ex)
            {
                StatusText = $"{tab.Title} yenilenemedi: {ex.GetType().Name}";
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            foreach (var tab in Tabs)
            {
                (tab as IDisposable)?.Dispose();
            }
        }
    }
}
