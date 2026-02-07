using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text.Json;
using KamatekCrm.Shared.Enums;

namespace KamatekCrm.Shared.Models
{
    public class ServiceJob
    {
        [Key]
        public int Id { get; set; }
        [Required]
        public int CustomerId { get; set; }
        public int? ServiceProjectId { get; set; }
        public int? CustomerAssetId { get; set; }
        [Required]
        public WorkOrderType WorkOrderType { get; set; } = WorkOrderType.Repair;
        
        // Deprecated fields might be missing in valid code, adding to be safe
        public JobCategory JobCategory { get; set; } = JobCategory.Other;
        public string CategoriesJson { get; set; } = "[]"; 

        [Required]
        [MaxLength(1000)]
        public string Description { get; set; } = string.Empty;
        [Required]
        public JobStatus Status { get; set; } = JobStatus.Pending;
        [Required]
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public DateTime? CompletedDate { get; set; }
        public DateTime? ScheduledDate { get; set; }
        [MaxLength(100)]
        public string? AssignedTechnician { get; set; }
        public int? AssignedUserId { get; set; }
        [ForeignKey(nameof(AssignedUserId))]
        public virtual User? AssignedUser { get; set; }
        public JobPriority Priority { get; set; } = JobPriority.Normal;

        public ServiceJobType ServiceJobType { get; set; } = ServiceJobType.Fault;
        public WorkflowStatus WorkflowStatus { get; set; } = WorkflowStatus.Draft;
        public bool IsStockReserved { get; set; } = false;
        public bool IsStockDeducted { get; set; } = false;
        public DateTime? ProposalSentDate { get; set; }
        public DateTime? ApprovalDate { get; set; }
        public string? ProposalNotes { get; set; }

        public string? DeviceBrand { get; set; }
        public string? DeviceModel { get; set; }
        public string? SerialNumber { get; set; }
        public string? Accessories { get; set; }
        public string? PhysicalCondition { get; set; }
        public RepairStatus RepairStatus { get; set; } = RepairStatus.Registered;
        public string? PhotoPathsJson { get; set; }

        public decimal Price { get; set; } = 0;
        public decimal LaborCost { get; set; } = 0;
        public decimal DiscountAmount { get; set; } = 0;

        [ForeignKey(nameof(CustomerId))]
        public virtual Customer Customer { get; set; } = null!;
        [ForeignKey(nameof(ServiceProjectId))]
        public virtual ServiceProject? ServiceProject { get; set; }
        [ForeignKey(nameof(CustomerAssetId))]
        public virtual CustomerAsset? CustomerAsset { get; set; }
        
        public virtual System.Collections.Generic.ICollection<ServiceJobItem> ServiceJobItems { get; set; } = new System.Collections.Generic.List<ServiceJobItem>();
        
        [NotMapped]
        public virtual System.Collections.Generic.ICollection<ServiceJobItem> Items => ServiceJobItems;

        public virtual System.Collections.Generic.ICollection<PurchaseOrder> PurchaseOrders { get; set; } = new System.Collections.Generic.List<PurchaseOrder>();
        
        #region Computed Properties

        [NotMapped]
        public bool IsWaitingForParts => Status == JobStatus.WaitingForParts;

        [NotMapped]
        public bool HasPendingPurchaseOrder => PurchaseOrders?.Any(p =>
            p.Status == PurchaseStatus.Pending ||
            p.Status == PurchaseStatus.Ordered ||
            p.Status == PurchaseStatus.Shipped) ?? false;

        [NotMapped]
        public string StatusDisplay => Status switch
        {
            JobStatus.Pending => "⏳ Bekliyor",
            JobStatus.InProgress => "🔵 Devam Ediyor",
            JobStatus.WaitingForParts => "📦 Parça Bekleniyor",
            JobStatus.WaitingForApproval => "✋ Onay Bekleniyor",
            JobStatus.Completed => "✅ Tamamlandı",
            JobStatus.Cancelled => "❌ İptal",
            _ => Status.ToString()
        };

        [NotMapped]
        public string WorkOrderTypeDisplay => WorkOrderType switch
        {
            WorkOrderType.Repair => "🔧 Arıza",
            WorkOrderType.Installation => "🏗️ Kurulum",
            WorkOrderType.Maintenance => "🛠️ Bakım",
            WorkOrderType.Inspection => "🔍 Keşif",
            WorkOrderType.Replacement => "🔄 Değiştirme",
            _ => WorkOrderType.ToString()
        };

        [NotMapped]
        public bool BelongsToProject => ServiceProjectId.HasValue;

        [NotMapped]
        public System.Collections.Generic.List<string> PhotoPathsList =>
            string.IsNullOrEmpty(PhotoPathsJson)
                ? new System.Collections.Generic.List<string>()
                : System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.List<string>>(PhotoPathsJson) ?? new System.Collections.Generic.List<string>();

        [NotMapped]
        public bool HasPhotos => !string.IsNullOrEmpty(PhotoPathsJson);

        [NotMapped]
        public decimal TotalAmount => (ServiceJobItems?.Sum(x => x.UnitPrice * x.QuantityUsed) ?? 0) + LaborCost - DiscountAmount;

        #endregion
    }
}
