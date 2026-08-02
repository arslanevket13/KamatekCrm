using KamatekCrm.Shared.Models;

namespace KamatekCrm.ApplicationCore.DTOs.Quotes;

public sealed record StandardQuoteCustomerDto(
    int Id,
    string CustomerCode,
    string FullName,
    string PhoneNumber,
    string? Email,
    string City,
    string? District,
    string? Neighborhood,
    string? Street,
    string? BuildingNo,
    string? ApartmentNo);

public sealed record StandardQuoteProductDto(
    int Id,
    string ProductName,
    string Sku,
    string Unit,
    decimal SalePrice,
    decimal PurchasePrice,
    decimal TaxPercent,
    int StockQuantity,
    string? ImagePath);

public sealed record StandardQuoteWorkspaceDto(
    IReadOnlyList<StandardQuoteCustomerDto> Customers);

public sealed record StandardQuoteLineInput(
    int ProductId,
    int Quantity,
    decimal UnitPrice,
    decimal DiscountPercent);

public sealed record StandardQuoteLinePricing(
    int ProductId,
    int Quantity,
    decimal GrossAmount,
    decimal DiscountAmount,
    decimal NetAmount,
    decimal TaxAmount,
    decimal LineTotal,
    decimal CostAmount);

public sealed record StandardQuotePricingResult(
    decimal SubTotal,
    decimal TotalDiscount,
    decimal NetTotal,
    decimal TotalTax,
    decimal GrandTotal,
    decimal TotalCost,
    decimal TotalProfit,
    decimal ProfitMarginPercent,
    IReadOnlyList<StandardQuoteLinePricing> Lines);

public sealed record SaveStandardQuoteCommand(
    Guid IdempotencyKey,
    int CustomerId,
    DateTime QuoteDate,
    DateTime ValidUntil,
    string Currency,
    string? TermsAndConditions,
    QuoteStatus Status,
    IReadOnlyList<StandardQuoteLineInput> Lines,
    int? SourceServiceJobId = null);

public sealed record StandardQuoteSaveResult(
    int QuoteId,
    string QuoteNumber,
    QuoteStatus Status,
    StandardQuotePricingResult Pricing,
    bool WasAlreadyApplied);

public sealed record StandardQuoteDocumentDto(
    Quote Quote,
    IReadOnlyList<QuoteLine> Lines);
