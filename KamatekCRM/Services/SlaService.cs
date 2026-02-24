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
        private readonly AppDbContext _context;

        public SlaService(AppDbContext context)
        {
            _context = context ?? throw new System.ArgumentNullException(nameof(context));
        }
        /// <summary>
        /// Günü gelen bakım sözleşmelerini kontrol eder ve otomatik iş emri oluşturur.
        /// Thread-safe çalıştırma için yeni DbContext oluşturur.
        /// </summary>
        public async Task CheckAndGenerateJobsAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    // DbContext thread-safe olmadığı için yeni scope oluştur
                    using (var scope = _context.Database.BeginTransaction())
                    {
                        var today = DateTime.Today;

                        // Günü gelen veya geçen aktif sözleşmeler
                        var dueContracts = _context.MaintenanceContracts
                            .Where(c => c.IsActive && c.NextDueDate <= today)
                            .Include(c => c.Customer)
                            .ToList();

                        if (!dueContracts.Any()) return;

                        foreach (var contract in dueContracts)
                        {
                            // 1. İş Emri Oluştur
                            var job = new ServiceJob
                            {
                                CustomerId = contract.CustomerId,
                                JobCategory = JobCategory.Other, // Bakım kategorisi varsa o seçilmeli
                                WorkOrderType = WorkOrderType.Maintenance,
                                Description = $"{contract.JobDescriptionTemplate} - {today:MMMM yyyy} Dönemi",
                                Status = JobStatus.Pending,
                                Priority = JobPriority.Normal,
                                CreatedDate = DateTime.Now,
                                ServiceJobType = ServiceJobType.Fault, // Veya Project, bakıma göre değişir
                                ScheduledDate = today.AddDays(1), // Varsayılan olarak yarına planla
                                Price = contract.PricePerVisit
                            };

                            _context.ServiceJobs.Add(job);

                            // 2. Bir sonraki tarihi güncelle
                            // Eğer NextDueDate çok eskiyse, bugünden itibaren ileriye at
                            var nextDate = contract.NextDueDate.AddMonths(contract.FrequencyInMonths);
                            if (nextDate < today)
                            {
                                nextDate = today.AddMonths(contract.FrequencyInMonths);
                            }
                            contract.NextDueDate = nextDate;
                        }

                        _context.SaveChanges();
                        scope.Commit();
                    }
                }
                catch (Exception ex)
                {
                    // Loglama yapılabilir
                    System.Diagnostics.Debug.WriteLine($"SLA Service Error: {ex.Message}");
                }
            });
        }
    }
}
