using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using KamatekCrm.Enums;

namespace KamatekCrm.Models
{
    /// <summary>
    /// Satın alma emri - Tedarikçiden parça siparişi
    /// </summary>
    public class PurchaseOrder
    {
        /// <summary>
        /// Sipariş ID (Primary Key)
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// İlgili iş emri ID (Opsiyonel)
        /// </summary>
        public int? ServiceJobId { get; set; }

        /// <summary>
        /// Sipariş numarası (Otomatik: PO-2024-001)
        /// </summary>
        [Required]
        [MaxLength(20)]
        public string PONumber { get; set; } = string.Empty;

        /// <summary>
        /// Tedarikçi adı
        /// </summary>
        [Required]
        [MaxLength(200)]
        public string SupplierName { get; set; } = string.Empty;

        /// <summary>
        /// Tedarikçi iletişim bilgisi
        /// </summary>
        [MaxLength(100)]
        public string? SupplierContact { get; set; }

        /// <summary>
        /// Sipariş durumu
        /// </summary>
        [Required]
        public PurchaseStatus Status { get; set; } = PurchaseStatus.Pending;

        /// <summary>
        /// Sipariş tarihi
        /// </summary>
        [Required]
        public DateTime OrderDate { get; set; } = DateTime.Now;

        /// <summary>
        /// Tahmini teslimat tarihi
        /// </summary>
        public DateTime? ExpectedDate { get; set; }

        /// <summary>
        /// Gerçek teslimat tarihi
        /// </summary>
        public DateTime? ReceivedDate { get; set; }

        /// <summary>
        /// Toplam tutar
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        /// <summary>
        /// Notlar
        /// </summary>
        [MaxLength(1000)]
        public string? Notes { get; set; }

        /// <summary>
        /// İlgili iş emri
        /// </summary>
        [ForeignKey(nameof(ServiceJobId))]
        public virtual ServiceJob? ServiceJob { get; set; }

        /// <summary>
        /// Sipariş kalemleri
        /// </summary>
        public virtual ICollection<PurchaseOrderItem> Items { get; set; } = new List<PurchaseOrderItem>();

        /// <summary>
        /// Durum gösterim metni
        /// </summary>
        [NotMapped]
        public string StatusDisplay => Status switch
        {
            PurchaseStatus.Pending => "⏳ Sipariş Bekliyor",
            PurchaseStatus.Ordered => "📦 Sipariş Verildi",
            PurchaseStatus.Shipped => "🚚 Kargoda",
            PurchaseStatus.Received => "✅ Teslim Alındı",
            PurchaseStatus.Cancelled => "❌ İptal",
            _ => Status.ToString()
        };

        /// <summary>
        /// Gecikme durumu (Computed)
        /// </summary>
        [NotMapped]
        public bool IsDelayed => ExpectedDate.HasValue &&
                                  ExpectedDate.Value < DateTime.Now &&
                                  Status != PurchaseStatus.Received &&
                                  Status != PurchaseStatus.Cancelled;
    }
}
