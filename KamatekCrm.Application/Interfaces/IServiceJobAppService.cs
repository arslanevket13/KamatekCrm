using KamatekCrm.ApplicationCore.Common;
using KamatekCrm.ApplicationCore.DTOs.ServiceJobs;

namespace KamatekCrm.ApplicationCore.Interfaces
{
    /// <summary>
    /// İş emri (Servis İşleri) Application servis kontratı.
    /// İş emri listeleme, detay sorgulama ve durum güncellemesini tanımlar.
    /// </summary>
    public interface IServiceJobAppService
    {
        /// <summary>
        /// Tüm iş emirlerini hafifletilmiş DTO listesi olarak döndürür.
        /// </summary>
        Task<Result<List<ServiceJobListItemDto>>> GetAllAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Belirli bir iş emrinin tam detay bilgilerini döndürür.
        /// </summary>
        Task<Result<ServiceJobDetailDto>> GetByIdAsync(int id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Belirli bir müşteriye ait iş emirlerini listeler.
        /// </summary>
        Task<Result<List<ServiceJobListItemDto>>> GetByCustomerIdAsync(int customerId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Belirli bir teknisyene atanmış iş emirlerini listeler.
        /// </summary>
        Task<Result<List<ServiceJobListItemDto>>> GetByTechnicianIdAsync(int technicianUserId, CancellationToken cancellationToken = default);
    }
}
