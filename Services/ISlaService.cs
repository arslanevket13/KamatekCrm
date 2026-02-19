using System.Threading.Tasks;

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
    }
}
