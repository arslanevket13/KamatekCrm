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
                        using var context = await _dbContextFactory.CreateDbContextAsync(stoppingToken);
                        
                        bool isAlive = await context.Database.CanConnectAsync(stoppingToken);

                        if (!isAlive)
                        {
                            HandleDisconnection("Veritabanı bağlantısı reddedildi veya zaman aşımına uğradı.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    HandleDisconnection($"Kalp atışı hatası (Exception): {ex.Message}");
                }

                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        private void HandleDisconnection(string reason)
        {
            if (_connectionProvider.IsConnected)
            {
                _logger.LogWarning($"BAĞLANTI KOPTU! Sebep: {reason}. Discovery State'e dönülüyor...");
                
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
