using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KamatekCrm.Diagnostics.Core;

namespace KamatekCrm.ViewModels.DevCenter
{
    /// <summary>
    /// Sınırlı bellek tamponunu periyodik olarak okuyan canlı log ekranı.
    /// <para>
    /// Dosya okuma/ayrıştırma yapılmaz; her turda en fazla 200 olay kopyalanır ve
    /// yalnızca yeni kayıt geldiyse liste yenilenir. Böylece UI thread bloklanmaz.
    /// </para>
    /// </summary>
    public sealed partial class LiveLogsTabViewModel : DevCenterTabViewModel, IDisposable
    {
        private const int MaxVisibleEvents = 200;

        private static readonly IReadOnlyList<string> Levels = new[]
        {
            "Tümü",
            "Debug ve üzeri",
            "Information ve üzeri",
            "Warning ve üzeri",
            "Error ve üzeri"
        };

        private readonly DiagnosticDataService _data;
        private readonly DispatcherTimer _timer;

        private string _selectedLevel = "Tümü";
        private string _searchText = string.Empty;
        private bool _isPaused;
        private string _statusText = "Canlı akış açık";
        private long _lastSequence;
        private bool _disposed;

        public LiveLogsTabViewModel(DiagnosticDataService data)
            : base("Canlı Loglar", "📜")
        {
            _data = data;
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += (_, _) => RefreshNow();
            _timer.Start();
        }

        public ObservableCollection<DiagnosticEvent> Items { get; } = new();

        public IReadOnlyList<string> LevelOptions => Levels;

        public string SelectedLevel
        {
            get => _selectedLevel;
            set
            {
                if (SetProperty(ref _selectedLevel, value))
                {
                    ForceRefresh();
                }
            }
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    ForceRefresh();
                }
            }
        }

        public bool IsPaused
        {
            get => _isPaused;
            private set
            {
                if (SetProperty(ref _isPaused, value))
                {
                    StatusText = value ? "Akış duraklatıldı" : "Canlı akış açık";
                }
            }
        }

        public string StatusText
        {
            get => _statusText;
            private set => SetProperty(ref _statusText, value);
        }

        public override Task RefreshAsync()
        {
            RefreshNow();
            return Task.CompletedTask;
        }

        [RelayCommand]
        private void TogglePause() => IsPaused = !IsPaused;

        [RelayCommand]
        private void Clear()
        {
            Items.Clear();
            _lastSequence = 0;
            StatusText = "Liste temizlendi";
        }

        [RelayCommand]
        private void RefreshNow()
        {
            if (_disposed || IsPaused)
            {
                return;
            }

            try
            {
                var events = _data.Query(new DiagnosticQuery
                {
                    MinimumLevel = ResolveLevel(SelectedLevel),
                    SearchText = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText,
                    Take = MaxVisibleEvents
                });

                if (events.Count == 0)
                {
                    return;
                }

                if (events[0].Sequence == _lastSequence)
                {
                    return;
                }

                _lastSequence = events[0].Sequence;
                Items.Clear();
                foreach (var item in events)
                {
                    Items.Add(item);
                }

                StatusText = $"{Items.Count} olay · {DateTime.Now:HH:mm:ss}";
            }
            catch (UnauthorizedAccessException ex)
            {
                _timer.Stop();
                StatusText = ex.Message;
            }
            catch (Exception ex)
            {
                StatusText = $"Log okunamadı: {ex.GetType().Name}";
            }
        }

        private void ForceRefresh()
        {
            _lastSequence = 0;
            RefreshNow();
        }

        private static DiagnosticLevel ResolveLevel(string? label) => label switch
        {
            "Debug ve üzeri" => DiagnosticLevel.Debug,
            "Information ve üzeri" => DiagnosticLevel.Information,
            "Warning ve üzeri" => DiagnosticLevel.Warning,
            "Error ve üzeri" => DiagnosticLevel.Error,
            _ => DiagnosticLevel.Verbose
        };

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _timer.Stop();
        }
    }
}
