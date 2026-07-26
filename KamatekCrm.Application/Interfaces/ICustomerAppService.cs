using KamatekCrm.ApplicationCore.Common;
using KamatekCrm.ApplicationCore.DTOs.Customers;

namespace KamatekCrm.ApplicationCore.Interfaces
{
    /// <summary>
    /// Müşteri iş süreçleri Application servis kontratı.
    /// UI katmanı bu arayüz üzerinden müşteri CRUD ve sorgulama işlemlerini gerçekleştirir.
    /// </summary>
    public interface ICustomerAppService
    {
        /// <summary>
        /// Tüm aktif müşterileri hafifletilmiş DTO listesi olarak döndürür.
        /// </summary>
        Task<Result<List<CustomerListItemDto>>> GetAllAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Belirli bir müşterinin tam detay bilgilerini döndürür.
        /// </summary>
        Task<Result<CustomerDetailDto>> GetByIdAsync(int id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Arama terimini müşteri adı, kodu, telefon veya şehirle eşleştirir.
        /// </summary>
        Task<Result<List<CustomerListItemDto>>> SearchAsync(string searchTerm, CancellationToken cancellationToken = default);

        /// <summary>
        /// Yeni müşteri oluşturur.
        /// </summary>
        Task<Result<int>> CreateAsync(CustomerCreateUpdateDto dto, CancellationToken cancellationToken = default);

        /// <summary>
        /// Mevcut müşteri bilgilerini günceller.
        /// </summary>
        Task<Result> UpdateAsync(CustomerCreateUpdateDto dto, CancellationToken cancellationToken = default);

        /// <summary>
        /// Müşteriyi mantıksal olarak siler (Soft Delete).
        /// Bağlı iş emirleri varsa ReferentialIntegrityException fırlatır.
        /// </summary>
        Task<Result> DeleteAsync(int id, CancellationToken cancellationToken = default);
    }
}
