using KamatekCrm.Data;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace KamatekCrm.API.Services;

/// <summary>
/// SLA (Service Level Agreement) Arka Plan İşçisi.
/// Her 6 saatte bir bakım sözleşmelerini kontrol eder ve otomatik iş emri oluşturur.
/// Bu servis YALNIZCA API tarafında çalışır — WPF masaüstü uygulaması bu işi yapmaz.
/// </summary>
public class SlaBackgroundWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SlaBackgroundWorker> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromHours(6);

    public SlaBackgroundWorker(IServiceScopeFactory scopeFactory, ILogger<SlaBackgroundWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SLA Background Worker started. Interval: {Interval}", _interval);

        // İlk çalıştırmayı hemen yap (startup'ta)
        await RunSlaCheckAsync(stoppingToken);

        // Sonra periyodik olarak çalış
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_interval, stoppingToken);
                await RunSlaCheckAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Graceful shutdown
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SLA Background Worker unhandled error. Will retry next interval.");
            }
        }

        _logger.LogInformation("SLA Background Worker stopped.");
    }

    private async Task RunSlaCheckAsync(CancellationToken ct)
    {
        _logger.LogInformation("Running SLA maintenance check at {Time}", DateTime.UtcNow);

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var today = DateTime.Today;

            // Günü gelen veya geçen aktif sözleşmeler
            var dueContracts = await context.MaintenanceContracts
                .Where(c => c.IsActive && c.NextDueDate <= today)
                .Include(c => c.Customer)
                .ToListAsync(ct);

            if (dueContracts.Count == 0)
            {
                _logger.LogInformation("No due maintenance contracts found.");
                return;
            }

            _logger.LogInformation("Found {Count} due maintenance contracts.", dueContracts.Count);

            foreach (var contract in dueContracts)
            {
                // 1. İş Emri Oluştur
                var job = new ServiceJob
                {
                    CustomerId = contract.CustomerId,
                    JobCategory = JobCategory.Other,
                    WorkOrderType = WorkOrderType.Maintenance,
                    Description = $"{contract.JobDescriptionTemplate} - {today:MMMM yyyy} Dönemi",
                    Status = JobStatus.Pending,
                    Priority = JobPriority.Normal,
                    CreatedDate = DateTime.UtcNow,
                    ServiceJobType = ServiceJobType.Fault,
                    ScheduledDate = today.AddDays(1),
                    Price = contract.PricePerVisit
                };

                context.ServiceJobs.Add(job);

                // 2. Bir sonraki tarihi güncelle
                var nextDate = contract.NextDueDate.AddMonths(contract.FrequencyInMonths);
                if (nextDate < today)
                {
                    nextDate = today.AddMonths(contract.FrequencyInMonths);
                }
                contract.NextDueDate = nextDate;

                _logger.LogInformation("Created maintenance job for contract {ContractId}, next due: {NextDue}",
                    contract.Id, nextDate);
            }

            await context.SaveChangesAsync(ct);
            _logger.LogInformation("SLA check completed. {Count} jobs created.", dueContracts.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during SLA maintenance check.");
        }
    }
}
