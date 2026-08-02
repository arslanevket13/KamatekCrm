using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models.Common;

namespace KamatekCrm.Shared.Models;

public sealed class StockCountSession : BaseEntity
{
    [Required, MaxLength(36)]
    public string IdempotencyKey { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string ReferenceNumber { get; set; } = string.Empty;

    public int WarehouseId { get; set; }
    public DateTime CountedAt { get; set; } = DateTime.UtcNow;
    public StockCountMode Mode { get; set; }
    public int ProductCount { get; set; }
    public int TotalPositiveDifference { get; set; }
    public int TotalNegativeDifference { get; set; }
    public decimal FinancialDifference { get; set; }

    [Required, MaxLength(100)]
    public string CountedBy { get; set; } = string.Empty;

    [ForeignKey(nameof(WarehouseId))]
    public Warehouse Warehouse { get; set; } = null!;

    public ICollection<StockCountSessionItem> Items { get; set; } = new List<StockCountSessionItem>();
}

public sealed class StockCountSessionItem
{
    public int Id { get; set; }
    public int StockCountSessionId { get; set; }
    public int ProductId { get; set; }
    [Required, MaxLength(100)]
    public string ProductCode { get; set; } = string.Empty;
    [Required, MaxLength(200)]
    public string ProductName { get; set; } = string.Empty;
    public int SystemQuantity { get; set; }
    public int CountedQuantity { get; set; }
    public int Difference { get; set; }
    public decimal UnitCost { get; set; }
    public decimal FinancialDifference { get; set; }
    public int? StockTransactionId { get; set; }

    [ForeignKey(nameof(StockCountSessionId))]
    public StockCountSession StockCountSession { get; set; } = null!;

    [ForeignKey(nameof(ProductId))]
    public Product Product { get; set; } = null!;

    [ForeignKey(nameof(StockTransactionId))]
    public StockTransaction? StockTransaction { get; set; }
}
