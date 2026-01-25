using System;
using System.Linq;
using System.Threading.Tasks;
using KamatekCrm.Data;
using KamatekCrm.Enums;
using KamatekCrm.Models;
using Microsoft.EntityFrameworkCore;

namespace KamatekCrm.Services
{
    /// <summary>
    /// SLA (Service Level Agreement) ve Bakım Otomasyon Servisi
    /// </summary>
    public class SlaService
    {
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
                    using (var context = new AppDbContext())
                    {
                        var today = DateTime.Today;

                        // Günü gelen veya geçen aktif sözleşmeler
                        var dueContracts = context.MaintenanceContracts
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

                            context.ServiceJobs.Add(job);

                            // 2. Bir sonraki tarihi güncelle
                            // Eğer NextDueDate çok eskiyse, bugünden itibaren ileriye at
                            var nextDate = contract.NextDueDate.AddMonths(contract.FrequencyInMonths);
                            if (nextDate < today)
                            {
                                nextDate = today.AddMonths(contract.FrequencyInMonths);
                            }
                            contract.NextDueDate = nextDate;
                        }

                        context.SaveChanges();
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
