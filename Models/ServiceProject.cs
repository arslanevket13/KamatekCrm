using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using KamatekCrm.Enums;

namespace KamatekCrm.Models
{
    /// <summary>
    /// Ana Servis Projesi - Birden fazla ServiceJob içerebilir
    /// </summary>
    public class ServiceProject
    {
        /// <summary>
        /// Proje ID (Primary Key)
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Müşteri ID (Foreign Key)
        /// </summary>
        [Required]
        public int CustomerId { get; set; }

        /// <summary>
        /// Proje kodu (Otomatik: PRJ-2024-001)
        /// </summary>
        [Required]
        [MaxLength(20)]
        public string ProjectCode { get; set; } = string.Empty;

        /// <summary>
        /// Proje başlığı (Örn: "Ahmet Bey Villa Kurulumu")
        /// </summary>
        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Proje tipi
        /// </summary>
        [Required]
        public ProjectType ProjectType { get; set; } = ProjectType.Installation;

        /// <summary>
        /// Proje durumu
        /// </summary>
        [Required]
        public ProjectStatus Status { get; set; } = ProjectStatus.Draft;

        /// <summary>
        /// Satış Boru Hattı Aşaması (Kanban)
        /// </summary>
        public PipelineStage PipelineStage { get; set; } = PipelineStage.Lead;

        /// <summary>
        /// Oluşturulma tarihi
        /// </summary>
        [Required]
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        /// <summary>
        /// Başlangıç tarihi
        /// </summary>
        public DateTime? StartDate { get; set; }

        /// <summary>
        /// Tahmini bitiş tarihi
        /// </summary>
        public DateTime? EstimatedEndDate { get; set; }

        /// <summary>
        /// Gerçek bitiş tarihi
        /// </summary>
        public DateTime? CompletedDate { get; set; }

        /// <summary>
        /// Toplam bütçe
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalBudget { get; set; }

        /// <summary>
        /// Keşif notları (Site Survey)
        /// </summary>
        [MaxLength(2000)]
        public string? SurveyNotes { get; set; }

        /// <summary>
        /// Proje notları
        /// </summary>
        [MaxLength(2000)]
        public string? Notes { get; set; }

        #region Yapı Sihirbazı (Structure Wizard)

        /// <summary>
        /// Yapı tipi (Tek birim, Apartman, Site, Ticari)
        /// </summary>
        public StructureType StructureType { get; set; } = StructureType.SingleUnit;

        /// <summary>
        /// Yapı tanımı (JSON formatında)
        /// </summary>
        public string? StructureDefinitionJson { get; set; }

        /// <summary>
        /// Toplam birim sayısı (Oluşturulan daire/bölge sayısı)
        /// </summary>
        public int TotalUnitCount { get; set; } = 1;

        /// <summary>
        /// Teklif kalemleri (JSON formatında)
        /// </summary>
        public string? QuoteItemsJson { get; set; }

        /// <summary>
        /// Proje iskontosu (%)
        /// </summary>
        [Column(TypeName = "decimal(5,2)")]
        public decimal DiscountPercent { get; set; } = 0;

        /// <summary>
        /// Proje kapsam ağacı (Hierarchical Tree - JSON)
        /// </summary>
        public string? ProjectScopeJson { get; set; }

        /// <summary>
        /// Toplam maliyet (Alış fiyatları + İşçilik)
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalCost { get; set; }

        /// <summary>
        /// Toplam kar (Satış - Maliyet)
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalProfit { get; set; }

        #endregion

        /// <summary>
        /// İlgili müşteri
        /// </summary>
        [ForeignKey(nameof(CustomerId))]
        public virtual Customer Customer { get; set; } = null!;

        /// <summary>
        /// Bu projeye bağlı iş emirleri
        /// </summary>
        public virtual ICollection<ServiceJob> ServiceJobs { get; set; } = new List<ServiceJob>();

        /// <summary>
        /// Projedeki toplam iş sayısı (Computed)
        /// </summary>
        [NotMapped]
        public int JobCount => ServiceJobs?.Count ?? 0;

        /// <summary>
        /// Tamamlanan iş sayısı (Computed)
        /// </summary>
        [NotMapped]
        public int CompletedJobCount => ServiceJobs?.Count(j => j.Status == JobStatus.Completed) ?? 0;

        /// <summary>
        /// İlerleme yüzdesi (Computed)
        /// </summary>
        [NotMapped]
        public int ProgressPercentage => JobCount > 0 ? (int)((double)CompletedJobCount / JobCount * 100) : 0;

        /// <summary>
        /// Durum gösterim metni
        /// </summary>
        [NotMapped]
        public string StatusDisplay => Status switch
        {
            ProjectStatus.Draft => "📝 Taslak",
            ProjectStatus.PendingApproval => "⏳ Onay Bekliyor",
            ProjectStatus.Active => "🔵 Devam Ediyor",
            ProjectStatus.OnHold => "⏸️ Beklemede",
            ProjectStatus.Completed => "✅ Tamamlandı",
            ProjectStatus.Cancelled => "❌ İptal",
            _ => Status.ToString()
        };
    }
}
