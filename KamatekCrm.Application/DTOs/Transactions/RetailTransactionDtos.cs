using KamatekCrm.Shared.Enums;

namespace KamatekCrm.ApplicationCore.DTOs.Transactions;

public sealed record TransactionLineInput(
    int ProductId,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    decimal DiscountAmount,
    int TaxRate,
    decimal LineTotal);

public sealed record PaymentAllocationInput(
    PaymentMethod PaymentMethod,
    decimal Amount,
    string? Reference = null);

public sealed record CompleteSaleCommand(
    int? CustomerId,
    string CustomerName,
    int WarehouseId,
    IReadOnlyCollection<TransactionLineInput> Items,
    IReadOnlyCollection<PaymentAllocationInput> Payments,
    string? Notes,
    string CreatedBy,
    string IdempotencyKey);

public sealed record SaleTransactionResult(int SalesOrderId, string OrderNumber, bool WasAlreadyProcessed);

public sealed record SaleSearchQuery(string? SearchText, DateTime? StartDate, DateTime? EndDate, int Take = 100);

public sealed record SaleSummaryDto(
    int SalesOrderId,
    string OrderNumber,
    DateTime Date,
    string CustomerName,
    decimal TotalAmount,
    SalesOrderStatus Status);

public sealed record ReturnableSaleLineDto(
    int SalesOrderItemId,
    int ProductId,
    string ProductName,
    int SoldQuantity,
    int ReturnedQuantity,
    int RemainingQuantity,
    decimal RemainingAmount);

public sealed record ReturnableSaleDto(
    int SalesOrderId,
    string OrderNumber,
    int? OriginalWarehouseId,
    decimal ExternalPaymentRemaining,
    IReadOnlyCollection<ReturnableSaleLineDto> Lines);

public sealed record SalesReturnLineInput(
    int SalesOrderItemId,
    int Quantity,
    ReturnDisposition Disposition,
    int RestockWarehouseId);

public sealed record ReturnSaleCommand(
    int SalesOrderId,
    IReadOnlyCollection<SalesReturnLineInput> Lines,
    IReadOnlyCollection<PaymentAllocationInput> Refunds,
    string Reason,
    string? Notes,
    string CreatedBy,
    string IdempotencyKey);

public sealed record ReturnTransactionResult(
    int ReturnId,
    string ReturnNumber,
    decimal TotalAmount,
    bool WasAlreadyProcessed);

public sealed record LegacyLedgerIssueDto(
    int SalesOrderId,
    string OrderNumber,
    int CustomerId,
    string CustomerName,
    decimal OnAccountAmount,
    string ReconciliationKey);

public sealed record LegacyLedgerPreviewDto(
    IReadOnlyCollection<LegacyLedgerIssueDto> Issues,
    decimal TotalCorrection);
