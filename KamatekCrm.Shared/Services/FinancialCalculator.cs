using System;
using System.Collections.Generic;
using System.Linq;

namespace KamatekCrm.Shared.Services;

/// <summary>
/// Merkezi Finansal Matematik ve Kuruş Dengeleme Servisi.
/// Tüm parasal hesaplamalarda 'decimal' tipi ve 'MidpointRounding.ToEven' standart yuvarlama kullanılır.
/// Çok satırlı tekliflerde iskonto ve KDV dağıtımı Largest Remainder (Hamilton-Hare) yöntemiyle yapılır.
/// Garanti: Satır Net Toplamları + Satır KDV Toplamları == Genel Toplam (kuruşu kuruşuna).
/// </summary>
public static class FinancialCalculator
{
    /// <summary>
    /// Parasal tutarı 2 basamağa ve MidpointRounding.ToEven (Banker's rounding) ile yuvarlar.
    /// </summary>
    public static decimal RoundMoney(decimal value) =>
        Math.Round(value, 2, MidpointRounding.ToEven);

    /// <summary>
    /// Largest Remainder (Hamilton-Hare) Yöntemi:
    /// Bir toplam tutarı (örneğin alt iskonto veya toplam vergi), verilen ağırlıklar (baz tutarlar)
    /// oranında satırlara dağıtırken oluşan kuruş farklarını (penny discrepancy) en büyük kalana
    /// ve en yüksek tutarlı satırlara yansıtarak, dağıtılan tutarların toplamının tam olarak
    /// targetTotal'a kuruşu kuruşuna eşit olmasını sağlar.
    /// </summary>
    public static decimal[] DistributeLargestRemainder(decimal targetTotal, IReadOnlyList<decimal> weights)
    {
        if (weights == null || weights.Count == 0) return Array.Empty<decimal>();
        if (weights.Count == 1) return new[] { RoundMoney(targetTotal) };

        targetTotal = RoundMoney(targetTotal);
        if (targetTotal == 0) return new decimal[weights.Count];

        decimal sumWeights = weights.Sum();
        if (sumWeights == 0)
        {
            // Ağırlıklar toplamı sıfırsa eşit dağıt
            long totalCents = (long)Math.Round(targetTotal * 100m, MidpointRounding.ToEven);
            long baseCentsPerItem = totalCents / weights.Count;
            long remCents = totalCents % weights.Count;

            var res = new decimal[weights.Count];
            for (int i = 0; i < weights.Count; i++)
            {
                long c = baseCentsPerItem + (i < Math.Abs(remCents) ? Math.Sign(remCents) : 0);
                res[i] = c / 100m;
            }
            return res;
        }

        int count = weights.Count;
        var floorCents = new long[count];
        var remainders = new decimal[count];
        long sumFloorCents = 0;

        for (int i = 0; i < count; i++)
        {
            decimal rawRatio = weights[i] / sumWeights;
            decimal rawCents = (targetTotal * rawRatio) * 100m;
            long floor = (long)Math.Floor(rawCents);
            floorCents[i] = floor;
            remainders[i] = rawCents - floor;
            sumFloorCents += floor;
        }

        long targetCents = (long)Math.Round(targetTotal * 100m, MidpointRounding.ToEven);
        long missingCents = targetCents - sumFloorCents;

        // Kalanı en büyük olan satırlardan başlayarak 1'er kuruş ekle
        var sortedIndices = Enumerable.Range(0, count)
            .OrderByDescending(i => remainders[i])
            .ThenByDescending(i => weights[i])
            .ToList();

        for (int i = 0; i < missingCents && i < count; i++)
        {
            floorCents[sortedIndices[i]]++;
        }

        var results = new decimal[count];
        for (int i = 0; i < count; i++)
        {
            results[i] = floorCents[i] / 100m;
        }

        return results;
    }

    /// <summary>
    /// Kuruş Dengeleme:
    /// Satır bazlı yuvarlamaların toplamı ile hedef toplam arasındaki +/- 0.01 TL kuruş farkını
    /// en yüksek tutarlı satıra yansıtarak dengeyi sağlar.
    /// </summary>
    public static void BalancePennyDiscrepancy(decimal targetTotal, decimal[] lineAmounts, IReadOnlyList<decimal> baseAmounts)
    {
        if (lineAmounts == null || lineAmounts.Length == 0) return;

        targetTotal = RoundMoney(targetTotal);
        decimal currentSum = lineAmounts.Sum();
        decimal diff = targetTotal - currentSum;
        if (diff == 0) return;

        int diffCents = (int)Math.Round(diff * 100m, MidpointRounding.ToEven);
        if (diffCents == 0) return;

        // En yüksek tutarlı satırların indeksleri
        var sortedIndices = Enumerable.Range(0, lineAmounts.Length)
            .OrderByDescending(i => baseAmounts != null && i < baseAmounts.Count ? baseAmounts[i] : lineAmounts[i])
            .ToList();

        int step = diffCents > 0 ? 1 : -1;
        int remainingSteps = Math.Abs(diffCents);

        for (int i = 0; i < remainingSteps && i < sortedIndices.Count; i++)
        {
            lineAmounts[sortedIndices[i]] += step * 0.01m;
        }
    }

    /// <summary>
    /// Çok satırlı teklif kalemleri için net, KDV ve KDV dahil tutarları kuruşu kuruşuna hesaplar.
    /// </summary>
    public static MultiLineCalculationResult CalculateMultiLine(
        IReadOnlyList<LineCalculationInput> lines,
        decimal overallDiscountAmount = 0m,
        decimal laborCost = 0m,
        decimal shippingCost = 0m,
        bool applyDiscountBeforeTax = false)
    {
        if (lines == null || lines.Count == 0)
        {
            decimal net = RoundMoney(laborCost + shippingCost - overallDiscountAmount);
            return new MultiLineCalculationResult(
                SubTotal: 0m,
                OverallDiscount: RoundMoney(overallDiscountAmount),
                NetTotal: Math.Max(0m, net),
                TaxTotal: 0m,
                GrandTotal: Math.Max(0m, net),
                LaborCost: RoundMoney(laborCost),
                ShippingCost: RoundMoney(shippingCost),
                Lines: Array.Empty<CalculatedLine>());
        }

        int count = lines.Count;
        var unallocatedNets = new decimal[count];
        for (int i = 0; i < count; i++)
        {
            var line = lines[i];
            decimal gross = line.Quantity * line.UnitPrice;
            decimal lineDiscount = line.DiscountPercent > 0
                ? RoundMoney(gross * (line.DiscountPercent / 100m))
                : 0m;
            unallocatedNets[i] = Math.Max(0m, RoundMoney(gross - lineDiscount));
        }

        decimal subtotal = unallocatedNets.Sum();
        decimal totalDiscount = RoundMoney(Math.Min(subtotal, Math.Max(0m, overallDiscountAmount)));

        // Alt iskontonun satırlara Largest Remainder ile dağıtılması
        decimal[] allocatedDiscounts = DistributeLargestRemainder(totalDiscount, unallocatedNets);

        var allocatedNets = new decimal[count];
        var rawTaxes = new decimal[count];
        var lineTaxes = new decimal[count];

        for (int i = 0; i < count; i++)
        {
            allocatedNets[i] = Math.Max(0m, unallocatedNets[i] - allocatedDiscounts[i]);
            decimal taxBase = applyDiscountBeforeTax ? allocatedNets[i] : unallocatedNets[i];
            decimal taxRate = lines[i].TaxPercent;
            decimal rawTax = taxBase * (taxRate / 100m);
            rawTaxes[i] = rawTax;
            lineTaxes[i] = RoundMoney(rawTax);
        }

        // Satır bazlı KDV toplamı ve Largest Remainder kuruş dengelemesi
        decimal targetTaxTotal = RoundMoney(rawTaxes.Sum());
        BalancePennyDiscrepancy(targetTaxTotal, lineTaxes, allocatedNets);

        decimal netMaterial = allocatedNets.Sum();
        decimal netTotal = RoundMoney(netMaterial + laborCost + shippingCost);
        decimal grandTotal = RoundMoney(netTotal + lineTaxes.Sum());

        var calculatedLines = new List<CalculatedLine>(count);
        for (int i = 0; i < count; i++)
        {
            var line = lines[i];
            decimal lineTotalWithTax = RoundMoney(allocatedNets[i] + lineTaxes[i]);
            calculatedLines.Add(new CalculatedLine(
                SourceId: line.SourceId,
                ProductId: line.ProductId,
                ProductName: line.ProductName,
                Quantity: line.Quantity,
                UnitPrice: line.UnitPrice,
                DiscountPercent: line.DiscountPercent,
                TaxPercent: line.TaxPercent,
                UnallocatedNet: unallocatedNets[i],
                AllocatedDiscount: allocatedDiscounts[i],
                NetAfterDiscount: allocatedNets[i],
                TaxAmount: lineTaxes[i],
                TotalWithTax: lineTotalWithTax));
        }

        return new MultiLineCalculationResult(
            SubTotal: subtotal,
            OverallDiscount: totalDiscount,
            NetTotal: netTotal,
            TaxTotal: lineTaxes.Sum(),
            GrandTotal: grandTotal,
            LaborCost: RoundMoney(laborCost),
            ShippingCost: RoundMoney(shippingCost),
            Lines: calculatedLines);
    }
}

public sealed record LineCalculationInput(
    int? SourceId,
    int? ProductId,
    string ProductName,
    decimal Quantity,
    decimal UnitPrice,
    decimal DiscountPercent,
    decimal TaxPercent);

public sealed record CalculatedLine(
    int? SourceId,
    int? ProductId,
    string ProductName,
    decimal Quantity,
    decimal UnitPrice,
    decimal DiscountPercent,
    decimal TaxPercent,
    decimal UnallocatedNet,
    decimal AllocatedDiscount,
    decimal NetAfterDiscount,
    decimal TaxAmount,
    decimal TotalWithTax);

public sealed record MultiLineCalculationResult(
    decimal SubTotal,
    decimal OverallDiscount,
    decimal NetTotal,
    decimal TaxTotal,
    decimal GrandTotal,
    decimal LaborCost,
    decimal ShippingCost,
    IReadOnlyList<CalculatedLine> Lines);
