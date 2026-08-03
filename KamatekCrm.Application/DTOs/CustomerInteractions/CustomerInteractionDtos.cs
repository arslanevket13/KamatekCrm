using System;
using System.Collections.Generic;
using KamatekCrm.Shared.Enums;

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
        public string CreatedByUserId { get; set; } = string.Empty;
        public string CreatedByUsername { get; set; } = string.Empty;
        public int? AssignedToUserId { get; set; }
        public string? AssignedToUsername { get; set; }
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
        public bool RequiresFollowUp { get; set; }
        public DateTime? FollowUpDate { get; set; }
        public bool RequiresManagerAttention { get; set; }
        public string? ManagerNotes { get; set; }
        public string? RelatedEntityType { get; set; }
        public int? RelatedEntityId { get; set; }
        public string? RelatedEntityNumber { get; set; }
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
        public int? CustomerId { get; set; }
        public bool? RequiresManagerAttention { get; set; }
        public bool? RequiresFollowUp { get; set; }
        public bool OnlyOverdue { get; set; }
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
}
