using KamatekCrm.Shared.Enums;

namespace KamatekCrm.ApplicationCore.DTOs.Transactions;

public sealed record PurchaseLineInput(
    int? ProductId,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    decimal DiscountAmount,
    decimal TaxRate,
    decimal LineTotal,
    string? Sku = null,
    string? Barcode = null,
    string? Unit = null);

public sealed record CreatePurchaseCommand(
    int SupplierId,
    string InvoiceNumber,
    DateTime OrderDate,
    IReadOnlyCollection<PurchaseLineInput> Lines,
    string? Notes,
    string CreatedBy,
    string IdempotencyKey,
    bool ReceiveImmediately = false,
    int? WarehouseId = null,
    IReadOnlyCollection<PaymentAllocationInput>? Settlements = null);

public sealed record PurchaseCommandResult(
    int PurchaseOrderId,
    decimal TotalAmount,
    bool WasAlreadyProcessed);

public sealed record ReceivePurchaseCommand(
    int PurchaseOrderId,
    int WarehouseId,
    IReadOnlyCollection<PaymentAllocationInput> Settlements,
    string CreatedBy,
    string IdempotencyKey);

public sealed record CancelPurchaseCommand(int PurchaseOrderId, string Reason, string CancelledBy);

public sealed record ReturnablePurchaseLineDto(
    int PurchaseOrderItemId,
    int ProductId,
    string ProductName,
    int ReceivedQuantity,
    int ReturnedQuantity,
    int RemainingQuantity,
    decimal RemainingAmount);

public sealed record ReturnablePurchaseDto(
    int PurchaseOrderId,
    string InvoiceNumber,
    int? OriginalWarehouseId,
    decimal ExternalSettlementRemaining,
    bool RequiresLegacySettlementOverride,
    IReadOnlyCollection<ReturnablePurchaseLineDto> Lines);

public sealed record PurchaseReturnLineInput(int PurchaseOrderItemId, int Quantity, int SourceWarehouseId);

public sealed record ReturnPurchaseCommand(
    int PurchaseOrderId,
    IReadOnlyCollection<PurchaseReturnLineInput> Lines,
    PaymentMethod SettlementMethod,
    string? SettlementReference,
    string Reason,
    string? Notes,
    string CreatedBy,
    string IdempotencyKey,
    bool LegacySettlementOverride = false);
