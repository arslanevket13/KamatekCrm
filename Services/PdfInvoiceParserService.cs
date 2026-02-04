using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using KamatekCrm.Shared.Models;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace KamatekCrm.Services
{
    public class PdfInvoiceParserService
    {
        public List<PurchaseOrderItem> Parse(string filePath)
        {
            var items = new List<PurchaseOrderItem>();

            try
            {
                using (var document = PdfDocument.Open(filePath))
                {
                    foreach (var page in document.GetPages())
                    {
                        var text = page.Text;
                        var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                        foreach (var line in lines)
                        {
                            var item = ParseTxLine(line);
                            if (item != null)
                            {
                                items.Add(item);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"PDF okunurken hata oluştu: {ex.Message}");
            }

            return items;
        }

        private PurchaseOrderItem? ParseTxLine(string line)
        {
            // Simple Heuristic: Look for patterns like "Product Name ... 10 ... 100.00"
            // This is a naive implementation and would need refinement for specific invoice formats.
            // Assuming format: [Product Name] [Quantity] [Unit Price] [Total]
            // Or: [Code] [Product Name] [Quantity] [Unit] [Price] ...

            // Regex strategies
            
            // Strategy 1: End of line contains Price and Quantity
            // Example: "Laptop 15.6 inch 2 15000,00 30000,00"
            // Regex: (.*?) (\d+) (\d+[.,]\d+) (\d+[.,]\d+)$
            
            var match = Regex.Match(line, @"^(.*)\s+(\d+)\s+([\d.,]+)\s+([\d.,]+)$");
            if (match.Success)
            {
                var name = match.Groups[1].Value.Trim();
                var qtyStr = match.Groups[2].Value;
                var priceStr = match.Groups[3].Value; // Unit Price

                if (decimal.TryParse(priceStr.Replace(".", "").Replace(",", "."), out decimal price) &&
                    int.TryParse(qtyStr, out int qty))
                {
                    // Clean up name
                    if (name.Length > 2 && !name.Contains("Toplam", StringComparison.OrdinalIgnoreCase))
                    {
                         return new PurchaseOrderItem
                         {
                             ProductName = name,
                             Quantity = qty,
                             UnitPrice = price,
                             LineTotal = qty * price
                         };
                    }
                }
            }

            return null;
        }
    }
}
