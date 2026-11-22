using System;
using System.Collections.Generic;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models.Common;

namespace KamatekCrm.Shared.Models
{
    /// <summary>
    /// Rapor şablonu
    /// </summary>
    public class ReportTemplate : BaseEntity
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public ReportType Type { get; set; }
        public string QueryJson { get; set; } = "{}";
        public bool IsDefault { get; set; }
        public bool IsActive { get; set; } = true;
    }

    /// <summary>
    /// Rapor parametreleri
    /// </summary>
    public class ReportParameters
    {
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int? CustomerId { get; set; }
        public int? ProductId { get; set; }
        public int? UserId { get; set; }
        public int? WarehouseId { get; set; }
        public string? GroupBy { get; set; }
        public bool IncludeDetails { get; set; }
        public int? TopN { get; set; }
    }

    /// <summary>
    /// Rapor sonucu
    /// </summary>
    public class ReportResult
    {
        public string Title { get; set; } = string.Empty;
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public List<Dictionary<string, object>> Rows { get; set; } = new();
        public List<string> Columns { get; set; } = new();
        public decimal? TotalAmount { get; set; }
        public int TotalCount { get; set; }
        public Dictionary<string, decimal> Summary { get; set; } = new();
    }
}
