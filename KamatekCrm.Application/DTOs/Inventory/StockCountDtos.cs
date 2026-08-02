using KamatekCrm.Shared.Enums;

namespace KamatekCrm.ApplicationCore.DTOs.Inventory;

public sealed record StockCountWarehouseDto(int Id, string Name, bool IsQuarantine);

public sealed record StockCountProductDto(
    int ProductId,
    string ProductCode,
    string Barcode,
    string ProductName,
    string ModelName,
    string Unit,
    int SystemQuantity,
    decimal PurchasePrice);

public sealed record StockCountLineCommand(
    int ProductId,
    int SystemQuantity,
    int CountedQuantity);

public sealed record ApplyStockCountCommand(
    Guid IdempotencyKey,
    int WarehouseId,
    DateTime CountedAt,
    StockCountMode Mode,
    IReadOnlyList<StockCountLineCommand> Lines,
    string ChangedBy);

public sealed record StockCountResult(
    int SessionId,
    string ReferenceNumber,
    int ProductCount,
    int TotalPositiveDifference,
    int TotalNegativeDifference,
    decimal FinancialDifference,
    bool WasAlreadyApplied);

public sealed record StockCountHistoryDto(
    int? SessionId,
    DateTime CountedAt,
    string WarehouseName,
    StockCountMode Mode,
    int ProductCount,
    int TotalDifference,
    decimal FinancialDifference,
    string ReferenceNumber,
    string CountedBy);

public sealed record StockCountHistoryLineDto(
    string ProductCode,
    string ProductName,
    int SystemQuantity,
    int CountedQuantity,
    int Difference,
    decimal FinancialDifference);
