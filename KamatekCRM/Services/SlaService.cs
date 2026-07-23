using System;
using System.Linq;
using System.Threading.Tasks;
using KamatekCrm.Data;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace KamatekCrm.Services
{
    /// <summary>
    /// SLA (Service Level Agreement) ve Bakım Otomasyon Servisi
    /// </summary>
    public class SlaService : ISlaService
    {
        private readonly IDbContextFactory<AppDbContext> _dbContextFactory;

        public SlaService(IDbContextFactory<AppDbContext> dbContextFactory)
        {
            _dbContextFactory = dbContextFactory ?? throw new System.ArgumentNullException(nameof(dbContextFactory));
        }

        /// <summary>
        /// Günü gelen bakım sözleşmelerini kontrol eder ve otomatik iş emri oluşturur.
        /// Thread-safe çalıştırma için yeni DbContext oluşturur.
        /// </summary>
        public async Task CheckAndGenerateJobsAsync()
        {
            try
            {
                await using var context = await _dbContextFactory.CreateDbContextAsync();
                using var scope = await context.Database.BeginTransactionAsync();
                var today = DateTime.Today;

                // Günü gelen veya geçen aktif sözleşmeler
                var dueContracts = await context.MaintenanceContracts
                    .Where(c => c.IsActive && c.NextDueDate <= today)
                    .Include(c => c.Customer)
                    .ToListAsync();

                if (!dueContracts.Any()) return;

                foreach (var contract in dueContracts)
                {
                    // 1. İş Emri Oluştur
                    var job = new ServiceJob
                    {
                        CustomerId = contract.CustomerId ?? 0,
                        JobCategory = JobCategory.Other,
                        WorkOrderType = WorkOrderType.Maintenance,
                        Description = $"{contract.JobDescriptionTemplate} - {today:MMMM yyyy} Dönemi",
                        Status = JobStatus.Pending,
                        Priority = JobPriority.Normal,
                        CreatedDate = DateTime.Now,
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
                }

                await context.SaveChangesAsync();
                await scope.CommitAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SLA Service Error: {ex.Message}");
            }
        }
    }
}


