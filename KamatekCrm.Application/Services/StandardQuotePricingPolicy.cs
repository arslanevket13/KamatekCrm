using KamatekCrm.ApplicationCore.Common;
using KamatekCrm.ApplicationCore.DTOs.Quotes;
using KamatekCrm.Shared.Services;

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
        decimal costTotal = 0;
        var results = new List<StandardQuoteLinePricing>(lines.Count);
        var calcInputs = new List<LineCalculationInput>(lines.Count);

        foreach (var line in lines)
        {
            if (line.ProductId <= 0 || line.Quantity <= 0)
                return Result.Failure<StandardQuotePricingResult>("Teklif kalemlerinde ürün ve miktar geçerli olmalıdır.");
            if (line.UnitPrice < 0 || line.PurchasePrice < 0)
                return Result.Failure<StandardQuotePricingResult>("Teklif kalemlerinde fiyatlar negatif olamaz.");
            if (line.DiscountPercent is < 0 or > 100 || line.TaxPercent is < 0 or > 100)
                return Result.Failure<StandardQuotePricingResult>("İskonto ve vergi oranları 0 ile 100 arasında olmalıdır.");

            calcInputs.Add(new LineCalculationInput(
                SourceId: null,
                ProductId: line.ProductId,
                ProductName: string.Empty,
                Quantity: line.Quantity,
                UnitPrice: line.UnitPrice,
                DiscountPercent: line.DiscountPercent,
                TaxPercent: line.TaxPercent));
        }

        try
        {
            var multiLine = FinancialCalculator.CalculateMultiLine(calcInputs);

            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                var lineCalc = multiLine.Lines[i];
                var gross = FinancialCalculator.RoundMoney(checked(line.UnitPrice * line.Quantity));
                var discount = FinancialCalculator.RoundMoney(gross - lineCalc.NetAfterDiscount);
                var cost = FinancialCalculator.RoundMoney(checked(line.PurchasePrice * line.Quantity));

                subTotal = checked(subTotal + gross);
                discountTotal = checked(discountTotal + discount);
                costTotal = checked(costTotal + cost);

                results.Add(new StandardQuoteLinePricing(
                    line.ProductId,
                    line.Quantity,
                    gross,
                    discount,
                    lineCalc.NetAfterDiscount,
                    lineCalc.TaxAmount,
                    lineCalc.TotalWithTax,
                    cost));
            }

            var roundedSubTotal = FinancialCalculator.RoundMoney(subTotal);
            var roundedDiscountTotal = FinancialCalculator.RoundMoney(discountTotal);
            var netTotal = multiLine.NetTotal;
            var taxTotal = multiLine.TaxTotal;
            var grandTotal = multiLine.GrandTotal;
            var roundedCostTotal = FinancialCalculator.RoundMoney(costTotal);

            var profit = FinancialCalculator.RoundMoney(netTotal - roundedCostTotal);
            var margin = roundedCostTotal == 0
                ? 0
                : FinancialCalculator.RoundMoney(profit / roundedCostTotal * 100m);

            return Result.Success(new StandardQuotePricingResult(
                roundedSubTotal,
                roundedDiscountTotal,
                netTotal,
                taxTotal,
                grandTotal,
                roundedCostTotal,
                profit,
                margin,
                results));
        }
        catch (OverflowException)
        {
            return Result.Failure<StandardQuotePricingResult>("Teklif toplamı desteklenen sayısal sınırı aşıyor.");
        }
    }
}
