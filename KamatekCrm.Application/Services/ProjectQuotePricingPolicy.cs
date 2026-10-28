using System.Text.Json;
using KamatekCrm.ApplicationCore.Common;
using KamatekCrm.ApplicationCore.DTOs.ProjectQuotes;
using KamatekCrm.Shared.Models;

namespace KamatekCrm.ApplicationCore.Services;

/// <summary>
/// Teklif finansallarının tek doğruluk kaynağıdır. Serileştirilmiş ara toplamları
/// kullanmaz; her sonucu kalemlerin değişmez girdilerinden yeniden üretir.
/// </summary>
public static class ProjectQuotePricingPolicy
{
    private const int MaximumNodeCount = 10_000;
    private const int MaximumItemCount = 50_000;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static Result<ProjectQuotePricingResult> Calculate(
        string? scopeJson,
        decimal discountPercent,
        decimal vatRate)
    {
        if (discountPercent is < 0 or > 100)
            return Result.Failure<ProjectQuotePricingResult>("İskonto oranı 0 ile 100 arasında olmalıdır.");
        if (vatRate is < 0 or > 100)
            return Result.Failure<ProjectQuotePricingResult>("KDV oranı 0 ile 100 arasında olmalıdır.");

        List<ScopeNode>? roots;
        try
        {
            roots = string.IsNullOrWhiteSpace(scopeJson)
                ? []
                : JsonSerializer.Deserialize<List<ScopeNode>>(scopeJson, JsonOptions);
        }
        catch (JsonException)
        {
            return Result.Failure<ProjectQuotePricingResult>("Teklif kapsamı geçerli bir veri yapısı değil.");
        }

        return Calculate(roots ?? [], discountPercent, vatRate);
    }

    public static Result<ProjectQuotePricingResult> Calculate(
        IEnumerable<ScopeNode> roots,
        decimal discountPercent,
        decimal vatRate)
    {
        if (discountPercent is < 0 or > 100)
            return Result.Failure<ProjectQuotePricingResult>("İskonto oranı 0 ile 100 arasında olmalıdır.");
        if (vatRate is < 0 or > 100)
            return Result.Failure<ProjectQuotePricingResult>("KDV oranı 0 ile 100 arasında olmalıdır.");

        decimal revenue = 0;
        decimal cost = 0;
        var nodeCount = 0;
        var itemCount = 0;
        var includedLineCount = 0;
        var totalQuantity = 0;

        var stack = new Stack<(ScopeNode node, int cumulativeMultiplier)>(
            roots.Reverse().Select(r => (r, 1)));
        while (stack.Count > 0)
        {
            var (node, cumulativeMultiplier) = stack.Pop();
            if (++nodeCount > MaximumNodeCount)
                return Result.Failure<ProjectQuotePricingResult>("Teklif kapsamı izin verilen düğüm sınırını aşıyor.");

            var currentMultiplier = checked(cumulativeMultiplier * Math.Max(1, node.Multiplier));

            foreach (var item in node.Items ?? [])
            {
                if (++itemCount > MaximumItemCount)
                    return Result.Failure<ProjectQuotePricingResult>("Teklif kapsamı izin verilen kalem sınırını aşıyor.");
                if (item.Quantity < 1 || item.UnitPrice < 0 || item.UnitCost < 0 || item.LaborCost < 0)
                    return Result.Failure<ProjectQuotePricingResult>("Teklif kalemlerinde miktar ve fiyatlar geçerli olmalıdır.");
                if (item.IsOptional) continue;
                includedLineCount++;

                try
                {
                    var effectiveQty = checked(item.Quantity * currentMultiplier);
                    revenue = checked(revenue + checked(item.UnitPrice * effectiveQty));
                    cost = checked(cost + checked((item.UnitCost + item.LaborCost) * effectiveQty));
                    totalQuantity = checked(totalQuantity + effectiveQty);
                }
                catch (OverflowException)
                {
                    return Result.Failure<ProjectQuotePricingResult>("Teklif toplamı desteklenen sayısal sınırı aşıyor.");
                }
            }

            foreach (var child in (node.Children ?? []).Reverse())
                stack.Push((child, currentMultiplier));
        }

        revenue = Money(revenue);
        cost = Money(cost);
        var discount = Money(revenue * discountPercent / 100m);
        var netRevenue = Money(revenue - discount);
        var vat = Money(netRevenue * vatRate / 100m);
        var grandTotal = Money(netRevenue + vat);
        var profit = Money(netRevenue - cost);
        var margin = netRevenue == 0 ? 0 : Math.Round(profit / netRevenue * 100m, 2, MidpointRounding.AwayFromZero);

        return Result.Success(new ProjectQuotePricingResult(
            revenue, cost, discount, netRevenue, vat, grandTotal, profit, margin,
            includedLineCount, totalQuantity));
    }

    private static decimal Money(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
