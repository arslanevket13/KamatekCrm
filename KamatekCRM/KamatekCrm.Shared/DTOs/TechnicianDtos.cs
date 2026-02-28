using System;
using System.Collections.Generic;

namespace KamatekCrm.Shared.DTOs
{
    public class RepairRecord
    {
        public int Id { get; set; }
        public int JobId { get; set; }
        public string Diagnostic { get; set; } = string.Empty;
        public string Symptoms { get; set; } = string.Empty;
        public string Solution { get; set; } = string.Empty;
        public string PartsUsed { get; set; } = string.Empty;
        public decimal LaborHours { get; set; }
        public string TestResult { get; set; } = string.Empty;
        public string Recommendations { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    public class ProjectItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public int? CustomerId { get; set; }
        public string Status { get; set; } = string.Empty;
        public decimal EstimatedCost { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public bool RequiresApproval { get; set; }
        public bool IsApproved { get; set; }
    }

    public class QuoteItem
    {
        public int Id { get; set; }
        public string QuoteNumber { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public int? CustomerId { get; set; }
        public string Status { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public DateTime ValidUntil { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class InstallationItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public int? CustomerId { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string InstallationType { get; set; } = string.Empty;
        public DateTime? ScheduledDate { get; set; }
        public DateTime? CompletedDate { get; set; }
    }

    public class CustomerListItem
    {
        public int Id { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public string? ContactName { get; set; }
        public string? Phone { get; set; }
        public string Status { get; set; } = string.Empty;
        public string StatusColor { get; set; } = "secondary";
    }

    public class ProductListItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Sku { get; set; }
        public decimal Price { get; set; }
        public int StockQuantity { get; set; }
    }

    public class JobListItem
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime Date { get; set; }
    }

    public class DashboardStats
    {
        public int ActiveJobs { get; set; }
        public int CompletedJobs { get; set; }
        public int PendingJobs { get; set; }
        public int SiteVisits { get; set; }
    }
}
