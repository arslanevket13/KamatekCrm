using KamatekCrm.ApplicationCore.Common;
using KamatekCrm.ApplicationCore.DTOs.Users;
using KamatekCrm.ApplicationCore.Interfaces;
using KamatekCrm.Shared.Models;
using KamatekCrm.Shared.Repositories;

namespace KamatekCrm.ApplicationCore.Services
{
    /// <summary>
    /// Kullanıcı yönetimi Application servisi.
    /// BCrypt şifre hashleme, RBAC izin yönetimi ve kullanıcı CRUD işlemlerini yönetir.
    /// </summary>
    public class UserAppService : IUserAppService
    {
        private readonly IUnitOfWork _unitOfWork;

        public UserAppService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        }

        public async Task<Result<List<UserListItemDto>>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var users = await _unitOfWork.Repository<User>()
                    .GetAllAsync(cancellationToken);

                var dtos = users
                    .Select(u => u.ToListItemDto())
                    .ToList();

                return Result.Success(dtos);
            }
            catch (Exception ex)
            {
                return Result.Failure<List<UserListItemDto>>($"Kullanıcılar yüklenirken hata oluştu: {ex.Message}");
            }
        }

        public async Task<Result<UserListItemDto>> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            try
            {
                var user = await _unitOfWork.Repository<User>()
                    .GetByIdAsync(id, cancellationToken);

                if (user is null)
                    return Result.Failure<UserListItemDto>($"Kullanıcı bulunamadı (ID: {id})");

                return Result.Success(user.ToListItemDto());
            }
            catch (Exception ex)
            {
                return Result.Failure<UserListItemDto>($"Kullanıcı detayı yüklenirken hata oluştu: {ex.Message}");
            }
        }

        public async Task<Result<int>> CreateAsync(UserCreateUpdateDto dto, CancellationToken cancellationToken = default)
        {
            try
            {
                // İş kuralı: Kullanıcı adı benzersiz olmalı
                var existingUsers = await _unitOfWork.Repository<User>()
                    .FindAsync(u => u.Username == dto.Username, cancellationToken);

                if (existingUsers.Any())
                    return Result.Failure<int>($"'{dto.Username}' kullanıcı adı zaten kullanımda.");

                // İş kuralı: Şifre zorunlu (yeni kullanıcı)
                if (string.IsNullOrWhiteSpace(dto.Password))
                    return Result.Failure<int>("Yeni kullanıcı için şifre zorunludur.");

                var entity = new User();
                dto.ApplyToEntity(entity);
                entity.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);
                entity.CreatedDate = DateTime.UtcNow;

                await _unitOfWork.Repository<User>().AddAsync(entity, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                return Result.Success(entity.Id);
            }
            catch (Exception ex)
            {
                return Result.Failure<int>($"Kullanıcı oluşturulurken hata oluştu: {ex.Message}");
            }
        }

        public async Task<Result> UpdateAsync(UserCreateUpdateDto dto, CancellationToken cancellationToken = default)
        {
            try
            {
                var entity = await _unitOfWork.Repository<User>()
                    .GetByIdAsync(dto.Id, cancellationToken);

                if (entity is null)
                    return Result.Failure($"Güncellenecek kullanıcı bulunamadı (ID: {dto.Id})");

                // İş kuralı: Kullanıcı adı değiştiyse benzersizliğini kontrol et
                if (entity.Username != dto.Username)
                {
                    var usernameConflict = await _unitOfWork.Repository<User>()
                        .FindAsync(u => u.Username == dto.Username && u.Id != dto.Id, cancellationToken);

                    if (usernameConflict.Any())
                        return Result.Failure($"'{dto.Username}' kullanıcı adı başka bir kullanıcı tarafından kullanılıyor.");
                }

                dto.ApplyToEntity(entity);

                // Şifre sadece doluysa güncelle (boş bırakılırsa mevcut hash korunur)
                if (!string.IsNullOrWhiteSpace(dto.Password))
                {
                    entity.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);
                }

                entity.ModifiedDate = DateTime.UtcNow;
                _unitOfWork.Repository<User>().Update(entity);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                return Result.Success();
            }
            catch (Exception ex)
            {
                return Result.Failure($"Kullanıcı güncellenirken hata oluştu: {ex.Message}");
            }
        }

        public async Task<Result> DeactivateAsync(int id, CancellationToken cancellationToken = default)
        {
            try
            {
                var entity = await _unitOfWork.Repository<User>()
                    .GetByIdAsync(id, cancellationToken);

                if (entity is null)
                    return Result.Failure($"Deaktive edilecek kullanıcı bulunamadı (ID: {id})");

                entity.IsActive = false;
                entity.ModifiedDate = DateTime.UtcNow;

                _unitOfWork.Repository<User>().Update(entity);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                return Result.Success();
            }
            catch (Exception ex)
            {
                return Result.Failure($"Kullanıcı deaktive edilirken hata oluştu: {ex.Message}");
            }
        }
    }
}
