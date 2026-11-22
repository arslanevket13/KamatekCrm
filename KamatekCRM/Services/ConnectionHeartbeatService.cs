using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using KamatekCrm.Infrastructure.Data;
using KamatekCrm.Shared.Services;

namespace KamatekCrm.Services
{
    public class ConnectionHeartbeatService : BackgroundService
    {
        private const int MaxConsecutiveFailures = 3;
        private static readonly int[] ReconnectBackoffSeconds = [2, 4, 8, 15];

        private int _consecutiveFailures;
        private int _reconnectAttempt;

        private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
        private readonly IDatabaseConnectionProvider _connectionProvider;
        private readonly ILogger<ConnectionHeartbeatService> _logger;
        private readonly NetworkDiscoveryService? _networkDiscoveryService;

        public ConnectionHeartbeatService(
            IDbContextFactory<AppDbContext> dbContextFactory,
            IDatabaseConnectionProvider connectionProvider,
            ILogger<ConnectionHeartbeatService> logger,
            NetworkDiscoveryService? networkDiscoveryService = null)
        {
            _dbContextFactory = dbContextFactory;
            _connectionProvider = connectionProvider;
            _logger = logger;
            _networkDiscoveryService = networkDiscoveryService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Delay(5000, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                TimeSpan nextDelay;

                if (_connectionProvider.IsConnected && !string.IsNullOrEmpty(_connectionProvider.CurrentServerIp))
                {
                    try
                    {
                        await using var context = await _dbContextFactory.CreateDbContextAsync(stoppingToken);
                        bool isAlive = await context.Database.CanConnectAsync(stoppingToken);

                        if (isAlive)
                        {
                            _consecutiveFailures = 0;
                            _reconnectAttempt = 0;
                        }
                        else
                        {
                            RecordFailure("Veritabanı bağlantısı reddedildi veya zaman aşımına uğradı.");
                        }
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        RecordFailure($"Kalp atışı hatası (Exception): {ex.Message}");
                    }

                    // Sağlıklı durumda 5sn + jitter; Circuit Breaker yeni tetiklendiyse hemen 2sn backoff ile kurtarmaya geç
                    int baseMs = _connectionProvider.IsConnected ? 5000 : ReconnectBackoffSeconds[0] * 1000;
                    nextDelay = TimeSpan.FromMilliseconds(baseMs + Random.Shared.Next(100, 750));
                }
                else
                {
                    // Bağlantı kopuk: üstel geri çekilmeli (2s -> 4s -> 8s -> 15s) öz-iyileştirme döngüsü
                    bool recovered = await TryReconnectAsync(stoppingToken);
                    if (recovered)
                    {
                        _consecutiveFailures = 0;
                        _reconnectAttempt = 0;
                        nextDelay = TimeSpan.FromMilliseconds(5000 + Random.Shared.Next(100, 750));
                    }
                    else
                    {
                        int backoffIndex = Math.Min(_reconnectAttempt, ReconnectBackoffSeconds.Length - 1);
                        int backoffSeconds = ReconnectBackoffSeconds[backoffIndex];
                        _reconnectAttempt++;

                        _logger.LogInformation(
                            "Otomatik yeniden bağlanma denemesi #{Attempt} başarısız. {Seconds} sn sonra tekrar denenecek.",
                            _reconnectAttempt,
                            backoffSeconds);

                        nextDelay = TimeSpan.FromMilliseconds(backoffSeconds * 1000 + Random.Shared.Next(100, 750));
                    }
                }

                await Task.Delay(nextDelay, stoppingToken);
            }
        }

        private async Task<bool> TryReconnectAsync(CancellationToken stoppingToken)
        {
            try
            {
                // 1. Önce son bilinen sunucu IP adresini test et
                string? currentIp = _connectionProvider.CurrentServerIp;
                if (!string.IsNullOrWhiteSpace(currentIp) && await _connectionProvider.TestConnectionAsync(currentIp))
                {
                    RestoreConnection(currentIp);
                    return true;
                }

                // 2. Kayıtlı sunucu adresi farklıysa onu dene
                string savedAddress = Properties.Settings.Default.SavedServerAddress;
                if (!string.IsNullOrWhiteSpace(savedAddress) &&
                    !string.Equals(savedAddress, currentIp, StringComparison.OrdinalIgnoreCase) &&
                    await _connectionProvider.TestConnectionAsync(savedAddress))
                {
                    RestoreConnection(savedAddress);
                    return true;
                }

                // 3. Her 3 başarısız denemede bir ağ keşfini (UDP/LAN) yeniden tetikle
                if (_networkDiscoveryService != null && _reconnectAttempt > 0 && _reconnectAttempt % 3 == 0)
                {
                    string? discoveredIp = await _networkDiscoveryService.TryRediscoverServerAsync(stoppingToken);
                    if (!string.IsNullOrWhiteSpace(discoveredIp))
                    {
                        RestoreConnection(discoveredIp);
                        return true;
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Otomatik yeniden bağlanma sırasında beklenmeyen hata.");
            }

            return false;
        }

        private void RestoreConnection(string serverIp)
        {
            _logger.LogInformation("BAĞLANTI YENİDEN KURULDU! Sunucu: {ServerIp}", serverIp);
            _connectionProvider.SetServerIp(serverIp);
            _connectionProvider.SetConnectionState(true);

            try
            {
                EventAggregator.Instance?.Publish(new DatabaseConnectionRestoredEvent());
            }
            catch (Exception catchEx) { Serilog.Log.Debug(catchEx, "İşlem sırasında beklenen/ikincil hata yakalandı: {Source}", "ConnectionHeartbeatService.cs"); }
        }

        private void RecordFailure(string reason)
        {
            _consecutiveFailures++;
            _logger.LogWarning("Veritabanı kalp atışı başarısız ({Count}/{Max}): {Reason}", 
                _consecutiveFailures, MaxConsecutiveFailures, reason);

            // Anlık tekil dalgalanmalarda hemen bağlantı kopartılmaz; ancak ardışık 3 başarısızlıktan sonra Circuit Breaker tetiklenir
            if (_consecutiveFailures >= MaxConsecutiveFailures)
            {
                HandleDisconnection(reason);
            }
        }

        private void HandleDisconnection(string reason)
        {
            if (_connectionProvider.IsConnected)
            {
                _logger.LogWarning("BAĞLANTI KOPTU (Circuit Breaker tetiklendi)! Sebep: {Reason}. Otomatik yeniden bağlanma döngüsü başlatılıyor...", reason);
                
                _connectionProvider.SetConnectionState(false);
                _reconnectAttempt = 0;
                
                try
                {
                    EventAggregator.Instance?.Publish(new DatabaseConnectionLostEvent());
                }
                catch (Exception catchEx) { Serilog.Log.Debug(catchEx, "İşlem sırasında beklenen/ikincil hata yakalandı: {Source}", "ConnectionHeartbeatService.cs"); }
            }
        }
    }
}
