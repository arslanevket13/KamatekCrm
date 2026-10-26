using System;
using System.Threading.Tasks;
using KamatekCrm.Shared.Enums;

namespace KamatekCrm.Services
{
    /// <summary>
    /// SLA (Service Level Agreement) ve Bakım Otomasyon Servisi Interface
    /// </summary>
    public interface ISlaService
    {
        /// <summary>
        /// Günü gelen bakım sözleşmelerini kontrol eder ve otomatik iş emri oluşturur.
        /// </summary>
        Task CheckAndGenerateJobsAsync();

        /// <summary>
        /// İş emri durumunun SLA sayacını duraklatıp duraklatmadığını belirtir.
        /// (Örn. Yedek parça veya müşteri onayı beklenirken duraklatılır)
        /// </summary>
        bool IsSlaPaused(JobStatus status);

        /// <summary>
        /// Mesai saatleri (Pazartesi-Cumartesi 09:00-18:00) dikkate alınarak hedef SLA bitiş tarihini hesaplar.
        /// </summary>
        DateTime CalculateWorkingHoursDeadline(DateTime startUtc, TimeSpan slaDuration);

        /// <summary>
        /// Mevcut duruma ve mesai saatlerine göre efektif kalan SLA süresini hesaplar.
        /// Duraklatılmış durumlarda kalan süre dondurulur.
        /// </summary>
        TimeSpan GetRemainingEffectiveTime(DateTime? deadlineUtc, JobStatus status);
    }
}

