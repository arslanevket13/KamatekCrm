using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using KamatekCrm.ApplicationCore.Common;
using KamatekCrm.ApplicationCore.DTOs.CustomerInteractions;
using KamatekCrm.ApplicationCore.Interfaces;
using KamatekCrm.ApplicationCore.Security;
using KamatekCrm.Infrastructure.Data;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace KamatekCrm.Infrastructure.Services
{
    public class CustomerInteractionCommandService : ICustomerInteractionCommandService
    {
        private const string AuditEntity = "CustomerInteraction";
        private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
        private readonly IApplicationAuthorizationService _authorization;
        private readonly ICurrentUserContext _currentUser;

        public CustomerInteractionCommandService(
            IDbContextFactory<AppDbContext> dbContextFactory,
            IApplicationAuthorizationService authorization,
            ICurrentUserContext currentUser)
        {
            _dbContextFactory = dbContextFactory;
            _authorization = authorization;
            _currentUser = currentUser;
        }

        public async Task<Result<CustomerInteractionDto>> CreateAsync(
            CreateCustomerInteractionDto dto,
            CancellationToken cancellationToken = default)
        {
            var auth = _authorization.Authorize(ApplicationPermission.ViewCustomerContactData);
            if (auth.IsFailure) return Result.Failure<CustomerInteractionDto>(auth.Error);

            if (dto.IdempotencyKey == Guid.Empty)
                return Result.Failure<CustomerInteractionDto>("İşlem anahtarı geçerli değil.");
            if (string.IsNullOrWhiteSpace(dto.CallerName))
                return Result.Failure<CustomerInteractionDto>("Arayan kişi adı gereklidir.");
            if (string.IsNullOrWhiteSpace(dto.CallerPhone))
                return Result.Failure<CustomerInteractionDto>("Arayan telefon numarası gereklidir.");
            if (string.IsNullOrWhiteSpace(dto.Subject))
                return Result.Failure<CustomerInteractionDto>("Görüşme konusu gereklidir.");
            if (string.IsNullOrWhiteSpace(dto.Summary))
                return Result.Failure<CustomerInteractionDto>("Görüşme özeti gereklidir.");

            if (dto.RequiresFollowUp && !dto.FollowUpDate.HasValue)
                return Result.Failure<CustomerInteractionDto>("Takip gerektiren görüşmelerde takip tarihi seçilmelidir.");

            await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

            return await context.ExecuteInTransactionAsync(async transaction =>
            {
                // Idempotency kontrolü
                var reference = $"INTERACTION-CREATE:{dto.IdempotencyKey:N}";
                var previousLog = await context.ActivityLogs.AsNoTracking()
                    .SingleOrDefaultAsync(log => log.EntityName == AuditEntity && log.ReferenceId == reference, cancellationToken);

                if (previousLog is not null && int.TryParse(previousLog.RecordId, out var previousId))
                {
                    var existing = await context.CustomerInteractions
                        .AsNoTracking()
                        .Include(i => i.Customer)
                        .Include(i => i.AssignedToUser)
                        .SingleOrDefaultAsync(i => i.Id == previousId, cancellationToken);

                    if (existing is not null)
                    {
                        return Result.Success(MapToDto(existing));
                    }
                }

                var normalizedPhone = PhoneNormalizationHelper.NormalizePhoneNumber(dto.CallerPhone);
                var interactionNumber = $"TALEP-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..24].ToUpper();

                var interaction = new CustomerInteraction
                {
                    InteractionNumber = interactionNumber,
                    CustomerId = dto.CustomerId,
                    CustomerName = string.IsNullOrWhiteSpace(dto.CustomerName) ? dto.CallerName : dto.CustomerName.Trim(),
                    CallerName = dto.CallerName.Trim(),
                    CallerPhone = dto.CallerPhone.Trim(),
                    NormalizedPhone = normalizedPhone,
                    Channel = dto.Channel,
                    RequestType = dto.RequestType,
                    Subject = dto.Subject.Trim(),
                    Summary = dto.Summary.Trim(),
                    DetailedNotes = dto.DetailedNotes?.Trim(),
                    Priority = dto.Priority,
                    Status = dto.RequiresFollowUp ? InteractionStatus.Scheduled : InteractionStatus.New,
                    InteractionDate = DateTime.UtcNow,
                    CreatedByUserId = _currentUser.UserId.ToString(),
                    CreatedByUsername = _currentUser.Username,
                    AssignedToUserId = dto.AssignedToUserId,
                    AssignedToUsername = dto.AssignedToUsername,
                    RequiresFollowUp = dto.RequiresFollowUp,
                    FollowUpDate = dto.FollowUpDate,
                    RequiresManagerAttention = dto.RequiresManagerAttention,
                    ManagerNotes = dto.ManagerNotes?.Trim(),
                    RelatedEntityType = dto.RelatedEntityType,
                    RelatedEntityId = dto.RelatedEntityId,
                    RelatedEntityNumber = dto.RelatedEntityNumber,
                    CreatedDate = DateTime.UtcNow,
                    IsActive = true
                };

                context.CustomerInteractions.Add(interaction);
                await context.SaveChangesAsync(cancellationToken);

                // Initial history entry
                context.CustomerInteractionHistories.Add(new CustomerInteractionHistory
                {
                    CustomerInteractionId = interaction.Id,
                    PreviousStatus = InteractionStatus.New,
                    NewStatus = interaction.Status,
                    NewAssignedToUsername = dto.AssignedToUsername,
                    ChangedByUsername = _currentUser.Username,
                    ChangedAt = DateTime.UtcNow,
                    Reason = "Görüşme kaydı oluşturuldu."
                });

                // Customer Activity Timeline entry if customer exists
                if (dto.CustomerId.HasValue && dto.CustomerId > 0)
                {
                    context.CustomerActivities.Add(new CustomerActivity
                    {
                        CustomerId = dto.CustomerId,
                        Type = ActivityType.CallMade,
                        Description = $"Görüşme Kaydedildi ({interaction.Subject}): {interaction.Summary}",
                        RelatedId = interaction.Id,
                        RelatedType = AuditEntity,
                        CreatedBy = _currentUser.Username,
                        CreatedDate = DateTime.UtcNow
                    });
                }

                // Audit Log
                context.ActivityLogs.Add(new ActivityLog
                {
                    UserId = _currentUser.UserId,
                    Username = _currentUser.Username,
                    Action = "CustomerInteractionCreated",
                    ActionType = "Create",
                    EntityName = AuditEntity,
                    RecordId = interaction.Id.ToString(),
                    ReferenceId = reference,
                    Description = $"{interaction.InteractionNumber} numaralı görüşme kaydı oluşturuldu ({interaction.CallerName}).",
                    Timestamp = DateTime.UtcNow,
                    UserAgent = "WPF Client"
                });

                await context.SaveChangesAsync(cancellationToken);

                return Result.Success(MapToDto(interaction));
            }, cancellationToken: cancellationToken);
        }

        public async Task<Result> UpdateStatusAsync(
            UpdateCustomerInteractionStatusDto dto,
            CancellationToken cancellationToken = default)
        {
            var auth = _authorization.Authorize(ApplicationPermission.ViewCustomerContactData);
            if (auth.IsFailure) return auth;

            await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            return await context.ExecuteInTransactionAsync(async transaction =>
            {
                var interaction = await context.CustomerInteractions
                    .SingleOrDefaultAsync(i => i.Id == dto.InteractionId, cancellationToken);

                if (interaction is null)
                    return Result.Failure($"Görüşme kaydı bulunamadı (ID: {dto.InteractionId}).");

                var oldStatus = interaction.Status;
                var oldAssigned = interaction.AssignedToUsername;

                interaction.Status = dto.NewStatus;
                if (dto.NewAssignedToUserId.HasValue)
                {
                    interaction.AssignedToUserId = dto.NewAssignedToUserId;
                    interaction.AssignedToUsername = dto.NewAssignedToUsername;
                }
                if (!string.IsNullOrWhiteSpace(dto.ResolutionNotes))
                {
                    interaction.ResolutionNotes = dto.ResolutionNotes.Trim();
                }

                if (dto.NewStatus == InteractionStatus.Completed || dto.NewStatus == InteractionStatus.Cancelled)
                {
                    interaction.CompletedDate = DateTime.UtcNow;
                }

                interaction.ModifiedDate = DateTime.UtcNow;

                context.CustomerInteractionHistories.Add(new CustomerInteractionHistory
                {
                    CustomerInteractionId = interaction.Id,
                    PreviousStatus = oldStatus,
                    NewStatus = dto.NewStatus,
                    PreviousAssignedToUsername = oldAssigned,
                    NewAssignedToUsername = interaction.AssignedToUsername,
                    ChangedByUsername = _currentUser.Username,
                    ChangedAt = DateTime.UtcNow,
                    Reason = dto.Reason?.Trim() ?? "Durum güncellendi.",
                    Notes = dto.ResolutionNotes
                });

                await context.SaveChangesAsync(cancellationToken);
                return Result.Success();
            }, cancellationToken: cancellationToken);
        }

        public async Task<Result> AssignUserAsync(
            int interactionId,
            int userId,
            string username,
            CancellationToken cancellationToken = default)
        {
            return await UpdateStatusAsync(new UpdateCustomerInteractionStatusDto
            {
                InteractionId = interactionId,
                NewStatus = InteractionStatus.Assigned,
                NewAssignedToUserId = userId,
                NewAssignedToUsername = username,
                Reason = $"Sorumlu personel atandı: {username}"
            }, cancellationToken);
        }

        public async Task<Result> ConvertToQuoteAsync(
            int interactionId,
            int quoteId,
            string quoteNumber,
            CancellationToken cancellationToken = default)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            var interaction = await context.CustomerInteractions.FindAsync(new object[] { interactionId }, cancellationToken);
            if (interaction is null) return Result.Failure("Görüşme kaydı bulunamadı.");

            interaction.RelatedEntityType = "Quote";
            interaction.RelatedEntityId = quoteId;
            interaction.RelatedEntityNumber = quoteNumber;
            interaction.Status = InteractionStatus.Completed;
            interaction.CompletedDate = DateTime.UtcNow;
            interaction.ResolutionNotes = $"Standart/Proje teklifine dönüştürüldü ({quoteNumber}).";
            interaction.ModifiedDate = DateTime.UtcNow;

            await context.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }

        public async Task<Result> ConvertToServiceJobAsync(
            int interactionId,
            int serviceJobId,
            string jobNo,
            CancellationToken cancellationToken = default)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            var interaction = await context.CustomerInteractions.FindAsync(new object[] { interactionId }, cancellationToken);
            if (interaction is null) return Result.Failure("Görüşme kaydı bulunamadı.");

            interaction.RelatedEntityType = "ServiceJob";
            interaction.RelatedEntityId = serviceJobId;
            interaction.RelatedEntityNumber = jobNo;
            interaction.Status = InteractionStatus.Completed;
            interaction.CompletedDate = DateTime.UtcNow;
            interaction.ResolutionNotes = $"Servis iş emrine dönüştürüldü ({jobNo}).";
            interaction.ModifiedDate = DateTime.UtcNow;

            await context.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }

        public async Task<Result> SaveDraftAsync(string draftJson, CancellationToken cancellationToken = default)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            var reference = $"INTERACTION-DRAFT:{_currentUser.UserId}";
            var log = await context.ActivityLogs.FirstOrDefaultAsync(l => l.ReferenceId == reference, cancellationToken);
            if (log is null)
            {
                context.ActivityLogs.Add(new ActivityLog
                {
                    UserId = _currentUser.UserId,
                    Username = _currentUser.Username,
                    Action = "CustomerInteractionDraftSaved",
                    ActionType = "Draft",
                    EntityName = AuditEntity,
                    RecordId = _currentUser.UserId.ToString(),
                    ReferenceId = reference,
                    Description = draftJson,
                    Timestamp = DateTime.UtcNow
                });
            }
            else
            {
                log.Description = draftJson;
                log.Timestamp = DateTime.UtcNow;
            }

            await context.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }

        public async Task<Result<string>> GetDraftAsync(CancellationToken cancellationToken = default)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            var reference = $"INTERACTION-DRAFT:{_currentUser.UserId}";
            var log = await context.ActivityLogs.AsNoTracking().FirstOrDefaultAsync(l => l.ReferenceId == reference, cancellationToken);
            if (log is null || string.IsNullOrWhiteSpace(log.Description))
                return Result.Success(string.Empty);

            return Result.Success(log.Description);
        }

        public async Task<Result> ClearDraftAsync(CancellationToken cancellationToken = default)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            var reference = $"INTERACTION-DRAFT:{_currentUser.UserId}";
            var log = await context.ActivityLogs.FirstOrDefaultAsync(l => l.ReferenceId == reference, cancellationToken);
            if (log is not null)
            {
                context.ActivityLogs.Remove(log);
                await context.SaveChangesAsync(cancellationToken);
            }
            return Result.Success();
        }

        private static CustomerInteractionDto MapToDto(CustomerInteraction entity)
        {
            return new CustomerInteractionDto
            {
                Id = entity.Id,
                InteractionNumber = entity.InteractionNumber,
                CustomerId = entity.CustomerId,
                CustomerName = entity.CustomerName,
                CallerName = entity.CallerName,
                CallerPhone = entity.CallerPhone,
                NormalizedPhone = entity.NormalizedPhone,
                Channel = entity.Channel,
                RequestType = entity.RequestType,
                Subject = entity.Subject,
                Summary = entity.Summary,
                DetailedNotes = entity.DetailedNotes,
                Priority = entity.Priority,
                Status = entity.Status,
                InteractionDate = entity.InteractionDate,
                CreatedByUserId = entity.CreatedByUserId,
                CreatedByUsername = entity.CreatedByUsername,
                AssignedToUserId = entity.AssignedToUserId,
                AssignedToUsername = entity.AssignedToUsername,
                RequiresFollowUp = entity.RequiresFollowUp,
                FollowUpDate = entity.FollowUpDate,
                RequiresManagerAttention = entity.RequiresManagerAttention,
                ManagerNotes = entity.ManagerNotes,
                CompletedDate = entity.CompletedDate,
                ResolutionNotes = entity.ResolutionNotes,
                RelatedEntityType = entity.RelatedEntityType,
                RelatedEntityId = entity.RelatedEntityId,
                RelatedEntityNumber = entity.RelatedEntityNumber,
                IsDraft = entity.IsDraft,
                CreatedDate = entity.CreatedDate
            };
        }
    }
}
