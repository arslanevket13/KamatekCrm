using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using KamatekCrm.Shared.Enums;

namespace KamatekCrm.Shared.Models.WorkOrders
{
    /// <summary>
    /// Keşif aşaması kaydı. Keşif ekranında girilen teknik notlar, fotoğraflar,
    /// önerilen çözüm ve tahmini malzemeler burada saklanır. Fiyat içermez.
    /// Her iş emri için tek keşif raporu vardır (1:1).
    /// </summary>
    public class DiscoveryReport
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ServiceJobId { get; set; }

        [ForeignKey(nameof(ServiceJobId))]
        public virtual ServiceJob? ServiceJob { get; set; }

        /// <summary>Teknisyenin sahadaki teknik tespitleri</summary>
        [MaxLength(4000)]
        public string? TechnicalNotes { get; set; }

        /// <summary>Önerilen çözüm / keşif sonucu</summary>
        [MaxLength(4000)]
        public string? RecommendedSolution { get; set; }

        /// <summary>Keşif fotoğrafları (JSON dizi)</summary>
        public string? PhotoPathsJson { get; set; }

        /// <summary>Tahmini işçilik süresi (saat)</summary>
        public double EstimatedLaborHours { get; set; }

        /// <summary>Keşfi yapan teknisyen</summary>
        [MaxLength(100)]
        public string? TechnicianName { get; set; }

        public DateTime CreatedDate { get; set; } = System.DateTime.UtcNow;

        public virtual ICollection<DiscoveryMaterial> Materials { get; set; } = new List<DiscoveryMaterial>();

        [NotMapped]
        public IReadOnlyList<string> PhotoPathsList =>
            string.IsNullOrWhiteSpace(PhotoPathsJson)
                ? new List<string>()
                : System.Text.Json.JsonSerializer.Deserialize<List<string>>(PhotoPathsJson) ?? new List<string>();
    }

    /// <summary>
    /// Keşifte tahmin edilen malzeme. Fiyat saklamaz; teklife dönüştürülürken
    /// <see cref="QuotationItem"/> olarak kopyalanır ve fiyatlar orada girilir.
    /// </summary>
    public class DiscoveryMaterial
    {
        public int Id { get; set; }

        [Required]
        public int DiscoveryReportId { get; set; }

        [ForeignKey(nameof(DiscoveryReportId))]
        public virtual DiscoveryReport? DiscoveryReport { get; set; }

        public int? ProductId { get; set; }

        [MaxLength(200)]
        public string ProductName { get; set; } = string.Empty;

        public int Quantity { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }
    }

    /// <summary>
    /// Keşif aşamasındaki tek bir saha ziyareti. Bir iş emri için birden çok ziyaret
    /// kaydedilebilir (ilk keşif, ek keşif, kontrol ziyareti vb.). Her ziyaretin kendi
    /// tarihi, teknisyeni, notu ve fotoğrafları vardır. Fiyat içermez.
    /// </summary>
    public class DiscoveryVisit
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ServiceJobId { get; set; }

        [ForeignKey(nameof(ServiceJobId))]
        public virtual ServiceJob? ServiceJob { get; set; }

        /// <summary>Ziyaret tarihi/saati</summary>
        public DateTime VisitDate { get; set; } = System.DateTime.UtcNow;

        /// <summary>Ziyareti yapan teknisyen</summary>
        [MaxLength(100)]
        public string? TechnicianName { get; set; }

        /// <summary>Ziyarette alınan notlar</summary>
        [MaxLength(4000)]
        public string? Notes { get; set; }

        /// <summary>Ziyaret fotoğrafları (JSON dizi)</summary>
        public string? PhotoPathsJson { get; set; }

        public DateTime CreatedDate { get; set; } = System.DateTime.UtcNow;

        [NotMapped]
        public IReadOnlyList<string> PhotoPathsList =>
            string.IsNullOrWhiteSpace(PhotoPathsJson)
                ? new List<string>()
                : System.Text.Json.JsonSerializer.Deserialize<List<string>>(PhotoPathsJson) ?? new List<string>();
    }

    /// <summary>
    /// İş emrine bağlı fiyat teklifi. Keşif malzemeleri kopyalanarak oluşturulur;
    /// fiyat, iskonto, KDV, işçilik, nakliye, garanti, teslim ve ödeme şartları burada tutulur.
    /// </summary>
    public class WorkOrderQuotation
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ServiceJobId { get; set; }

        [ForeignKey(nameof(ServiceJobId))]
        public virtual ServiceJob? ServiceJob { get; set; }

        [MaxLength(50)]
        public string QuotationNumber { get; set; } = string.Empty;

        /// <summary>Bu teklifin revizyonunu türettiği ana teklif. İlk teklifte null.</summary>
        public int? ParentQuotationId { get; set; }

        /// <summary>Revizyon numarası (ilk teklif 0, sonraki her revizyon +1).</summary>
        public int RevisionNumber { get; set; }

        public QuotationStatus Status { get; set; } = QuotationStatus.Draft;

        public DateTime IssuedDate { get; set; } = System.DateTime.UtcNow;

        public DateTime? ValidUntil { get; set; }

        /// <summary>Teklif açıklaması</summary>
        [MaxLength(2000)]
        public string? Description { get; set; }

        /// <summary>Garanti koşulları</summary>
        [MaxLength(500)]
        public string? Warranty { get; set; }

        /// <summary>Teslim süresi</summary>
        [MaxLength(200)]
        public string? DeliveryTime { get; set; }

        /// <summary>Ödeme şartları</summary>
        [MaxLength(500)]
        public string? PaymentTerms { get; set; }

        /// <summary>İşçilik bedeli</summary>
        public decimal LaborCost { get; set; }

        /// <summary>Nakliye bedeli</summary>
        public decimal ShippingCost { get; set; }

        /// <summary>Toplam iskonto (tutar)</summary>
        public decimal DiscountAmount { get; set; }

        /// <summary>KDV oranı (%)</summary>
        public decimal TaxRate { get; set; } = 20m;

        /// <summary>KDV tutarı</summary>
        public decimal TaxAmount { get; set; }

        /// <summary>Genel toplam</summary>
        public decimal TotalAmount { get; set; }

        public DateTime? SentDate { get; set; }
        public DateTime? AcceptedAt { get; set; }
        public DateTime? RejectedAt { get; set; }

        [MaxLength(500)]
        public string? RejectionReason { get; set; }

        public virtual ICollection<QuotationItem> Items { get; set; } = new List<QuotationItem>();
    }

    /// <summary>
    /// Teklif kalemi. Keşif malzemelerinin kopyasıdır; fiyat bilgisi yalnızca burada tutulur.
    /// </summary>
    public class QuotationItem
    {
        public int Id { get; set; }

        [Required]
        public int QuotationId { get; set; }

        [ForeignKey(nameof(QuotationId))]
        public virtual WorkOrderQuotation? Quotation { get; set; }

        public int? ProductId { get; set; }

        [MaxLength(200)]
        public string ProductName { get; set; } = string.Empty;

        /// <summary>Miktar; metre, kilogram, saat gibi kesirli birimleri destekler.</summary>
        public decimal Quantity { get; set; }

        /// <summary>Teklif içindeki satır sırası (0'dan başlar).</summary>
        public int Sequence { get; set; }

        public decimal UnitPrice { get; set; }

        public decimal DiscountPercent { get; set; }

        public decimal TaxPercent { get; set; } = 20m;

        public decimal LineTotal { get; set; }

        [NotMapped]
        public decimal LineTotalBeforeTax => Quantity * UnitPrice * (1m - DiscountPercent / 100m);
    }

    /// <summary>
    /// Montaj iş emri. Teklif kabul edildiğinde teklif kalemleri kopyalanarak oluşturulur.
    /// Teknisyen, montaj tarihi, görevler ve notlar ayrı saklanır.
    /// </summary>
    public class InstallationOrder
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ServiceJobId { get; set; }

        [ForeignKey(nameof(ServiceJobId))]
        public virtual ServiceJob? ServiceJob { get; set; }

        public int? QuotationId { get; set; }

        [ForeignKey(nameof(QuotationId))]
        public virtual WorkOrderQuotation? Quotation { get; set; }

        public int? TechnicianId { get; set; }

        [MaxLength(100)]
        public string? TechnicianName { get; set; }

        public DateTime? InstallationDate { get; set; }

        [MaxLength(2000)]
        public string? Notes { get; set; }

        /// <summary>Montajda harcanan işçilik saati (tamamlama formunda fiili değerle güncellenir).</summary>
        public decimal LaborHours { get; set; }

        public DateTime CreatedDate { get; set; } = System.DateTime.UtcNow;

        // ── Tamamlama Verileri ──
        public DateTime? CompletedAt { get; set; }

        [MaxLength(100)]
        public string? CompletionTechnician { get; set; }

        /// <summary>Teslim notu</summary>
        [MaxLength(2000)]
        public string? DeliveryNote { get; set; }

        /// <summary>Müşteri imzası (base64)</summary>
        public string? CustomerSignature { get; set; }

        public virtual ICollection<InstallationMaterial> Materials { get; set; } = new List<InstallationMaterial>();

        public virtual ICollection<InstallationTask> Tasks { get; set; } = new List<InstallationTask>();
    }

    /// <summary>
    /// Montajda kullanılan malzeme. Teklif kalemlerinin kopyasıdır; montaj
    /// tamamlanırken gerçek kullanılan miktarlar güncellenebilir.
    /// </summary>
    public class InstallationMaterial
    {
        public int Id { get; set; }

        [Required]
        public int InstallationOrderId { get; set; }

        [ForeignKey(nameof(InstallationOrderId))]
        public virtual InstallationOrder? InstallationOrder { get; set; }

        public int? ProductId { get; set; }

        [MaxLength(200)]
        public string ProductName { get; set; } = string.Empty;

        /// <summary>Kullanılacak miktar; kesirli birimler (metre vb.) desteklenir.</summary>
        public decimal Quantity { get; set; }

        public decimal UnitPrice { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }
    }

    /// <summary>
    /// Montaj görev listesi kalemi.
    /// </summary>
    public class InstallationTask
    {
        public int Id { get; set; }

        [Required]
        public int InstallationOrderId { get; set; }

        [ForeignKey(nameof(InstallationOrderId))]
        public virtual InstallationOrder? InstallationOrder { get; set; }

        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Description { get; set; }

        public bool IsCompleted { get; set; }

        public DateTime? CompletedAt { get; set; }
    }

    /// <summary>
    /// Teslim aşaması kaydı (Paket 7). İş teslim edildiğinde teslim tarihi, teslim eden,
    /// teslim notu, müşteri imzası ve ödeme bilgileri (durum, yöntem, tahsilat, fatura no)
    /// burada saklanır. Her iş emri için tek teslim kaydı vardır (1:1).
    /// </summary>
    public class JobDelivery
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ServiceJobId { get; set; }

        [ForeignKey(nameof(ServiceJobId))]
        public virtual ServiceJob? ServiceJob { get; set; }

        /// <summary>Teslim tarihi/saati</summary>
        public DateTime DeliveryDate { get; set; } = System.DateTime.UtcNow;

        /// <summary>Teslim eden kişi</summary>
        [MaxLength(100)]
        public string? DeliveredBy { get; set; }

        /// <summary>Teslim notu</summary>
        [MaxLength(2000)]
        public string? DeliveryNote { get; set; }

        /// <summary>Müşteri imzası (base64 veya metin)</summary>
        public string? CustomerSignature { get; set; }

        /// <summary>Ödeme durumu (tahsilat bekleniyor / kısmi / ödendi)</summary>
        public KamatekCrm.Shared.Enums.PaymentStatus PaymentStatus { get; set; } = KamatekCrm.Shared.Enums.PaymentStatus.Unpaid;

        /// <summary>Ödeme yöntemi</summary>
        public KamatekCrm.Shared.Enums.PaymentMethod PaymentMethod { get; set; } = KamatekCrm.Shared.Enums.PaymentMethod.Cash;

        /// <summary>Tahsil edilen tutar</summary>
        public decimal PaidAmount { get; set; }

        /// <summary>Fatura numarası</summary>
        [MaxLength(50)]
        public string? InvoiceNumber { get; set; }

        public DateTime CreatedDate { get; set; } = System.DateTime.UtcNow;
    }
}
