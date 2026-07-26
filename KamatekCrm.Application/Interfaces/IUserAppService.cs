using KamatekCrm.ApplicationCore.Common;
using KamatekCrm.ApplicationCore.DTOs.Users;

namespace KamatekCrm.ApplicationCore.Interfaces
{
    /// <summary>
    /// Kullanıcı yönetimi Application servis kontratı.
    /// Kullanıcı CRUD, şifre hashleme ve RBAC izin yönetimi işlemlerini tanımlar.
    /// </summary>
    public interface IUserAppService
    {
        /// <summary>
        /// Tüm kullanıcıları hafifletilmiş DTO listesi olarak döndürür.
        /// </summary>
        Task<Result<List<UserListItemDto>>> GetAllAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Belirli bir kullanıcının bilgilerini döndürür.
        /// </summary>
        Task<Result<UserListItemDto>> GetByIdAsync(int id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Yeni kullanıcı oluşturur. Şifre BCrypt ile hashlenir.
        /// </summary>
        Task<Result<int>> CreateAsync(UserCreateUpdateDto dto, CancellationToken cancellationToken = default);

        /// <summary>
        /// Mevcut kullanıcı bilgilerini günceller.
        /// Password alanı dolu ise şifre yeniden hashlenir; boş ise mevcut hash korunur.
        /// </summary>
        Task<Result> UpdateAsync(UserCreateUpdateDto dto, CancellationToken cancellationToken = default);

        /// <summary>
        /// Kullanıcıyı pasife çeker (soft deactivation).
        /// </summary>
        Task<Result> DeactivateAsync(int id, CancellationToken cancellationToken = default);
    }
}
