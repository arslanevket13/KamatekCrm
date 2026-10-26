using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using KamatekCrm.Shared.Models;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace KamatekCrm.Services
{
    public class InvoiceScannerService
    {
        /// <summary>
        /// Kademeli Akıllı Eşleştirme Algoritması:
        /// 1. Barkod veya SKU tam eşleşmesi
        /// 2. Normalize edilmiş ürün adı tam eşleşmesi (Türkçe karakter ve noktalama temizliği)
        /// 3. Levenshtein / Dice Coefficient benzerlik algoritması (En az %80 ve üzeri benzerlik)
        /// </summary>
        public Product? FindBestMatch(string description, List<Product> products)
        {
            if (string.IsNullOrWhiteSpace(description) || products == null || !products.Any()) return null;

            var trimmedDesc = description.Trim();

            // 1. ADIM: Barkod veya SKU tam eşleşmesi
            var skuMatch = products.FirstOrDefault(p =>
                (!string.IsNullOrWhiteSpace(p.SKU) && (string.Equals(p.SKU, trimmedDesc, StringComparison.OrdinalIgnoreCase) || trimmedDesc.Contains(p.SKU, StringComparison.OrdinalIgnoreCase))) ||
                (!string.IsNullOrWhiteSpace(p.Barcode) && (string.Equals(p.Barcode, trimmedDesc, StringComparison.OrdinalIgnoreCase) || trimmedDesc.Contains(p.Barcode, StringComparison.OrdinalIgnoreCase))));

            if (skuMatch != null) return skuMatch;

            // 2. ADIM: Normalize edilmiş Ürün Adı tam eşleşmesi
            var normalizedDesc = NormalizeText(trimmedDesc);
            var normalizedExact = products.FirstOrDefault(p => NormalizeText(p.ProductName) == normalizedDesc);
            if (normalizedExact != null) return normalizedExact;

            // 3. ADIM: Levenshtein ve Dice Bigram Benzerlik Skoru (>= 0.80 / %80)
            Product? bestMatch = null;
            double maxSimilarity = 0.0;

            foreach (var product in products)
            {
                var normProd = NormalizeText(product.ProductName);
                if (string.IsNullOrWhiteSpace(normProd)) continue;

                double similarity = CalculateCombinedSimilarity(normalizedDesc, normProd);

                if (similarity >= 0.80 && similarity > maxSimilarity)
                {
                    maxSimilarity = similarity;
                    bestMatch = product;
                }
            }

            return bestMatch;
        }

        public static string NormalizeText(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;

            string text = input.Trim().ToLowerInvariant()
                .Replace('ı', 'i')
                .Replace('ğ', 'g')
                .Replace('ü', 'u')
                .Replace('ş', 's')
                .Replace('ö', 'o')
                .Replace('ç', 'c');

            var chars = text.Select(c => char.IsLetterOrDigit(c) ? c : ' ').ToArray();
            text = new string(chars);

            return Regex.Replace(text, @"\s+", " ").Trim();
        }

        public static double CalculateCombinedSimilarity(string source, string target)
        {
            if (string.IsNullOrEmpty(source) && string.IsNullOrEmpty(target)) return 1.0;
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(target)) return 0.0;
            if (source == target) return 1.0;

            int levDistance = CalculateLevenshteinDistance(source, target);
            int maxLen = Math.Max(source.Length, target.Length);
            double levSim = maxLen == 0 ? 1.0 : 1.0 - ((double)levDistance / maxLen);

            double diceSim = CalculateDiceCoefficient(source, target);

            return Math.Max(levSim, diceSim);
        }

        private static double CalculateDiceCoefficient(string s1, string s2)
        {
            if (s1.Length < 2 || s2.Length < 2) return 0.0;

            var bigrams1 = new HashSet<string>();
            for (int i = 0; i < s1.Length - 1; i++)
            {
                bigrams1.Add(s1.Substring(i, 2));
            }

            int matches = 0;
            for (int i = 0; i < s2.Length - 1; i++)
            {
                if (bigrams1.Contains(s2.Substring(i, 2)))
                {
                    matches++;
                }
            }

            int totalBigrams = (s1.Length - 1) + (s2.Length - 1);
            return totalBigrams == 0 ? 0.0 : (2.0 * matches) / totalBigrams;
        }

        private static int CalculateLevenshteinDistance(string source, string target)
        {
            if (string.IsNullOrEmpty(source))
            {
                if (string.IsNullOrEmpty(target)) return 0;
                return target.Length;
            }
            if (string.IsNullOrEmpty(target)) return source.Length;

            int[,] d = new int[source.Length + 1, target.Length + 1];

            for (int i = 0; i <= source.Length; d[i, 0] = i++) { }
            for (int j = 0; j <= target.Length; d[0, j] = j++) { }

            for (int i = 1; i <= source.Length; i++)
            {
                for (int j = 1; j <= target.Length; j++)
                {
                    int cost = (target[j - 1] == source[i - 1]) ? 0 : 1;
                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost);
                }
            }
            return d[source.Length, target.Length];
        }

        public List<PurchaseOrderItem> ExtractItemsFromPdf(string filePath, List<Product> availableProducts)
        {
            var items = new List<PurchaseOrderItem>();
            var extractedLines = new List<string>();

            try
            {
                using (var document = PdfDocument.Open(filePath))
                {
                    foreach (var page in document.GetPages())
                    {
                        // Basit metin çıkarma (Satır satır analiz için)
                        // Daha gelişmiş tablolar için PdfPig'in Words/Letters API'si kullanılabilir
                        // Ancak faturalarda genellikle satırlar regex ile yakalanabilir
                        var text = page.Text;
                        var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                        extractedLines.AddRange(lines);
                    }
                }

                // Deseni: [Ürün Adı] [Miktar] [Birim] [Birim Fiyat] [Toplam]
                // Örnek Regex: (.+?)\s+(\d+(?:[.,]\d+)?)\s*(Adet|Kutu|Ay|Saat)?\s+(\d+(?:[.,]\d+)?)\s+(\d+(?:[.,]\d+)?)
                // NOT: Bu regex her fatura formatı için özelleştirilmelidir. Genelleştirilmiş bir yapı kullanacağız.
                
                // Strateji: Satırın sonunda sayısal değerler arıyoruz (Toplam, Fiyat, Miktar)
                // Genelde: ... Ürün Adı ... 5 Adet 100,00 TL 500,00 TL
                
                foreach (var line in extractedLines)
                {
                    // Satırı normalize et
                    var normalizedLine = line.Trim();
                    if (string.IsNullOrWhiteSpace(normalizedLine)) continue;

                    // Regex denemesi 1: Standart Tablo Satırı
                    // Grup 1: Ürün Adı (Metin)
                    // Grup 2: Miktar (Sayı) - Opsiyonel Birim ile
                    // Grup 3: Birim Fiyat (Sayı)
                    // Grup 4: Toplam (Sayı) - İsteğe bağlı
                    var match = Regex.Match(normalizedLine, @"^(.+?)\s+(\d+(?:[.,]\d+)?)\s+(?:Adet|Ad\.|Kg|Mt|M\.|Kutu|Set)?\s*(\d+(?:[.,]\d+)?)(?:\s+(\d+(?:[.,]\d+)?))?$", RegexOptions.IgnoreCase);

                    if (match.Success)
                    {
                        string desc = match.Groups[1].Value.Trim();
                        string qtyStr = match.Groups[2].Value.Replace(',', '.'); // Basit dönüşüm
                        string priceStr = match.Groups[3].Value.Replace(',', '.');

                        // Bazı formatlarda (1.000,00) nokta-virgül karmaşası olabilir.
                        // Türkiye formatı: 1.000,00 -> 1000.00
                        if (match.Groups[2].Value.Contains(',')) qtyStr = match.Groups[2].Value.Replace(".", "").Replace(",", ".");
                        if (match.Groups[3].Value.Contains(',')) priceStr = match.Groups[3].Value.Replace(".", "").Replace(",", ".");
                        
                        // Gereksiz "TL", "TRY" vb temizliği
                        desc = Regex.Replace(desc, @"\s+(\d+(?:[.,]\d+)?)$", ""); // Sonda kalan sayı varsa temizle

                        if (decimal.TryParse(qtyStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal qty) &&
                            decimal.TryParse(priceStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal price))
                        {
                            // Ürün Eşleştirme
                            var matchedProduct = FindBestMatch(desc, availableProducts);

                            var item = new PurchaseOrderItem
                            {
                                ProductName = matchedProduct?.ProductName ?? desc, // Eşleşme yoksa PDF'deki ismi kullan
                                ProductId = matchedProduct?.Id ?? 0,
                                Quantity = (int)qty,
                                UnitPrice = price,
                                SubTotal = (int)qty * price,
                                TaxRate = 20, // Varsayılan KDV
                            };
                            
                            // Hesaplamalar
                            item.DiscountAmount = 0;
                            item.TaxAmount = item.SubTotal * (item.TaxRate / 100m);
                            item.LineTotal = item.SubTotal + item.TaxAmount;

                            items.Add(item);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"PDF Parse Error: {ex.Message}");
                throw;
            }

            return items;
        }
    }
}
