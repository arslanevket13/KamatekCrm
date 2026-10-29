using System;
using System.Threading.Tasks;
using KamatekCrm.Diagnostics.Core;

namespace KamatekCrm.ViewModels.DevCenter
{
    /// <summary>Uygulama, makine ve tampon durumunun özeti.</summary>
    public sealed class OverviewTabViewModel : DevCenterTabViewModel
    {
        private readonly DiagnosticDataService _data;

        private string _appVersion = "-";
        private string _machineName = "-";
        private string _operatingSystem = "-";
        private string _runtimeVersion = "-";
        private int _processId;
        private int _threadCount;
        private string _workingSetText = "-";
        private string _uptimeText = "-";
        private string _logDirectory = "-";
        private string _diagnosticsDirectory = "-";
        private int _developerAccountCount;
        private string _bufferSummary = "-";
        private long _droppedCount;
        private string _levelName = "-";
        private bool _isDiagnosticsEnabled;
        private string _lastRefreshedText = "Henüz yenilenmedi";

        public OverviewTabViewModel(DiagnosticDataService data)
            : base("Genel Bakış", "📊")
        {
            _data = data;
        }

        public string AppVersion { get => _appVersion; private set => SetProperty(ref _appVersion, value); }
        public string MachineName { get => _machineName; private set => SetProperty(ref _machineName, value); }
        public string OperatingSystem { get => _operatingSystem; private set => SetProperty(ref _operatingSystem, value); }
        public string RuntimeVersion { get => _runtimeVersion; private set => SetProperty(ref _runtimeVersion, value); }
        public int ProcessId { get => _processId; private set => SetProperty(ref _processId, value); }
        public int ThreadCount { get => _threadCount; private set => SetProperty(ref _threadCount, value); }
        public string WorkingSetText { get => _workingSetText; private set => SetProperty(ref _workingSetText, value); }
        public string UptimeText { get => _uptimeText; private set => SetProperty(ref _uptimeText, value); }
        public string LogDirectory { get => _logDirectory; private set => SetProperty(ref _logDirectory, value); }
        public string DiagnosticsDirectory { get => _diagnosticsDirectory; private set => SetProperty(ref _diagnosticsDirectory, value); }
        public int DeveloperAccountCount { get => _developerAccountCount; private set => SetProperty(ref _developerAccountCount, value); }
        public string BufferSummary { get => _bufferSummary; private set => SetProperty(ref _bufferSummary, value); }
        public long DroppedCount { get => _droppedCount; private set => SetProperty(ref _droppedCount, value); }
        public string LevelName { get => _levelName; private set => SetProperty(ref _levelName, value); }
        public bool IsDiagnosticsEnabled { get => _isDiagnosticsEnabled; private set => SetProperty(ref _isDiagnosticsEnabled, value); }
        public string LastRefreshedText { get => _lastRefreshedText; private set => SetProperty(ref _lastRefreshedText, value); }

        public override async Task RefreshAsync()
        {
            var stats = await Task.Run(_data.GetStatistics);
            var environment = await Task.Run(_data.GetEnvironmentSnapshot);

            AppVersion = environment.AppVersion;
            MachineName = environment.MachineName;
            OperatingSystem = environment.OperatingSystem;
            RuntimeVersion = environment.RuntimeVersion;
            ProcessId = environment.ProcessId;
            ThreadCount = environment.ThreadCount;
            WorkingSetText = $"{environment.WorkingSetBytes / (1024d * 1024d):N1} MB";
            UptimeText = $"{environment.UptimeMinutes:N0} dk";
            LogDirectory = environment.LogDirectory;
            DiagnosticsDirectory = environment.DiagnosticsDirectory;
            DeveloperAccountCount = environment.ConfiguredDeveloperAccounts;

            IsDiagnosticsEnabled = stats.IsEnabled;
            BufferSummary = $"{stats.Count} / {stats.Capacity} olay";
            DroppedCount = stats.DroppedCount;
            LevelName = stats.LevelName;
            LastRefreshedText = $"Son güncelleme: {DateTime.Now:HH:mm:ss}";
        }
    }
}
