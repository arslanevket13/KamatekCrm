using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using KamatekCrm.Enums;

namespace KamatekCrm.Models
{
    /// <summary>
    /// Finansal işlem entity'si (Ödeme/Borç)
    /// </summary>
    public class Transaction
    {
        /// <summary>
        /// İşlem ID (Primary Key)
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Müşteri ID (Foreign Key)
        /// </summary>
        [Required]
        public int CustomerId { get; set; }

        /// <summary>
        /// İşlem tarihi
        /// </summary>
        [Required]
        public DateTime Date { get; set; } = DateTime.Now;

        /// <summary>
        /// İşlem tutarı
        /// </summary>
        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        /// <summary>
        /// İşlem türü (Ödeme/Borç)
        /// </summary>
        [Required]
        public TransactionType Type { get; set; }

        /// <summary>
        /// İşlem açıklaması
        /// </summary>
        [MaxLength(500)]
        public string? Description { get; set; }

        /// <summary>
        /// İlgili müşteri (Navigation Property)
        /// </summary>
        [ForeignKey(nameof(CustomerId))]
        public virtual Customer Customer { get; set; } = null!;
    }
}
