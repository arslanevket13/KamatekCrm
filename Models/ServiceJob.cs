using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using KamatekCrm.Enums;

namespace KamatekCrm.Models
{
    /// <summary>
    /// İş kaydı entity'si
    /// </summary>
    public class ServiceJob
    {
        /// <summary>
        /// İş ID (Primary Key)
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Müşteri ID (Foreign Key)
        /// </summary>
        [Required]
        public int CustomerId { get; set; }

        /// <summary>
        /// Proje ID (Foreign Key - OPSİYONEL)
        /// Tek başına iş için null, proje altındaki iş için dolu
        /// </summary>
        public int? ServiceProjectId { get; set; }

        /// <summary>
        /// Müşteri Cihazı ID (Foreign Key - OPSİYONEL)
        /// Arıza/bakım işleri için ilgili cihaz
        /// </summary>
        public int? CustomerAssetId { get; set; }

        /// <summary>
        /// İş emri tipi (Arıza, Kurulum, Bakım, vb.)
        /// </summary>
        [Required]
        public WorkOrderType WorkOrderType { get; set; } = WorkOrderType.Repair;

        /// <summary>
        /// İş türü (Kamera, Diafon, Uydu) - DEPRECATED: JobCategory kullanın
        /// </summary>
        public JobType? JobType { get; set; }

        /// <summary>
        /// İş kategorisi (Tek kategori - geriye uyumluluk için)
        /// </summary>
        [Required]
        public JobCategory JobCategory { get; set; }

        /// <summary>
        /// Seçilen kategoriler (JSON array formatında)
        /// Örnek: "[0,2,5]" = CCTV + FireAlarm + AccessControl
        /// </summary>
        [MaxLength(200)]
        public string? CategoriesJson { get; set; }

        /// <summary>
        /// İş detayları JSON formatında (DEPRECATED - artık kullanılmıyor)
        /// </summary>
        public string? JobDetailsJson { get; set; }

        /// <summary>
        /// İş açıklaması/detayı
        /// </summary>
        [Required(ErrorMessage = "İş açıklaması zorunludur")]
        [MaxLength(1000)]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// İş durumu (Beklemede, Devam Ediyor, Parça Bekliyor, Tamamlandı, İptal)
        /// </summary>
        [Required]
        public JobStatus Status { get; set; } = JobStatus.Pending;

        /// <summary>
        /// İş oluşturulma tarihi
        /// </summary>
        [Required]
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        /// <summary>
        /// İş tamamlanma tarihi (İsteğe bağlı)
        /// </summary>
        public DateTime? CompletedDate { get; set; }

        /// <summary>
        /// Planlanan tarih
        /// </summary>
        public DateTime? ScheduledDate { get; set; }

        /// <summary>
        /// Atanan teknisyen
        /// </summary>
        [MaxLength(100)]
        public string? AssignedTechnician { get; set; }

        /// <summary>
        /// Atanan Teknisyen ID (Scheduler)
        /// </summary>
        public int? AssignedUserId { get; set; }

        [ForeignKey(nameof(AssignedUserId))]
        public virtual User? AssignedUser { get; set; }

        /// <summary>
        /// İş önceliği
        /// </summary>
        public JobPriority Priority { get; set; } = JobPriority.Normal;

        #region Advanced Workflow Fields

        /// <summary>
        /// İş tipi (Arıza / Proje)
        /// </summary>
        [Required]
        public ServiceJobType ServiceJobType { get; set; } = ServiceJobType.Fault;

        /// <summary>
        /// Proje iş akışı durumu (Sadece Project tipi için)
        /// </summary>
        public WorkflowStatus WorkflowStatus { get; set; } = WorkflowStatus.Draft;

        /// <summary>
        /// Stok rezerve edildi mi? (Teklif onaylandığında true)
        /// </summary>
        public bool IsStockReserved { get; set; } = false;

        /// <summary>
        /// Stok düşüldü mü? (Final onayında true)
        /// </summary>
        public bool IsStockDeducted { get; set; } = false;

        /// <summary>
        /// Teklif gönderim tarihi
        /// </summary>
        public DateTime? ProposalSentDate { get; set; }

        /// <summary>
        /// Müşteri onay tarihi
        /// </summary>
        public DateTime? ApprovalDate { get; set; }

        /// <summary>
        /// Keşif/Teklif notları
        /// </summary>
        [MaxLength(2000)]
        public string? ProposalNotes { get; set; }

        #endregion

        #region Repair Specific Fields (Cihaz Tamir)

        [MaxLength(100)]
        public string? DeviceBrand { get; set; }

        [MaxLength(100)]
        public string? DeviceModel { get; set; }

        [MaxLength(100)]
        public string? SerialNumber { get; set; }

        /// <summary>
        /// Aksesuarlar (Kablo, Çanta, Adaptör vb.)
        /// </summary>
        [MaxLength(500)]
        public string? Accessories { get; set; }

        /// <summary>
        /// Fiziksel Durum (Çizik, Kırık vb.)
        /// </summary>
        [MaxLength(500)]
        public string? PhysicalCondition { get; set; }

        /// <summary>
        /// Tamir Durumu
        /// </summary>
        public RepairStatus RepairStatus { get; set; } = RepairStatus.Registered;

        /// <summary>
        /// Fotoğraf yolları (JSON List<string>)
        /// </summary>
        public string? PhotoPathsJson { get; set; }

        #endregion

        /// <summary>
        /// İş ücreti/fiyatı
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; } = 0;

        /// <summary>
        /// İşçilik ücreti
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal LaborCost { get; set; } = 0;

        /// <summary>
        /// İndirim tutarı
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountAmount { get; set; } = 0;

        /// <summary>
        /// Toplam tutar (Hesaplanmış - Veritabanında saklanmaz)
        /// Malzeme + İşçilik - İndirim
        /// </summary>
        [NotMapped]
        public decimal TotalAmount => (ServiceJobItems?.Sum(x => x.UnitPrice * x.QuantityUsed) ?? 0) + LaborCost - DiscountAmount;

        #region Navigation Properties

        /// <summary>
        /// İlgili müşteri
        /// </summary>
        [ForeignKey(nameof(CustomerId))]
        public virtual Customer Customer { get; set; } = null!;

        /// <summary>
        /// Ana proje (Opsiyonel)
        /// </summary>
        [ForeignKey(nameof(ServiceProjectId))]
        public virtual ServiceProject? ServiceProject { get; set; }

        /// <summary>
        /// İlgili müşteri cihazı (Opsiyonel)
        /// </summary>
        [ForeignKey(nameof(CustomerAssetId))]
        public virtual CustomerAsset? CustomerAsset { get; set; }

        /// <summary>
        /// Bu işte kullanılan ürünler
        /// </summary>
        public virtual ICollection<ServiceJobItem> ServiceJobItems { get; set; } = new List<ServiceJobItem>();

        /// <summary>
        /// Bu işe bağlı satın alma emirleri
        /// </summary>
        public virtual ICollection<PurchaseOrder> PurchaseOrders { get; set; } = new List<PurchaseOrder>();

        #endregion

        #region Computed Properties

        /// <summary>
        /// Parça bekleniyor mu?
        /// </summary>
        [NotMapped]
        public bool IsWaitingForParts => Status == JobStatus.WaitingForParts;

        /// <summary>
        /// Aktif satın alma emri var mı?
        /// </summary>
        [NotMapped]
        public bool HasPendingPurchaseOrder => PurchaseOrders?.Any(p =>
            p.Status == PurchaseStatus.Pending ||
            p.Status == PurchaseStatus.Ordered ||
            p.Status == PurchaseStatus.Shipped) ?? false;

        /// <summary>
        /// Durum gösterimi
        /// </summary>
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

        /// <summary>
        /// İş emri tipi gösterimi
        /// </summary>
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

        /// <summary>
        /// Bir projeye bağlı mı?
        /// </summary>
        [NotMapped]
        public bool BelongsToProject => ServiceProjectId.HasValue;

        /// <summary>
        /// Fotoğraf listesi (JSON'dan parse edilir)
        /// </summary>
        [NotMapped]
        public System.Collections.Generic.List<string> PhotoPathsList =>
            string.IsNullOrEmpty(PhotoPathsJson)
                ? new System.Collections.Generic.List<string>()
                : System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.List<string>>(PhotoPathsJson) ?? new System.Collections.Generic.List<string>();

        [NotMapped]
        public bool HasPhotos => !string.IsNullOrEmpty(PhotoPathsJson);

        #endregion
    }
}
