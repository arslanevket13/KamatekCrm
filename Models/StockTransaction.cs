using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using KamatekCrm.Enums;

namespace KamatekCrm.Models
{
    /// <summary>
    /// Stok Hareketleri (Ledger)
    /// Tüm giriş, çıkış ve transfer işlemleri burada kayıt altına alınır.
    /// </summary>
    public class StockTransaction
    {
        /// <summary>
        /// İşlem ID (PK)
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// İşlem Tarihi
        /// </summary>
        [Required]
        public DateTime Date { get; set; } = DateTime.Now;

        /// <summary>
        /// Ürün ID (FK)
        /// </summary>
        [Required]
        public int ProductId { get; set; }

        /// <summary>
        /// İlgili Ürün
        /// </summary>
        [ForeignKey(nameof(ProductId))]
        public virtual Product Product { get; set; } = null!;

        /// <summary>
        /// Kaynak Depo ID (Çıkış yapılan yer - Nullable: Satınalma ise null olabilir)
        /// </summary>
        public int? SourceWarehouseId { get; set; }

        /// <summary>
        /// Kaynak Depo
        /// </summary>
        [ForeignKey(nameof(SourceWarehouseId))]
        public virtual Warehouse? SourceWarehouse { get; set; }

        /// <summary>
        /// Hedef Depo ID (Giriş yapılan yer - Nullable: Satış/Zayi ise null olabilir)
        /// </summary>
        public int? TargetWarehouseId { get; set; }

        /// <summary>
        /// Hedef Depo
        /// </summary>
        [ForeignKey(nameof(TargetWarehouseId))]
        public virtual Warehouse? TargetWarehouse { get; set; }

        /// <summary>
        /// İşlem Miktarı (Her zaman pozitif, yönü TransactionType belirler)
        /// </summary>
        [Required]
        public int Quantity { get; set; }

        /// <summary>
        /// İşlem Tipi (Alım, Satış, Transfer vs.)
        /// </summary>
        [Required]
        public StockTransactionType TransactionType { get; set; }

        /// <summary>
        /// İşlem anındaki birim maliyet (Raporlama için)
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitCost { get; set; }

        /// <summary>
        /// Referans ID (ServiceJobId, Fatura No vb.)
        /// </summary>
        [MaxLength(100)]
        public string? ReferenceId { get; set; }

        /// <summary>
        /// İşlemi Yapan Kullanıcı ID (Loglamak için)
        /// </summary>
        [MaxLength(100)]
        public string? UserId { get; set; }
        
        /// <summary>
        /// Açıklama
        /// </summary>
        [MaxLength(500)]
        public string? Description { get; set; }
    }
}
