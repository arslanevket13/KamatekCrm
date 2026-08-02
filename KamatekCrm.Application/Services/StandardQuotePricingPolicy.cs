using KamatekCrm.ApplicationCore.Common;
using KamatekCrm.ApplicationCore.DTOs.Quotes;

namespace KamatekCrm.ApplicationCore.Services;

public static class StandardQuotePricingPolicy
{
    public static Result<StandardQuotePricingResult> Calculate(
        IReadOnlyList<(int ProductId, int Quantity, decimal UnitPrice, decimal PurchasePrice,
            decimal DiscountPercent, decimal TaxPercent)> lines)
    {
        if (lines.Count == 0)
            return Result.Failure<StandardQuotePricingResult>("Teklifte en az bir kalem bulunmalıdır.");
        if (lines.Count > 1_000)
            return Result.Failure<StandardQuotePricingResult>("Teklif en fazla 1.000 kalem içerebilir.");

        decimal subTotal = 0;
        decimal discountTotal = 0;
        decimal netTotal = 0;
        decimal taxTotal = 0;
        decimal grandTotal = 0;
        decimal costTotal = 0;
        var results = new List<StandardQuoteLinePricing>(lines.Count);

        foreach (var line in lines)
        {
            if (line.ProductId <= 0 || line.Quantity <= 0)
                return Result.Failure<StandardQuotePricingResult>("Teklif kalemlerinde ürün ve miktar geçerli olmalıdır.");
            if (line.UnitPrice < 0 || line.PurchasePrice < 0)
                return Result.Failure<StandardQuotePricingResult>("Teklif kalemlerinde fiyatlar negatif olamaz.");
            if (line.DiscountPercent is < 0 or > 100 || line.TaxPercent is < 0 or > 100)
                return Result.Failure<StandardQuotePricingResult>("İskonto ve vergi oranları 0 ile 100 arasında olmalıdır.");

            try
            {
                var gross = Money(checked(line.UnitPrice * line.Quantity));
                var discount = Money(gross * line.DiscountPercent / 100m);
                var net = Money(gross - discount);
                var tax = Money(net * line.TaxPercent / 100m);
                var total = Money(net + tax);
                var cost = Money(checked(line.PurchasePrice * line.Quantity));
                subTotal = checked(subTotal + gross);
                discountTotal = checked(discountTotal + discount);
                netTotal = checked(netTotal + net);
                taxTotal = checked(taxTotal + tax);
                grandTotal = checked(grandTotal + total);
                costTotal = checked(costTotal + cost);
                results.Add(new StandardQuoteLinePricing(
                    line.ProductId, line.Quantity, gross, discount, net, tax, total, cost));
            }
            catch (OverflowException)
            {
                return Result.Failure<StandardQuotePricingResult>("Teklif toplamı desteklenen sayısal sınırı aşıyor.");
            }
        }

        var profit = Money(netTotal - costTotal);
        var margin = costTotal == 0
            ? 0
            : Math.Round(profit / costTotal * 100m, 2, MidpointRounding.AwayFromZero);
        return Result.Success(new StandardQuotePricingResult(
            Money(subTotal), Money(discountTotal), Money(netTotal), Money(taxTotal),
            Money(grandTotal), Money(costTotal), profit, margin, results));
    }

    private static decimal Money(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
