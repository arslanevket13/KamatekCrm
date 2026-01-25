using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using KamatekCrm.Enums;

namespace KamatekCrm.Models
{
    /// <summary>
    /// Kasa Hareketi Entity
    /// Günlük nakit akışı, gelir ve giderleri takip eder.
    /// Müşteri bağımsız işlemler (gider, genel gelir) destekler.
    /// </summary>
    public class CashTransaction
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// İşlem tarihi
        /// </summary>
        [Required]
        public DateTime Date { get; set; } = DateTime.Now;

        /// <summary>
        /// İşlem tutarı (pozitif değer)
        /// </summary>
        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        /// <summary>
        /// İşlem türü (Nakit/Kart/Gider)
        /// </summary>
        [Required]
        public CashTransactionType TransactionType { get; set; }

        /// <summary>
        /// Açıklama / Detay
        /// </summary>
        [MaxLength(500)]
        public string? Description { get; set; }

        /// <summary>
        /// Kategori (Giderler için: Fatura, Kira, Malzeme vb.)
        /// </summary>
        [MaxLength(100)]
        public string? Category { get; set; }

        /// <summary>
        /// Referans belge numarası (Fiş, Fatura No)
        /// </summary>
        [MaxLength(50)]
        public string? ReferenceNumber { get; set; }

        /// <summary>
        /// Müşteri ID (Opsiyonel - Gelir işlemlerinde)
        /// </summary>
        public int? CustomerId { get; set; }

        /// <summary>
        /// Satış siparişi ID (Opsiyonel - POS satışlarında)
        /// </summary>
        public int? SalesOrderId { get; set; }

        /// <summary>
        /// İşlemi yapan kullanıcı
        /// </summary>
        [MaxLength(100)]
        public string? CreatedBy { get; set; }

        /// <summary>
        /// Oluşturma zamanı
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation Properties
        [ForeignKey(nameof(CustomerId))]
        public virtual Customer? Customer { get; set; }

        [ForeignKey(nameof(SalesOrderId))]
        public virtual SalesOrder? SalesOrder { get; set; }

        /// <summary>
        /// Bu bir gelir işlemi mi?
        /// </summary>
        [NotMapped]
        public bool IsIncome => TransactionType == CashTransactionType.CashIncome 
                             || TransactionType == CashTransactionType.CardIncome
                             || TransactionType == CashTransactionType.TransferIncome;

        /// <summary>
        /// Bu bir gider işlemi mi?
        /// </summary>
        [NotMapped]
        public bool IsExpense => TransactionType == CashTransactionType.Expense
                              || TransactionType == CashTransactionType.TransferExpense;
    }
}
