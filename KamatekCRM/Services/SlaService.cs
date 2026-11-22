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
                await context.ExecuteInTransactionAsync(async transaction =>
                {
                    var today = DateTime.UtcNow.Date;

                    // Günü gelen veya geçen aktif sözleşmeler
                    var dueContracts = await context.MaintenanceContracts
                        .Where(c => c.IsActive && c.NextDueDate <= today)
                        .Include(c => c.Customer)
                        .ToListAsync();

                    if (!dueContracts.Any()) return false;

                    foreach (var contract in dueContracts)
                    {
                        var nowUtc = DateTime.UtcNow;
                        // 1. İş Emri Oluştur
                        var job = new ServiceJob
                        {
                            CustomerId = contract.CustomerId ?? 0,
                            JobCategory = JobCategory.Other,
                            WorkOrderType = WorkOrderType.Maintenance,
                            Description = $"{contract.JobDescriptionTemplate} - {today:MMMM yyyy} Dönemi",
                            Status = JobStatus.Pending,
                            Priority = JobPriority.Normal,
                            CreatedDate = nowUtc,
                            ServiceJobType = ServiceJobType.Fault,
                            ScheduledDate = today.AddDays(1),
                            SlaDeadline = CalculateWorkingHoursDeadline(nowUtc, GetDefaultSlaDuration(JobPriority.Normal)),
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
                    if (transaction is not null)
                    {
                        await transaction.CommitAsync();
                    }

                    return true;
                });
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
        /// Öncelik seviyesine göre varsayılan mesai içi SLA süresini döndürür.
        /// </summary>
        public TimeSpan GetDefaultSlaDuration(JobPriority priority) => priority switch
        {
            JobPriority.Critical => TimeSpan.FromHours(4),
            JobPriority.Urgent => TimeSpan.FromHours(8),
            JobPriority.High => TimeSpan.FromHours(16),
            JobPriority.Medium => TimeSpan.FromHours(24),
            JobPriority.Normal => TimeSpan.FromHours(24),
            JobPriority.Low => TimeSpan.FromHours(48),
            _ => TimeSpan.FromHours(24)
        };

        private static readonly TimeSpan TurkeyUtcOffset = TimeSpan.FromHours(3);

        /// <summary>
        /// Türkiye yerel mesai saatleri (UTC+3, Pzt-Cmt 09:00 - 18:00, Pazar tatil) gözetilerek
        /// SLA bitiş tarihini hesaplar ve sonucu DateTimeKind.Utc olarak döndürür.
        /// </summary>
        public DateTime CalculateWorkingHoursDeadline(DateTime startUtc, TimeSpan slaDuration)
        {
            var normalizedUtc = startUtc.Kind switch
            {
                DateTimeKind.Utc => startUtc,
                DateTimeKind.Local => startUtc.ToUniversalTime(),
                _ => DateTime.SpecifyKind(startUtc, DateTimeKind.Utc)
            };

            // Türkiye yerel saatinde (UTC+3) mesai hesabı
            var currentLocal = DateTime.SpecifyKind(normalizedUtc.Add(TurkeyUtcOffset), DateTimeKind.Unspecified);
            var remaining = slaDuration > TimeSpan.Zero ? slaDuration : TimeSpan.Zero;

            while (remaining > TimeSpan.Zero)
            {
                if (currentLocal.DayOfWeek == DayOfWeek.Sunday)
                {
                    currentLocal = currentLocal.Date.AddDays(1).AddHours(9);
                    continue;
                }

                if (currentLocal.Hour < 9)
                {
                    currentLocal = currentLocal.Date.AddHours(9);
                }
                else if (currentLocal.Hour >= 18)
                {
                    currentLocal = currentLocal.Date.AddDays(1).AddHours(9);
                    continue;
                }

                var dayEnd = currentLocal.Date.AddHours(18);
                var availableToday = dayEnd - currentLocal;

                if (remaining <= availableToday)
                {
                    currentLocal = currentLocal.Add(remaining);
                    remaining = TimeSpan.Zero;
                }
                else
                {
                    remaining -= availableToday;
                    currentLocal = currentLocal.Date.AddDays(1).AddHours(9);
                }
            }

            // Yerel Türkiye saatini tekrar UTC'ye çevir
            return DateTime.SpecifyKind(currentLocal.Subtract(TurkeyUtcOffset), DateTimeKind.Utc);
        }

        /// <summary>
        /// Duraklatma ve Türkiye mesai saatlerini (UTC+3, 09:00-18:00 Pzt-Cmt) dikkate alarak
        /// efektif kalan SLA süresini hesaplar.
        /// </summary>
        public TimeSpan GetRemainingEffectiveTime(DateTime? deadlineUtc, JobStatus status, DateTime? pausedAtUtc = null)
        {
            if (!deadlineUtc.HasValue) return TimeSpan.Zero;

            var targetUtc = deadlineUtc.Value.Kind == DateTimeKind.Utc
                ? deadlineUtc.Value
                : DateTime.SpecifyKind(deadlineUtc.Value, DateTimeKind.Utc);

            // Duraklatılmış durumda referans zaman duraklatılma anında sabitlenir
            var referenceUtc = IsSlaPaused(status)
                ? (pausedAtUtc.HasValue
                    ? (pausedAtUtc.Value.Kind == DateTimeKind.Utc ? pausedAtUtc.Value : pausedAtUtc.Value.ToUniversalTime())
                    : DateTime.UtcNow)
                : DateTime.UtcNow;

            if (IsSlaPaused(status) && targetUtc <= referenceUtc)
            {
                return TimeSpan.Zero;
            }

            return CalculateWorkingHoursBetween(referenceUtc, targetUtc);
        }

        private static TimeSpan CalculateWorkingHoursBetween(DateTime fromUtc, DateTime toUtc)
        {
            if (fromUtc == toUtc) return TimeSpan.Zero;

            bool isNegative = toUtc < fromUtc;
            var startLocal = DateTime.SpecifyKind((isNegative ? toUtc : fromUtc).Add(TurkeyUtcOffset), DateTimeKind.Unspecified);
            var endLocal = DateTime.SpecifyKind((isNegative ? fromUtc : toUtc).Add(TurkeyUtcOffset), DateTimeKind.Unspecified);

            var totalWorking = TimeSpan.Zero;
            var cursor = startLocal;

            while (cursor < endLocal)
            {
                if (cursor.DayOfWeek == DayOfWeek.Sunday)
                {
                    cursor = cursor.Date.AddDays(1).AddHours(9);
                    continue;
                }

                var workStart = cursor.Date.AddHours(9);
                var workEnd = cursor.Date.AddHours(18);

                if (cursor >= workEnd)
                {
                    cursor = cursor.Date.AddDays(1).AddHours(9);
                    continue;
                }

                var segmentStart = cursor < workStart ? workStart : cursor;
                var segmentEnd = endLocal < workEnd ? endLocal : workEnd;

                if (segmentEnd > segmentStart)
                {
                    totalWorking += segmentEnd - segmentStart;
                }

                cursor = cursor.Date.AddDays(1).AddHours(9);
            }

            return isNegative ? -totalWorking : totalWorking;
        }
    }
}


