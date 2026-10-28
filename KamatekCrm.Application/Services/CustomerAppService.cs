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

                // İlişkili kayıt kontrolü: Geçmiş iş emri, satış, cari hareket veya varlık varsa
                // fiziksel silme yapmayıp veritabanı bütünlüğünü korumak için güvenli soft-delete uygulanır.
                entity.IsDeleted = true;
                entity.DeletedAt = DateTime.UtcNow;
                entity.ModifiedDate = DateTime.UtcNow;

                _unitOfWork.Repository<Customer>().Update(entity);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                return Result.Success();
            }
            catch (Exception ex)
            {
                return Result.Failure($"Müşteri silinirken hata oluştu: {ex.Message}");
            }
        }

        public async Task<Result> MergeCustomersAsync(int sourceCustomerId, int targetCustomerId, CancellationToken cancellationToken = default)
        {
            if (sourceCustomerId == targetCustomerId)
                return Result.Failure("Kaynak ve hedef müşteri aynı olamaz.");

            try
            {
                var sourceCustomer = await _unitOfWork.Repository<Customer>().GetByIdAsync(sourceCustomerId, cancellationToken);
                if (sourceCustomer is null || sourceCustomer.IsDeleted)
                    return Result.Failure($"Kaynak müşteri bulunamadı veya silinmiş (ID: {sourceCustomerId})");

                var targetCustomer = await _unitOfWork.Repository<Customer>().GetByIdAsync(targetCustomerId, cancellationToken);
                if (targetCustomer is null || targetCustomer.IsDeleted)
                    return Result.Failure($"Hedef müşteri bulunamadı veya silinmiş (ID: {targetCustomerId})");

                await _unitOfWork.BeginTransactionAsync(cancellationToken);

                // 1. İş Emirlerini Aktar
                var jobs = await _unitOfWork.Repository<ServiceJob>().FindAsync(j => j.CustomerId == sourceCustomerId, cancellationToken);
                foreach (var job in jobs)
                {
                    job.CustomerId = targetCustomerId;
                    _unitOfWork.Repository<ServiceJob>().Update(job);
                }

                // 2. Finansal Hareketleri (Transactions) Aktar
                var transactions = await _unitOfWork.Repository<Transaction>().FindAsync(t => t.CustomerId == sourceCustomerId, cancellationToken);
                foreach (var tr in transactions)
                {
                    tr.CustomerId = targetCustomerId;
                    _unitOfWork.Repository<Transaction>().Update(tr);
                }

                // 3. Satış Emirlerini (SalesOrders) Aktar
                var sales = await _unitOfWork.Repository<SalesOrder>().FindAsync(s => s.CustomerId == sourceCustomerId, cancellationToken);
                foreach (var s in sales)
                {
                    s.CustomerId = targetCustomerId;
                    _unitOfWork.Repository<SalesOrder>().Update(s);
                }

                // 4. Teklifleri (Quotes) Aktar
                var quotes = await _unitOfWork.Repository<Quote>().FindAsync(q => q.CustomerId == sourceCustomerId, cancellationToken);
                foreach (var q in quotes)
                {
                    q.CustomerId = targetCustomerId;
                    _unitOfWork.Repository<Quote>().Update(q);
                }

                // 5. Müşteri Cihazlarını/Varlıklarını (Assets) Aktar
                var assets = await _unitOfWork.Repository<CustomerAsset>().FindAsync(a => a.CustomerId == sourceCustomerId, cancellationToken);
                foreach (var a in assets)
                {
                    a.CustomerId = targetCustomerId;
                    _unitOfWork.Repository<CustomerAsset>().Update(a);
                }

                // 6. Müşteri Etkileşimlerini (Interactions) Aktar
                var interactions = await _unitOfWork.Repository<CustomerInteraction>().FindAsync(i => i.CustomerId == sourceCustomerId, cancellationToken);
                foreach (var i in interactions)
                {
                    i.CustomerId = targetCustomerId;
                    _unitOfWork.Repository<CustomerInteraction>().Update(i);
                }

                // 7. Müşteri Aktivitelerini (Activities) Aktar
                var activities = await _unitOfWork.Repository<CustomerActivity>().FindAsync(a => a.CustomerId == sourceCustomerId, cancellationToken);
                foreach (var a in activities)
                {
                    a.CustomerId = targetCustomerId;
                    _unitOfWork.Repository<CustomerActivity>().Update(a);
                }

                // 8. Hedef Müşteri Toplamlarını ve Birleştirme Notunu Güncelle
                targetCustomer.TotalSpent += sourceCustomer.TotalSpent;
                targetCustomer.TotalPurchaseCount += sourceCustomer.TotalPurchaseCount;
                targetCustomer.LoyaltyPoints += sourceCustomer.LoyaltyPoints;
                string mergeNote = $"[BİRLEŞTİRME: {DateTime.UtcNow:dd.MM.yyyy HH:mm}] {sourceCustomer.FullName} ({sourceCustomer.CustomerCode}) kartı bu hesaba aktarıldı.";
                targetCustomer.Notes = string.IsNullOrWhiteSpace(targetCustomer.Notes) ? mergeNote : $"{targetCustomer.Notes}\n{mergeNote}";
                targetCustomer.ModifiedDate = DateTime.UtcNow;
                _unitOfWork.Repository<Customer>().Update(targetCustomer);

                // 9. Kaynak Müşteriyi Arşivle (Soft Delete)
                sourceCustomer.IsDeleted = true;
                sourceCustomer.DeletedAt = DateTime.UtcNow;
                sourceCustomer.ModifiedDate = DateTime.UtcNow;
                string sourceNote = $"[BİRLEŞTİRİLDİ: {DateTime.UtcNow:dd.MM.yyyy HH:mm}] Bu kart {targetCustomer.FullName} ({targetCustomer.CustomerCode}) ile birleştirilerek arşivlendi.";
                sourceCustomer.Notes = string.IsNullOrWhiteSpace(sourceCustomer.Notes) ? sourceNote : $"{sourceCustomer.Notes}\n{sourceNote}";
                _unitOfWork.Repository<Customer>().Update(sourceCustomer);

                // 10. Kaydet ve Transaction Commit
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await _unitOfWork.CommitAsync(cancellationToken);

                return Result.Success();
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackAsync(cancellationToken);
                return Result.Failure($"Müşteri birleştirme işlemi başarısız: {ex.Message}");
            }
        }
    }
}
