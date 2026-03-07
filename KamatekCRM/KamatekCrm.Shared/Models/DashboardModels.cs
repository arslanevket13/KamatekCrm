using System;
using System.Collections.Generic;

namespace KamatekCrm.Shared.Models
{
    public class DashboardSummaryDto
    {
        public List<LowStockItemDto> LowStockProducts { get; set; } = new();
        public List<TodayJobItemDto> TodaysJobs { get; set; } = new();
        public List<ReadyRepairItemDto> ReadyToDeliverRepairs { get; set; } = new();
        public DashboardFinancialDto Financials { get; set; } = new();
        public DashboardCustomerStatsDto CustomerStats { get; set; } = new();
        public DashboardSalesReportsDto SalesReports { get; set; } = new();
        public DashboardChartDataDto ChartData { get; set; } = new();
    }

    public class LowStockItemDto
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int CurrentStock { get; set; }
        public int MinStockLevel { get; set; }
        public string UrgencyLevel { get; set; } = string.Empty;
    }

    public class TodayJobItemDto
    {
        public int JobId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string ScheduledTime { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
    }

    public class ReadyRepairItemDto
    {
        public int JobId { get; set; }
        public string TicketNo { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string DeviceInfo { get; set; } = string.Empty;
        public int DaysWaiting { get; set; }
        public string CustomerPhone { get; set; } = string.Empty;
    }

    public class DashboardFinancialDto
    {
        public decimal DailyIncome { get; set; }
        public decimal DailyExpense { get; set; }
        public decimal MonthlySalesTotal { get; set; }
        public int MonthlySalesCount { get; set; }
        public int MonthlyJobsCompleted { get; set; }
        public int ActiveJobsCount { get; set; }
    }

    public class DashboardCustomerStatsDto
    {
        public int TotalCustomers { get; set; }
        public int NewCustomersThisMonth { get; set; }
        public int VipCustomers { get; set; }
        public int UpcomingBirthdays { get; set; }
        public List<Customer> BirthdayCustomers { get; set; } = new();
    }

    public class DashboardSalesReportsDto
    {
        public decimal TodaySalesTotal { get; set; }
        public int TodaySalesCount { get; set; }
        public decimal WeekSalesTotal { get; set; }
        public decimal AverageSaleAmount { get; set; }
    }

    public class DashboardChartDataDto
    {
        public List<WeeklyTrendItemDto> WeeklyTrend { get; set; } = new();
        public List<JobCategoryItemDto> JobCategoryDistribution { get; set; } = new();
    }

    public class WeeklyTrendItemDto
    {
        public string DayName { get; set; } = string.Empty;
        public decimal Income { get; set; }
        public int CompletedJobs { get; set; }
    }

    public class JobCategoryItemDto
    {
        public string Category { get; set; } = string.Empty;
        public int Count { get; set; }
    }
}
