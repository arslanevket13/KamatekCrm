using KamatekCrm.Shared.Enums;

namespace KamatekCrm.ApplicationCore.DTOs.Transactions;

public sealed record WarehouseLookupDto(int Id, string Name, bool IsQuarantine);

public sealed record SupplierLookupDto(int Id, string CompanyName);

public sealed record PurchaseProductLookupDto(
    int Id,
    string ProductName,
    string Sku,
    string Barcode,
    string Unit,
    decimal PurchasePrice,
    int VatRate);

public sealed record PurchasingWorkspaceDto(
    IReadOnlyCollection<PurchaseProductLookupDto> Products,
    IReadOnlyCollection<SupplierLookupDto> Suppliers,
    IReadOnlyCollection<WarehouseLookupDto> Warehouses,
    IReadOnlyCollection<PurchaseHistoryDto> RecentOrders);

public sealed record PurchaseHistoryDto(
    int PurchaseOrderId,
    string InvoiceNumber,
    DateTime OrderDate,
    string SupplierName,
    decimal TotalAmount,
    PurchaseStatus Status);

public sealed record SalesReturnReceiptLineDto(
    string ProductName,
    int Quantity,
    decimal LineTotal);

public sealed record SalesReturnReceiptPaymentDto(
    PaymentMethod PaymentMethod,
    decimal Amount,
    string Reference);

public sealed record SalesReturnReceiptDto(
    int SalesReturnId,
    string ReturnNumber,
    string SalesOrderNumber,
    DateTime Date,
    string Reason,
    decimal TotalAmount,
    IReadOnlyCollection<SalesReturnReceiptLineDto> Lines,
    IReadOnlyCollection<SalesReturnReceiptPaymentDto> Payments);
