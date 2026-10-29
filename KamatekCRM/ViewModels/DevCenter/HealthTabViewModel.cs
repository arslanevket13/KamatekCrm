using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using KamatekCrm.Diagnostics.Core;
using KamatekCrm.Helpers;
using KamatekCrm.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace KamatekCrm.ViewModels.DevCenter
{
    /// <summary>
    /// API ve veritabanı sağlığını gerçek çağrılarla kontrol eder.
    /// Ağır işlem yoktur; her kontrol kısa zaman aşımıyla arka planda yapılır.
    /// </summary>
    public sealed partial class HealthTabViewModel : DevCenterTabViewModel
    {
        private readonly DiagnosticDataService _data;
        private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        private string _databaseStatus = "Kontrol edilmedi";
        private string _apiStatus = "Kontrol edilmedi";
        private string _lastCheckedText = "Henüz kontrol edilmedi";
        private bool _isBusy;

        public HealthTabViewModel(
            DiagnosticDataService data,
            IDbContextFactory<AppDbContext> dbContextFactory,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration)
            : base("API / DB Sağlığı", "🩺")
        {
            _data = data;
            _dbContextFactory = dbContextFactory;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }

        public string DatabaseStatus { get => _databaseStatus; private set => SetProperty(ref _databaseStatus, value); }
        public string ApiStatus { get => _apiStatus; private set => SetProperty(ref _apiStatus, value); }
        public string LastCheckedText { get => _lastCheckedText; private set => SetProperty(ref _lastCheckedText, value); }
        public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }

        public override Task RefreshAsync() => CheckAsync();

        [RelayCommand]
        private async Task CheckAsync()
        {
            if (IsBusy)
            {
                return;
            }

            _data.EnsureAccess();
            IsBusy = true;

            try
            {
                DatabaseStatus = await CheckDatabaseAsync();
                ApiStatus = await CheckApiAsync();
                LastCheckedText = $"Son kontrol: {DateTime.Now:HH:mm:ss}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task<string> CheckDatabaseAsync()
        {
            try
            {
                await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var canConnect = await dbContext.Database.CanConnectAsync(timeout.Token);
                return canConnect ? "Bağlı" : "Bağlantı kurulamadı";
            }
            catch (OperationCanceledException)
            {
                return "Zaman aşımı (5 sn)";
            }
            catch (Exception ex)
            {
                // Mesaj yerine yalnızca tip adı: bağlantı dizesi sızmasın.
                return $"Hata: {ex.GetType().Name}";
            }
        }

        private async Task<string> CheckApiAsync()
        {
            var baseUrl = _configuration["AppSettings:ApiBaseUrl"] ?? ProcessManager.API_LOCAL_URL;

            try
            {
                using var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(5);
                using var response = await client.GetAsync($"{baseUrl.TrimEnd('/')}/health");
                return response.IsSuccessStatusCode
                    ? $"Sağlıklı ({(int)response.StatusCode})"
                    : $"Yanıt kodu: {(int)response.StatusCode}";
            }
            catch (TaskCanceledException)
            {
                return "Zaman aşımı (5 sn)";
            }
            catch (Exception ex)
            {
                return $"Erişilemiyor: {ex.GetType().Name}";
            }
        }
    }
}
