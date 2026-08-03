using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KamatekCrm.ApplicationCore.Common;
using KamatekCrm.ApplicationCore.DTOs.CustomerInteractions;
using KamatekCrm.ApplicationCore.Interfaces;
using KamatekCrm.Infrastructure.Data;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;
using KamatekCrm.Shared.Models.Common;
using Microsoft.EntityFrameworkCore;

namespace KamatekCrm.Infrastructure.Services
{
    public class CustomerInteractionReadService : ICustomerInteractionReadService
    {
        private readonly IDbContextFactory<AppDbContext> _dbContextFactory;

        public CustomerInteractionReadService(IDbContextFactory<AppDbContext> dbContextFactory)
        {
            _dbContextFactory = dbContextFactory;
        }

        public async Task<Result<CustomerInteractionDto>> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            var item = await context.CustomerInteractions
                .AsNoTracking()
                .SingleOrDefaultAsync(i => i.Id == id, cancellationToken);

            if (item is null)
                return Result.Failure<CustomerInteractionDto>($"Görüşme kaydı bulunamadı (ID: {id}).");

            return Result.Success(MapToDto(item));
        }

        public async Task<Result<PagedResult<CustomerInteractionDto>>> FilterAsync(
            CustomerInteractionFilterDto filter,
            CancellationToken cancellationToken = default)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            var query = context.CustomerInteractions.AsNoTracking().Where(i => i.IsActive);

            if (filter.StartDate.HasValue)
                query = query.Where(i => i.InteractionDate >= filter.StartDate.Value);
            if (filter.EndDate.HasValue)
                query = query.Where(i => i.InteractionDate <= filter.EndDate.Value);

            if (filter.RequestType.HasValue)
                query = query.Where(i => i.RequestType == filter.RequestType.Value);
            if (filter.Status.HasValue)
                query = query.Where(i => i.Status == filter.Status.Value);
            if (filter.Priority.HasValue)
                query = query.Where(i => i.Priority == filter.Priority.Value);
            if (filter.AssignedToUserId.HasValue)
                query = query.Where(i => i.AssignedToUserId == filter.AssignedToUserId.Value);
            if (filter.CustomerId.HasValue)
                query = query.Where(i => i.CustomerId == filter.CustomerId.Value);

            if (filter.RequiresManagerAttention.HasValue)
                query = query.Where(i => i.RequiresManagerAttention == filter.RequiresManagerAttention.Value);
            if (filter.RequiresFollowUp.HasValue)
                query = query.Where(i => i.RequiresFollowUp == filter.RequiresFollowUp.Value);

            if (filter.OnlyOverdue)
            {
                var now = DateTime.UtcNow;
                query = query.Where(i => i.RequiresFollowUp &&
                                         i.FollowUpDate.HasValue &&
                                         i.FollowUpDate.Value < now &&
                                         i.Status != InteractionStatus.Completed &&
                                         i.Status != InteractionStatus.Cancelled);
            }

            if (!string.IsNullOrWhiteSpace(filter.SearchText))
            {
                var search = filter.SearchText.Trim().ToLower();
                query = query.Where(i =>
                    i.CallerName.ToLower().Contains(search) ||
                    i.CallerPhone.Contains(search) ||
                    i.CustomerName.ToLower().Contains(search) ||
                    i.Subject.ToLower().Contains(search) ||
                    i.Summary.ToLower().Contains(search) ||
                    i.InteractionNumber.ToLower().Contains(search));
            }

            var totalCount = await query.CountAsync(cancellationToken);
            var items = await query
                .OrderByDescending(i => i.InteractionDate)
                .Skip((filter.PageNumber - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToListAsync(cancellationToken);

            var dtos = items.Select(MapToDto).ToList();
            return Result.Success(new PagedResult<CustomerInteractionDto>(dtos, totalCount, filter.PageNumber, filter.PageSize));
        }

        public async Task<Result<List<CustomerPhoneMatchResultDto>>> SearchByPhoneAsync(
            string phoneQuery,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(phoneQuery))
                return Result.Success(new List<CustomerPhoneMatchResultDto>());

            var normalized = PhoneNormalizationHelper.NormalizePhoneNumber(phoneQuery);
            var cleanDigits = new string(phoneQuery.Where(char.IsDigit).ToArray());

            await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

            var customers = await context.Customers
                .AsNoTracking()
                .Where(c => c.PhoneNumber.Contains(cleanDigits) ||
                            (normalized.Length > 5 && c.PhoneNumber.Contains(normalized.Substring(normalized.Length - 7))))
                .Take(10)
                .ToListAsync(cancellationToken);

            var results = new List<CustomerPhoneMatchResultDto>();
            foreach (var customer in customers)
            {
                var activeJobs = await context.ServiceJobs
                    .AsNoTracking()
                    .CountAsync(j => j.CustomerId == customer.Id && j.Status != JobStatus.Completed && j.Status != JobStatus.Cancelled, cancellationToken);

                var activeQuotes = await context.Quotes
                    .AsNoTracking()
                    .CountAsync(q => q.CustomerId == customer.Id && q.Status != QuoteStatus.Approved && q.Status != QuoteStatus.Rejected, cancellationToken);

                var pendingInteractions = await context.CustomerInteractions
                    .AsNoTracking()
                    .CountAsync(i => i.CustomerId == customer.Id && i.Status != InteractionStatus.Completed && i.Status != InteractionStatus.Cancelled, cancellationToken);

                results.Add(new CustomerPhoneMatchResultDto
                {
                    CustomerId = customer.Id,
                    FullName = customer.FullName,
                    PhoneNumber = customer.PhoneNumber,
                    Email = customer.Email,
                    FullAddress = customer.FullAddress,
                    ActiveServiceJobsCount = activeJobs,
                    ActiveQuotesCount = activeQuotes,
                    PendingInteractionsCount = pendingInteractions
                });
            }

            return Result.Success(results);
        }

        public async Task<Result<List<CustomerInteractionDto>>> GetByCustomerIdAsync(int customerId, CancellationToken cancellationToken = default)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            var items = await context.CustomerInteractions
                .AsNoTracking()
                .Where(i => i.CustomerId == customerId && i.IsActive)
                .OrderByDescending(i => i.InteractionDate)
                .ToListAsync(cancellationToken);

            return Result.Success(items.Select(MapToDto).ToList());
        }

        public async Task<Result<CustomerInteractionSummaryDto>> GetSummaryMetricsAsync(CancellationToken cancellationToken = default)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            var today = DateTime.UtcNow.Date;
            var now = DateTime.UtcNow;

            var todayQuery = context.CustomerInteractions.AsNoTracking().Where(i => i.IsActive && i.InteractionDate >= today);

            var total = await todayQuery.CountAsync(cancellationToken);
            var followUp = await context.CustomerInteractions.AsNoTracking().CountAsync(i => i.IsActive && i.RequiresFollowUp && i.Status != InteractionStatus.Completed && i.Status != InteractionStatus.Cancelled, cancellationToken);
            var overdue = await context.CustomerInteractions.AsNoTracking().CountAsync(i => i.IsActive && i.RequiresFollowUp && i.FollowUpDate.HasValue && i.FollowUpDate.Value < now && i.Status != InteractionStatus.Completed && i.Status != InteractionStatus.Cancelled, cancellationToken);
            var managerAgenda = await context.CustomerInteractions.AsNoTracking().CountAsync(i => i.IsActive && i.RequiresManagerAttention && i.Status != InteractionStatus.Completed && i.Status != InteractionStatus.Cancelled, cancellationToken);
            var priceQuotes = await todayQuery.CountAsync(i => i.RequestType == InteractionRequestType.PriceQuote, cancellationToken);
            var discoveries = await todayQuery.CountAsync(i => i.RequestType == InteractionRequestType.Discovery, cancellationToken);
            var serviceStatus = await todayQuery.CountAsync(i => i.RequestType == InteractionRequestType.ServiceStatus, cancellationToken);

            return Result.Success(new CustomerInteractionSummaryDto
            {
                TotalInteractionsCount = total,
                FollowUpRequiredCount = followUp,
                OverdueCount = overdue,
                ManagerAgendaCount = managerAgenda,
                PriceQuoteRequestsCount = priceQuotes,
                DiscoveryRequestsCount = discoveries,
                ServiceStatusRequestsCount = serviceStatus
            });
        }

        public async Task<Result<List<CustomerInteractionDto>>> GetManagerAgendaAsync(CancellationToken cancellationToken = default)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            var items = await context.CustomerInteractions
                .AsNoTracking()
                .Where(i => i.IsActive &&
                            (i.RequiresManagerAttention || i.RequestType == InteractionRequestType.ManagerAgenda || i.Priority == InteractionPriority.Critical) &&
                            i.Status != InteractionStatus.Completed &&
                            i.Status != InteractionStatus.Cancelled)
                .OrderByDescending(i => i.Priority)
                .ThenByDescending(i => i.InteractionDate)
                .ToListAsync(cancellationToken);

            return Result.Success(items.Select(MapToDto).ToList());
        }

        public async Task<Result<List<CustomerInteractionDto>>> GetOverdueFollowUpsAsync(CancellationToken cancellationToken = default)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            var now = DateTime.UtcNow;

            var items = await context.CustomerInteractions
                .AsNoTracking()
                .Where(i => i.IsActive &&
                            i.RequiresFollowUp &&
                            i.FollowUpDate.HasValue &&
                            i.FollowUpDate.Value < now &&
                            i.Status != InteractionStatus.Completed &&
                            i.Status != InteractionStatus.Cancelled)
                .OrderBy(i => i.FollowUpDate)
                .ToListAsync(cancellationToken);

            return Result.Success(items.Select(MapToDto).ToList());
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
