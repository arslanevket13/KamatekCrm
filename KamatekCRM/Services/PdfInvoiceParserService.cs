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
        private readonly InvoiceScannerService _scannerService;
        private readonly Microsoft.EntityFrameworkCore.IDbContextFactory<KamatekCrm.Infrastructure.Data.AppDbContext>? _dbContextFactory;

        public PdfInvoiceParserService(
            InvoiceScannerService? scannerService = null,
            Microsoft.EntityFrameworkCore.IDbContextFactory<KamatekCrm.Infrastructure.Data.AppDbContext>? dbContextFactory = null)
        {
            _scannerService = scannerService ?? new InvoiceScannerService();
            _dbContextFactory = dbContextFactory ?? (App.ServiceProvider?.GetService(typeof(Microsoft.EntityFrameworkCore.IDbContextFactory<KamatekCrm.Infrastructure.Data.AppDbContext>)) as Microsoft.EntityFrameworkCore.IDbContextFactory<KamatekCrm.Infrastructure.Data.AppDbContext>);
        }

        public List<PurchaseOrderItem> Parse(string filePath, List<Product>? availableProducts = null)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !System.IO.File.Exists(filePath))
                throw new System.IO.FileNotFoundException("PDF dosyası bulunamadı.", filePath);

            // Eğer ürün listesi dışarıdan verilmediyse ve DbContext mevcutsa çek
            if (availableProducts == null && _dbContextFactory != null)
            {
                try
                {
                    using var context = _dbContextFactory.CreateDbContext();
                    availableProducts = Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AsNoTracking(context.Products).ToList();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[PdfInvoiceParserService] Ürün listesi çekilemedi: {ex.Message}");
                }
            }

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
                            var item = ParseTxLine(line, availableProducts);
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
                throw new Exception($"PDF okunurken hata oluştu: {ex.Message}", ex);
            }

            return items;
        }

        private PurchaseOrderItem? ParseTxLine(string line, List<Product>? availableProducts)
        {
            var match = Regex.Match(line, @"^(.*)\s+(\d+)\s+([\d.,]+)\s+([\d.,]+)$");
            if (match.Success)
            {
                var name = match.Groups[1].Value.Trim();
                var qtyStr = match.Groups[2].Value;
                var priceStr = match.Groups[3].Value;

                if (decimal.TryParse(priceStr.Replace(".", "").Replace(",", "."), out decimal price) &&
                    int.TryParse(qtyStr, out int qty))
                {
                    if (name.Length > 2 && !name.Contains("Toplam", StringComparison.OrdinalIgnoreCase))
                    {
                        var item = new PurchaseOrderItem
                        {
                            ProductName = name,
                            Quantity = qty,
                            UnitPrice = price,
                            LineTotal = qty * price
                        };

                        // Kademeli Akıllı Eşleştirme (Barkod/SKU -> Normalize İsim -> Levenshtein/Dice >= %80)
                        if (availableProducts != null && availableProducts.Count > 0)
                        {
                            var matchedProduct = _scannerService.FindBestMatch(name, availableProducts);
                            if (matchedProduct != null)
                            {
                                item.ProductId = matchedProduct.Id;
                                item.ProductName = matchedProduct.ProductName;
                            }
                        }

                        return item;
                    }
                }
            }

            return null;
        }
    }
}
