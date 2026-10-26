using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using KamatekCrm.Infrastructure.Data;
using KamatekCrm.Shared.Services;
using KamatekCrm.Services;

namespace KamatekCrm.Services
{
    public class ConnectionHeartbeatService : BackgroundService
    {
        private const int MaxConsecutiveFailures = 3;
        private int _consecutiveFailures = 0;

        private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
        private readonly IDatabaseConnectionProvider _connectionProvider;
        private readonly ILogger<ConnectionHeartbeatService> _logger;

        public ConnectionHeartbeatService(
            IDbContextFactory<AppDbContext> dbContextFactory,
            IDatabaseConnectionProvider connectionProvider,
            ILogger<ConnectionHeartbeatService> logger)
        {
            _dbContextFactory = dbContextFactory;
            _connectionProvider = connectionProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Delay(5000, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (_connectionProvider.IsConnected && !string.IsNullOrEmpty(_connectionProvider.CurrentServerIp))
                    {
                        await using var context = await _dbContextFactory.CreateDbContextAsync(stoppingToken);
                        
                        bool isAlive = await context.Database.CanConnectAsync(stoppingToken);

                        if (isAlive)
                        {
                            // Başarılı ping durumunda ardışık hata sayacını sıfırla
                            _consecutiveFailures = 0;
                        }
                        else
                        {
                            RecordFailure("Veritabanı bağlantısı reddedildi veya zaman aşımına uğradı.");
                        }
                    }
                    else
                    {
                        _consecutiveFailures = 0;
                    }
                }
                catch (Exception ex)
                {
                    RecordFailure($"Kalp atışı hatası (Exception): {ex.Message}");
                }

                // Jitter (100 - 750 ms rastgele sapma): Retry storm ve tüm istemcilerin aynı anda sunucuya yüklenmesini önler
                int jitterMs = Random.Shared.Next(100, 750);
                TimeSpan delay = TimeSpan.FromMilliseconds(5000 + jitterMs);
                await Task.Delay(delay, stoppingToken);
            }
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
                _logger.LogWarning($"BAĞLANTI KOPTU (Circuit Breaker tetiklendi)! Sebep: {reason}. Discovery State'e dönülüyor...");
                
                _connectionProvider.SetConnectionState(false);
                
                try
                {
                    KamatekCrm.Services.EventAggregator.Instance?.Publish(new KamatekCrm.Services.DatabaseConnectionLostEvent());
                }
                catch { }
            }
        }
    }
}
