using KamatekCrm.ApplicationCore.Common;
using KamatekCrm.ApplicationCore.DTOs.ServiceJobs;
using KamatekCrm.ApplicationCore.Interfaces;
using KamatekCrm.Shared.Models;
using KamatekCrm.Shared.Repositories;

namespace KamatekCrm.ApplicationCore.Services
{
    /// <summary>
    /// İş emri yönetimi Application servisi.
    /// İlk aşamada (Strangler Fig) yalnızca okuma (query) işlemlerini kapsar.
    /// CRUD işlemleri aşamalı geçişle eklenecektir.
    /// </summary>
    public class ServiceJobAppService : IServiceJobAppService
    {
        private readonly IUnitOfWork _unitOfWork;

        public ServiceJobAppService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        }

        public async Task<Result<List<ServiceJobListItemDto>>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var jobs = await _unitOfWork.Repository<ServiceJob>()
                    .GetAllAsync(cancellationToken);

                var dtos = jobs
                    .Select(j => j.ToListItemDto())
                    .OrderByDescending(j => j.CreatedAt)
                    .ToList();

                return Result.Success(dtos);
            }
            catch (Exception ex)
            {
                return Result.Failure<List<ServiceJobListItemDto>>($"İş emirleri yüklenirken hata oluştu: {ex.Message}");
            }
        }

        public async Task<Result<ServiceJobDetailDto>> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            try
            {
                var job = await _unitOfWork.Repository<ServiceJob>()
                    .GetByIdAsync(id, cancellationToken);

                if (job is null)
                    return Result.Failure<ServiceJobDetailDto>($"İş emri bulunamadı (ID: {id})");

                return Result.Success(job.ToDetailDto());
            }
            catch (Exception ex)
            {
                return Result.Failure<ServiceJobDetailDto>($"İş emri detayı yüklenirken hata oluştu: {ex.Message}");
            }
        }

        public async Task<Result<List<ServiceJobListItemDto>>> GetByCustomerIdAsync(int customerId, CancellationToken cancellationToken = default)
        {
            try
            {
                var jobs = await _unitOfWork.Repository<ServiceJob>()
                    .FindAsync(j => j.CustomerId == customerId, cancellationToken);

                var dtos = jobs
                    .Select(j => j.ToListItemDto())
                    .OrderByDescending(j => j.CreatedAt)
                    .ToList();

                return Result.Success(dtos);
            }
            catch (Exception ex)
            {
                return Result.Failure<List<ServiceJobListItemDto>>($"Müşterinin iş emirleri yüklenirken hata oluştu: {ex.Message}");
            }
        }

        public async Task<Result<List<ServiceJobListItemDto>>> GetByTechnicianIdAsync(int technicianUserId, CancellationToken cancellationToken = default)
        {
            try
            {
                var jobs = await _unitOfWork.Repository<ServiceJob>()
                    .FindAsync(j => j.AssignedUserId == technicianUserId, cancellationToken);

                var dtos = jobs
                    .Select(j => j.ToListItemDto())
                    .OrderByDescending(j => j.CreatedAt)
                    .ToList();

                return Result.Success(dtos);
            }
            catch (Exception ex)
            {
                return Result.Failure<List<ServiceJobListItemDto>>($"Teknisyenin iş emirleri yüklenirken hata oluştu: {ex.Message}");
            }
        }
    }
}
