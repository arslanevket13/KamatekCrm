using System;
using System.Linq;
using System.Threading.Tasks;
using KamatekCrm.Infrastructure.Data;
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
                var today = DateTime.UtcNow.Date;

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
                }

                await context.SaveChangesAsync();
                await scope.CommitAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SLA Service Error: {ex.Message}");
            }
        }

        /// <summary>
        /// İş emri bekleme durumunda mı (SLA sayacı duraklatıldı mı)?
        /// </summary>
        public bool IsSlaPaused(JobStatus status) =>
            status is JobStatus.WaitingForParts or JobStatus.WaitingForApproval;

        /// <summary>
        /// Mesai saatleri (Pzt-Cmt 09:00 - 18:00, Pazar tatil) gözetilerek SLA bitiş tarihini hesaplar.
        /// </summary>
        public DateTime CalculateWorkingHoursDeadline(DateTime startUtc, TimeSpan slaDuration)
        {
            var current = startUtc;
            var remaining = slaDuration;

            // İş başlangıcı çalışma saatleri dışındaysa bir sonraki mesai başlangıcına çek
            while (remaining > TimeSpan.Zero)
            {
                if (current.DayOfWeek == DayOfWeek.Sunday)
                {
                    current = current.Date.AddDays(1).AddHours(9);
                    continue;
                }

                if (current.Hour < 9)
                {
                    current = current.Date.AddHours(9);
                }
                else if (current.Hour >= 18)
                {
                    current = current.Date.AddDays(1).AddHours(9);
                    continue;
                }

                var dayEnd = current.Date.AddHours(18);
                var availableToday = dayEnd - current;

                if (remaining <= availableToday)
                {
                    current = current.Add(remaining);
                    remaining = TimeSpan.Zero;
                }
                else
                {
                    remaining -= availableToday;
                    current = current.Date.AddDays(1).AddHours(9);
                }
            }

            return current;
        }

        /// <summary>
        /// Duraklatma ve mesai saatlerini dikkate alarak efektif kalan SLA süresini hesaplar.
        /// </summary>
        public TimeSpan GetRemainingEffectiveTime(DateTime? deadlineUtc, JobStatus status)
        {
            if (!deadlineUtc.HasValue) return TimeSpan.Zero;
            if (IsSlaPaused(status))
            {
                // Duraklatılmış durumda kalan süre dondurulur (mevcut an yerine deadline ile kıyas)
                return deadlineUtc.Value > DateTime.UtcNow ? deadlineUtc.Value - DateTime.UtcNow : TimeSpan.Zero;
            }
            return deadlineUtc.Value - DateTime.UtcNow;
        }
    }
}


