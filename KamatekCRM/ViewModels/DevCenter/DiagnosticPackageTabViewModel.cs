using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using KamatekCrm.Diagnostics.Core;

namespace KamatekCrm.ViewModels.DevCenter
{
    /// <summary>
    /// Tanılama paketi dışa aktarma.
    /// <para>
    /// Paket içeriği redakte edilir; hedef dizin sabittir ve dosya adı servis tarafından üretilir.
    /// Dosya işlemleri arka planda yapılır, UI thread'de disk okunmaz.
    /// </para>
    /// </summary>
    public sealed partial class DiagnosticPackageTabViewModel : DevCenterTabViewModel
    {
        private readonly DiagnosticDataService _data;

        private string _statusText = "Henüz paket oluşturulmadı";
        private bool _isBusy;
        private bool _includeLogs = true;
        private bool _includeCrashes = true;
        private string _lastPackagePath = string.Empty;

        public DiagnosticPackageTabViewModel(DiagnosticDataService data)
            : base("Tanılama Paketi", "📦")
        {
            _data = data;
        }

        public ObservableCollection<DiagnosticPackageInfo> Packages { get; } = new();

        public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }

        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    CreatePackageCommand.NotifyCanExecuteChanged();
                }
            }
        }

        public bool IncludeLogs
        {
            get => _includeLogs;
            set => SetProperty(ref _includeLogs, value);
        }

        public bool IncludeCrashes
        {
            get => _includeCrashes;
            set => SetProperty(ref _includeCrashes, value);
        }

        /// <summary>Son oluşturulan paketin yolu (arayüzde kopyalanabilir).</summary>
        public string LastPackagePath { get => _lastPackagePath; private set => SetProperty(ref _lastPackagePath, value); }

        public string ExportsDirectory => _data.ExportsDirectory;

        public string ScopeNote =>
            "Paket yalnızca seçili logları ve çökme izlerini içerir; her satır redakte edilir " +
            "(parola, token, e-posta, telefon, TCKN, IBAN, kart). Dosya başına 2 MB, paket toplamı 20 MB; " +
            "en fazla 10 paket saklanır.";

        public override async Task RefreshAsync() => await LoadPackagesAsync();

        private bool CanCreatePackage() => !IsBusy && (IncludeLogs || IncludeCrashes);

        [RelayCommand(CanExecute = nameof(CanCreatePackage))]
        private async Task CreatePackageAsync()
        {
            if (!CanCreatePackage())
            {
                return;
            }

            IsBusy = true;
            StatusText = "Paket oluşturuluyor...";

            try
            {
                var request = new DiagnosticPackageRequest(IncludeLogs, IncludeCrashes);
                var result = await Task.Run(() => _data.CreatePackage(request));

                if (result.Succeeded)
                {
                    LastPackagePath = result.FilePath!;
                    StatusText = result.Warnings.Count == 0
                        ? $"Paket hazır: {result.FileName} ({result.EntryCount} dosya)"
                        : $"Paket hazır ({result.Warnings.Count} uyarı): {result.FileName}";
                }
                else
                {
                    StatusText = result.ErrorMessage ?? "Paket oluşturulamadı.";
                }

                await LoadPackagesAsync();
            }
            catch (UnauthorizedAccessException ex)
            {
                StatusText = ex.Message;
            }
            catch (Exception ex)
            {
                StatusText = $"Paket hatası: {ex.GetType().Name}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void OpenExportFolder()
        {
            try
            {
                var directory = _data.ExportsDirectory;
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = directory,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                StatusText = $"Dizin açılamadı: {ex.GetType().Name}";
            }
        }

        [RelayCommand]
        private async Task DeletePackageAsync(DiagnosticPackageInfo? package)
        {
            if (package is null)
            {
                return;
            }

            try
            {
                var deleted = await Task.Run(() => _data.DeletePackage(package.FileName));
                StatusText = deleted
                    ? $"{package.FileName} silindi."
                    : $"{package.FileName} silinemedi.";
                await LoadPackagesAsync();
            }
            catch (UnauthorizedAccessException ex)
            {
                StatusText = ex.Message;
            }
        }

        private async Task LoadPackagesAsync()
        {
            try
            {
                var packages = await Task.Run(() => _data.GetPackages());

                Packages.Clear();
                foreach (var package in packages)
                {
                    Packages.Add(package);
                }

                if (packages.Count == 0 && !IsBusy)
                {
                    StatusText = "Henüz paket oluşturulmadı";
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                StatusText = ex.Message;
            }
            catch (Exception ex)
            {
                StatusText = $"Paketler listelenemedi: {ex.GetType().Name}";
            }
        }
    }
}
