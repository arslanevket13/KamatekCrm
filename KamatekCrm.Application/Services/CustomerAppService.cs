using KamatekCrm.ApplicationCore.Common;
using KamatekCrm.ApplicationCore.DTOs.Customers;
using KamatekCrm.ApplicationCore.Interfaces;
using KamatekCrm.Shared.Exceptions;
using KamatekCrm.Shared.Models;
using KamatekCrm.Shared.Repositories;

namespace KamatekCrm.ApplicationCore.Services
{
    /// <summary>
    /// Müşteri iş süreçlerinin somut Application servisi.
    /// IUnitOfWork ve IRepository üzerinden veri erişimi sağlar;
    /// doğrudan DbContext veya EF Core tiplerine erişmez.
    /// </summary>
    public class CustomerAppService : ICustomerAppService
    {
        private readonly IUnitOfWork _unitOfWork;

        public CustomerAppService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        }

        public async Task<Result<List<CustomerListItemDto>>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var customers = await _unitOfWork.Repository<Customer>()
                    .GetAllAsync(cancellationToken);

                var dtos = customers
                    .Where(c => !c.IsDeleted)
                    .Select(c => c.ToListItemDto())
                    .ToList();

                return Result.Success(dtos);
            }
            catch (Exception ex)
            {
                return Result.Failure<List<CustomerListItemDto>>($"Müşteriler yüklenirken hata oluştu: {ex.Message}");
            }
        }

        public async Task<Result<CustomerDetailDto>> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            try
            {
                var customer = await _unitOfWork.Repository<Customer>()
                    .GetByIdAsync(id, cancellationToken);

                if (customer is null || customer.IsDeleted)
                    return Result.Failure<CustomerDetailDto>($"Müşteri bulunamadı (ID: {id})");

                return Result.Success(customer.ToDetailDto());
            }
            catch (Exception ex)
            {
                return Result.Failure<CustomerDetailDto>($"Müşteri detayı yüklenirken hata oluştu: {ex.Message}");
            }
        }

        public async Task<Result<List<CustomerListItemDto>>> SearchAsync(string searchTerm, CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(searchTerm))
                    return await GetAllAsync(cancellationToken);

                var term = searchTerm.Trim().ToLowerInvariant();

                var customers = await _unitOfWork.Repository<Customer>()
                    .FindAsync(c =>
                        !c.IsDeleted &&
                        (c.FullName.ToLower().Contains(term) ||
                         c.CustomerCode.ToLower().Contains(term) ||
                         c.PhoneNumber.Contains(term) ||
                         (c.Email != null && c.Email.ToLower().Contains(term)) ||
                         c.City.ToLower().Contains(term)),
                        cancellationToken);

                var dtos = customers.Select(c => c.ToListItemDto()).ToList();
                return Result.Success(dtos);
            }
            catch (Exception ex)
            {
                return Result.Failure<List<CustomerListItemDto>>($"Müşteri araması sırasında hata oluştu: {ex.Message}");
            }
        }

        public async Task<Result<int>> CreateAsync(CustomerCreateUpdateDto dto, CancellationToken cancellationToken = default)
        {
            try
            {
                // İş kuralı: Müşteri kodu benzersiz olmalı
                var existing = await _unitOfWork.Repository<Customer>()
                    .FindAsync(c => c.CustomerCode == dto.CustomerCode && !c.IsDeleted, cancellationToken);

                if (existing.Any())
                    return Result.Failure<int>($"'{dto.CustomerCode}' müşteri kodu zaten kullanımda.");

                var entity = new Customer();
                dto.ApplyToEntity(entity);
                entity.CreatedDate = DateTime.UtcNow;

                await _unitOfWork.Repository<Customer>().AddAsync(entity, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                return Result.Success(entity.Id);
            }
            catch (Exception ex)
            {
                return Result.Failure<int>($"Müşteri oluşturulurken hata oluştu: {ex.Message}");
            }
        }

        public async Task<Result> UpdateAsync(CustomerCreateUpdateDto dto, CancellationToken cancellationToken = default)
        {
            try
            {
                var entity = await _unitOfWork.Repository<Customer>()
                    .GetByIdAsync(dto.Id, cancellationToken);

                if (entity is null || entity.IsDeleted)
                    return Result.Failure($"Güncellenecek müşteri bulunamadı (ID: {dto.Id})");

                // İş kuralı: Müşteri kodu değiştiyse benzersizliğini kontrol et
                if (entity.CustomerCode != dto.CustomerCode)
                {
                    var codeConflict = await _unitOfWork.Repository<Customer>()
                        .FindAsync(c => c.CustomerCode == dto.CustomerCode && c.Id != dto.Id && !c.IsDeleted, cancellationToken);

                    if (codeConflict.Any())
                        return Result.Failure($"'{dto.CustomerCode}' müşteri kodu başka bir müşteri tarafından kullanılıyor.");
                }

                dto.ApplyToEntity(entity);
                entity.ModifiedDate = DateTime.UtcNow;

                _unitOfWork.Repository<Customer>().Update(entity);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                return Result.Success();
            }
            catch (Exception ex)
            {
                return Result.Failure($"Müşteri güncellenirken hata oluştu: {ex.Message}");
            }
        }

        public async Task<Result> DeleteAsync(int id, CancellationToken cancellationToken = default)
        {
            try
            {
                var entity = await _unitOfWork.Repository<Customer>()
                    .GetByIdAsync(id, cancellationToken);

                if (entity is null || entity.IsDeleted)
                    return Result.Failure($"Silinecek müşteri bulunamadı (ID: {id})");

                // İş kuralı: Bağlı iş emirleri varsa silme
                var relatedJobs = await _unitOfWork.Repository<ServiceJob>()
                    .FindAsync(j => j.CustomerId == id, cancellationToken);

                if (relatedJobs.Any())
                    throw new ReferentialIntegrityException("Müşteri", id, "İş Emri", relatedJobs.Count);

                // Soft Delete
                entity.IsDeleted = true;
                entity.DeletedAt = DateTime.UtcNow;
                entity.ModifiedDate = DateTime.UtcNow;

                _unitOfWork.Repository<Customer>().Update(entity);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                return Result.Success();
            }
            catch (ReferentialIntegrityException)
            {
                throw; // Domain istisnalarını yukarı fırlat — Global Exception Handler yakalar
            }
            catch (Exception ex)
            {
                return Result.Failure($"Müşteri silinirken hata oluştu: {ex.Message}");
            }
        }
    }
}
