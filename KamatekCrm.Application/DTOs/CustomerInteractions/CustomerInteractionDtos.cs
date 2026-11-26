using System;
using System.Collections.Generic;
using System.Linq;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;

namespace KamatekCrm.ApplicationCore.DTOs.CustomerInteractions
{
    public class CustomerInteractionDto
    {
        public int Id { get; set; }
        public string InteractionNumber { get; set; } = string.Empty;
        public int? CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string CallerName { get; set; } = string.Empty;
        public string CallerPhone { get; set; } = string.Empty;
        public string NormalizedPhone { get; set; } = string.Empty;
        public InteractionChannel Channel { get; set; }
        public InteractionRequestType RequestType { get; set; }
        public string Subject { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public string? DetailedNotes { get; set; }
        public InteractionPriority Priority { get; set; }
        public InteractionStatus Status { get; set; }
        public DateTime InteractionDate { get; set; }
        public DateTime Date => InteractionDate;
        public string CreatedByUserId { get; set; } = string.Empty;
        public string CreatedByUsername { get; set; } = string.Empty;
        public int? AssignedToUserId { get; set; }
        public string? AssignedToUsername { get; set; }
        public int? AssignedUserId { get => AssignedToUserId; set => AssignedToUserId = value; }
        public bool RequiresFollowUp { get; set; }
        public DateTime? FollowUpDate { get; set; }
        public bool RequiresManagerAttention { get; set; }
        public string? ManagerNotes { get; set; }
        public DateTime? CompletedDate { get; set; }
        public string? ResolutionNotes { get; set; }
        public string? RelatedEntityType { get; set; }
        public int? RelatedEntityId { get; set; }
        public string? RelatedEntityNumber { get; set; }
        public bool IsDraft { get; set; }
        public DateTime CreatedDate { get; set; }
        public bool IsOverdue { get; set; }
        public string? OverdueText { get; set; }
        public int PriorityScore { get; set; }
        public string PriorityLevel { get; set; } = "Normal";
        public string? SourceType { get; set; } = "Interaction";
        public int? SourceEntityId { get; set; }

        // Faz 5 Çok Katmanlı Defter Alanları
        public InteractionDirection Direction { get; set; } = InteractionDirection.InboundRequest;
        public BallInCourtStatus BallInCourt { get; set; } = BallInCourtStatus.Us;
        public DateTime? SnoozedUntilUtc { get; set; }
        public string? LinkedEntityType { get; set; }
        public int? LinkedEntityId { get; set; }
        public string EntriesJson { get; set; } = "[]";
        public string ChecklistJson { get; set; } = "[]";
        public List<InteractionThreadEntry> ThreadEntries { get; set; } = new();
        public List<InteractionChecklistItem> ChecklistItems { get; set; } = new();

        public bool IsSnoozed => SnoozedUntilUtc.HasValue && SnoozedUntilUtc.Value > DateTime.UtcNow;
        public int OverdueDays
        {
            get
            {
                if (!FollowUpDate.HasValue || FollowUpDate.Value >= DateTime.UtcNow ||
                    Status == InteractionStatus.Completed || Status == InteractionStatus.Cancelled)
                    return 0;
                return (int)Math.Max(1, (DateTime.UtcNow - FollowUpDate.Value).TotalDays);
            }
        }

        public string DirectionText => Direction switch
        {
            InteractionDirection.InboundRequest => "Müşteri Talebi",
            InteractionDirection.OutboundInfoRequest => "Bilgi/Onay İstendi",
            InteractionDirection.InternalNote => "İç Not",
            _ => "Talebi"
        };

        public string BallInCourtText => BallInCourt switch
        {
            BallInCourtStatus.Us => "Top Bizde",
            BallInCourtStatus.Customer => "Müşteride",
            BallInCourtStatus.Manager => "Patronda",
            _ => "Top Bizde"
        };

        public int CompletedChecklistCount => ChecklistItems?.Count(x => x.IsCompleted) ?? 0;
        public int TotalChecklistCount => ChecklistItems?.Count ?? 0;
        public string ChecklistProgressText => TotalChecklistCount > 0 ? $"{CompletedChecklistCount}/{TotalChecklistCount}" : string.Empty;
        public string LastThreadEntryPreview => ThreadEntries?.LastOrDefault()?.Content ?? Summary;
        public string? LastThreadEntryOutcome => ThreadEntries?.LastOrDefault()?.CallOutcome;
    }

    public class CreateCustomerInteractionDto
    {
        public Guid IdempotencyKey { get; set; } = Guid.NewGuid();
        public int? CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string CallerName { get; set; } = string.Empty;
        public string CallerPhone { get; set; } = string.Empty;
        public InteractionChannel Channel { get; set; } = InteractionChannel.Phone;
        public InteractionRequestType RequestType { get; set; } = InteractionRequestType.PriceQuote;
        public string Subject { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public string? DetailedNotes { get; set; }
        public InteractionPriority Priority { get; set; } = InteractionPriority.Normal;
        public int? AssignedToUserId { get; set; }
        public string? AssignedToUsername { get; set; }
        public int? AssignedUserId { get => AssignedToUserId; set => AssignedToUserId = value; }
        public bool RequiresFollowUp { get; set; }
        public DateTime? FollowUpDate { get; set; }
        public bool RequiresManagerAttention { get; set; }
        public string? ManagerNotes { get; set; }
        public string? RelatedEntityType { get; set; }
        public int? RelatedEntityId { get; set; }
        public string? RelatedEntityNumber { get; set; }

        // Faz 5 Yeni Alanlar
        public InteractionDirection Direction { get; set; } = InteractionDirection.InboundRequest;
        public BallInCourtStatus BallInCourt { get; set; } = BallInCourtStatus.Us;
        public DateTime? SnoozedUntilUtc { get; set; }
        public string? LinkedEntityType { get; set; }
        public int? LinkedEntityId { get; set; }
        public List<string>? InitialChecklist { get; set; }
        public string? InitialThreadNote { get; set; }
    }

    public class UpdateCustomerInteractionStatusDto
    {
        public int InteractionId { get; set; }
        public InteractionStatus NewStatus { get; set; }
        public int? NewAssignedToUserId { get; set; }
        public string? NewAssignedToUsername { get; set; }
        public string? Reason { get; set; }
        public string? ResolutionNotes { get; set; }
    }

    public class CustomerInteractionFilterDto
    {
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public InteractionRequestType? RequestType { get; set; }
        public InteractionStatus? Status { get; set; }
        public InteractionPriority? Priority { get; set; }
        public int? AssignedToUserId { get; set; }
        public int? AssignedUserId { get => AssignedToUserId; set => AssignedToUserId = value; }
        public int? CustomerId { get; set; }
        public bool? RequiresManagerAttention { get; set; }
        public bool? RequiresFollowUp { get; set; }
        public bool OnlyOverdue { get; set; }
        public bool IsOverdueOnly { get => OnlyOverdue; set => OnlyOverdue = value; }
        public DateTime? FollowUpStartDate { get; set; }
        public DateTime? FollowUpEndDate { get; set; }
        public BallInCourtStatus? BallInCourt { get; set; }
        public string? SearchText { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }

    public class CustomerInteractionSummaryDto
    {
        public int TotalInteractionsCount { get; set; }
        public int FollowUpRequiredCount { get; set; }
        public int OverdueCount { get; set; }
        public int ManagerAgendaCount { get; set; }
        public int PriceQuoteRequestsCount { get; set; }
        public int DiscoveryRequestsCount { get; set; }
        public int ServiceStatusRequestsCount { get; set; }
    }

    public class CustomerPhoneMatchResultDto
    {
        public int? CustomerId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? FullAddress { get; set; }
        public int ActiveServiceJobsCount { get; set; }
        public int ActiveQuotesCount { get; set; }
        public int PendingInteractionsCount { get; set; }
    }

    public class AddThreadEntryDto
    {
        public ThreadEntryType EntryType { get; set; } = ThreadEntryType.Note;
        public string Content { get; set; } = string.Empty;
        public string? CallOutcome { get; set; }
        public BallInCourtStatus? UpdateBallInCourt { get; set; }
        public DateTime? UpdateSnoozedUntilUtc { get; set; }
        public int? UpdateAssignedToUserId { get; set; }
        public string? UpdateAssignedToUsername { get; set; }
        public BallInCourtStatus? NewBallInCourt { get => UpdateBallInCourt; set => UpdateBallInCourt = value; }
        public int? NewAssignedUserId { get => UpdateAssignedToUserId; set => UpdateAssignedToUserId = value; }
    }

    public class CallerContextDto
    {
        public int? CustomerId { get; set; }
        public string? FullName { get; set; }
        public string? CustomerName => FullName;
        public int OpenJobsCount => OpenServiceJobsCount;
        public decimal Balance => CurrentBalance;
        private string? _matchedPhone;
        public string PhoneNumber { get; set; } = string.Empty;
        public string MatchedPhone
        {
            get => string.IsNullOrWhiteSpace(_matchedPhone) ? PhoneNumber : _matchedPhone;
            set => _matchedPhone = value;
        }
        public string? Email { get; set; }
        public string? FullAddress { get; set; }
        public bool IsRegisteredCustomer => CustomerId.HasValue && CustomerId.Value > 0;
        public decimal CurrentBalance { get; set; }
        public int OpenServiceJobsCount { get; set; }
        public int PendingQuotesCount { get; set; }
        public int PendingInteractionsCount { get; set; }
        public List<CallerRecentInteractionDto> RecentInteractions { get; set; } = new();
    }

    public class CallerRecentInteractionDto
    {
        public int Id { get; set; }
        public string InteractionNumber { get; set; } = string.Empty;
        public DateTime InteractionDate { get; set; }
        public DateTime Date => InteractionDate;
        public string Subject { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public InteractionStatus Status { get; set; }
        public InteractionRequestType RequestType { get; set; }
        public string? CreatedByUsername { get; set; }
    }

    public class CalendarDaySummaryDto
    {
        public int Day { get; set; }
        public DateTime Date { get; set; }
        public int TotalCount { get; set; }
        public int OverdueCount { get; set; }
        public bool HasManagerAgenda { get; set; }
    }
}
