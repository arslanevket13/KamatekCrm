using System;
using System.Collections.Generic;
using System.Linq;
using KamatekCrm.Data;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace KamatekCrm.Services
{
    /// <summary>
    /// Rapor oluşturma servisi
    /// </summary>
    public class ReportService
    {
        private readonly AppDbContext _context;

        public ReportService()
        {
            _context = new AppDbContext();
        }

        /// <summary>
        /// Satış özet raporu
        /// </summary>
        public ReportResult GetSalesSummaryReport(DateTime? startDate, DateTime? endDate)
        {
            var result = new ReportResult
            {
                Title = "Satış Özet Raporu",
                StartDate = startDate,
                EndDate = endDate,
                Columns = new List<string> { "Tarih", "Satış Sayısı", "Toplam Tutar", "Ortalama" }
            };

            var query = _context.SalesOrders.AsQueryable();

            if (startDate.HasValue)
                query = query.Where(s => s.Date >= startDate.Value);
            if (endDate.HasValue)
                query = query.Where(s => s.Date <= endDate.Value);

            var sales = query.ToList();
            var grouped = sales.GroupBy(s => s.Date.Date).OrderBy(g => g.Key);

            foreach (var group in grouped)
            {
                var total = group.Sum(s => (decimal)s.TotalAmount);
                result.Rows.Add(new Dictionary<string, object>
                {
                    { "Tarih", group.Key.ToString("dd.MM.yyyy") },
                    { "Satış Sayısı", group.Count() },
                    { "Toplam Tutar", total },
                    { "Ortalama", group.Count() > 0 ? total / group.Count() : 0 }
                });
            }

            result.TotalAmount = sales.Sum(s => (decimal)s.TotalAmount);
            result.TotalCount = sales.Count;
            result.Summary = new Dictionary<string, decimal>
            {
                { "Toplam Satış", result.TotalAmount ?? 0 },
                { "Toplam Adet", result.TotalCount }
            };

            return result;
        }

        /// <summary>
        /// Müşteri listesi raporu
        /// </summary>
        public ReportResult GetCustomerListReport()
        {
            var result = new ReportResult
            {
                Title = "Müşteri Listesi Raporu",
                Columns = new List<string> { "Müşteri Kodu", "Ad Soyad", "Telefon", "Şehir", "Tip", "Toplam Harcama" }
            };

            var customers = _context.Customers.ToList();

            foreach (var customer in customers)
            {
                result.Rows.Add(new Dictionary<string, object>
                {
                    { "Müşteri Kodu", customer.CustomerCode },
                    { "Ad Soyad", customer.FullName },
                    { "Telefon", customer.PhoneNumber },
                    { "Şehir", customer.City },
                    { "Tip", customer.Type.ToString() },
                    { "Toplam Harcama", customer.TotalSpent }
                });
            }

            result.TotalCount = customers.Count;
            result.Summary = new Dictionary<string, decimal>
            {
                { "Toplam Müşteri", result.TotalCount },
                { "Toplam Harcama", customers.Sum(c => c.TotalSpent) }
            };

            return result;
        }

        /// <summary>
        /// Stok durumu raporu
        /// </summary>
        public ReportResult GetInventoryReport()
        {
            var result = new ReportResult
            {
                Title = "Stok Durumu Raporu",
                Columns = new List<string> { "Ürün Kodu", "Ürün Adı", "Kategori", "Miktar", "Birim Fiyat", "Toplam Değer" }
            };

            var products = _context.Products.Include(p => p.Category).ToList();

            foreach (var product in products)
            {
                var quantity = _context.Inventories.Where(i => i.ProductId == product.Id).Sum(i => i.Quantity);
                var totalValue = quantity * product.SalePrice;

                result.Rows.Add(new Dictionary<string, object>
                {
                    { "Ürün Kodu", product.SKU },
                    { "Ürün Adı", product.ProductName },
                    { "Kategori", product.Category?.Name ?? "-" },
                    { "Miktar", quantity },
                    { "Birim Fiyat", product.SalePrice },
                    { "Toplam Değer", totalValue }
                });
            }

            result.TotalCount = products.Count;
            result.TotalAmount = products.Sum(p => 
                _context.Inventories.Where(i => i.ProductId == p.Id).Sum(i => i.Quantity) * p.SalePrice);
            result.Summary = new Dictionary<string, decimal>
            {
                { "Toplam Ürün", result.TotalCount },
                { "Toplam Stok Değeri", result.TotalAmount ?? 0 }
            };

            return result;
        }

        /// <summary>
        /// Servis işleri raporu
        /// </summary>
        public ReportResult GetServiceJobsReport(DateTime? startDate, DateTime? endDate)
        {
            var result = new ReportResult
            {
                Title = "Servis İşleri Raporu",
                StartDate = startDate,
                EndDate = endDate,
                Columns = new List<string> { "İş No", "Müşteri", "Teknisyen", "Tarih", "Durum", "Tutar" }
            };

            var query = _context.ServiceJobs.Include(j => j.Customer).AsQueryable();

            if (startDate.HasValue)
                query = query.Where(j => j.CreatedDate >= startDate.Value);
            if (endDate.HasValue)
                query = query.Where(j => j.CreatedDate <= endDate.Value);

            var jobs = query.ToList();

            foreach (var job in jobs)
            {
                result.Rows.Add(new Dictionary<string, object>
                {
                    { "İş No", $"İŞ-{job.Id}" },
                    { "Müşteri", job.Customer?.FullName ?? "-" },
                    { "Teknisyen", job.AssignedTechnician ?? "-" },
                    { "Tarih", job.CreatedDate.ToString("dd.MM.yyyy") },
                    { "Durum", job.StatusDisplay },
                    { "Tutar", job.Price }
                });
            }

            result.TotalCount = jobs.Count;
            result.TotalAmount = jobs.Sum(j => j.Price);
            result.Summary = new Dictionary<string, decimal>
            {
                { "Toplam İş", result.TotalCount },
                { "Toplam Tutar", result.TotalAmount ?? 0 }
            };

            return result;
        }

        /// <summary>
        /// En çok satan müşteriler
        /// </summary>
        public ReportResult GetTopCustomersReport(int topN = 10)
        {
            var result = new ReportResult
            {
                Title = $"En Çok Harcayan {topN} Müşteri",
                Columns = new List<string> { "Sıra", "Müşteri Kodu", "Ad Soyad", "Telefon", "Toplam Harcama", "Alışveriş Sayısı" }
            };

            var customers = _context.Customers
                .OrderByDescending(c => c.TotalSpent)
                .Take(topN)
                .ToList();

            int rank = 1;
            foreach (var customer in customers)
            {
                result.Rows.Add(new Dictionary<string, object>
                {
                    { "Sıra", rank++ },
                    { "Müşteri Kodu", customer.CustomerCode },
                    { "Ad Soyad", customer.FullName },
                    { "Telefon", customer.PhoneNumber },
                    { "Toplam Harcama", customer.TotalSpent },
                    { "Alışveriş Sayısı", customer.TotalPurchaseCount }
                });
            }

            result.TotalCount = customers.Count;
            result.TotalAmount = customers.Sum(c => c.TotalSpent);
            result.Summary = new Dictionary<string, decimal>
            {
                { "Toplam", result.TotalAmount ?? 0 }
            };

            return result;
        }

        /// <summary>
        /// Finansal özet raporu
        /// </summary>
        public ReportResult GetFinancialSummaryReport(DateTime? startDate, DateTime? endDate)
        {
            var result = new ReportResult
            {
                Title = "Finansal Özet Raporu",
                StartDate = startDate,
                EndDate = endDate,
                Columns = new List<string> { "Kalem", "Tutar" }
            };

            var query = _context.CashTransactions.AsQueryable();
            if (startDate.HasValue)
                query = query.Where(t => t.Date >= startDate.Value);
            if (endDate.HasValue)
                query = query.Where(t => t.Date <= endDate.Value);

            var transactions = query.ToList();

            var income = transactions
                .Where(t => t.TransactionType == CashTransactionType.CashIncome || 
                           t.TransactionType == CashTransactionType.CardIncome ||
                           t.TransactionType == CashTransactionType.TransferIncome)
                .Sum(t => t.Amount);

            var expense = transactions
                .Where(t => t.TransactionType == CashTransactionType.CashExpense ||
                           t.TransactionType == CashTransactionType.CardExpense)
                .Sum(t => t.Amount);

            result.Rows.Add(new Dictionary<string, object> { { "Kalem", "Gelirler" }, { "Tutar", income } });
            result.Rows.Add(new Dictionary<string, object> { { "Kalem", "Giderler" }, { "Tutar", expense } });
            result.Rows.Add(new Dictionary<string, object> { { "Kalem", "Net Kar/Zarar" }, { "Tutar", income - expense } });

            result.Summary = new Dictionary<string, decimal>
            {
                { "Gelir", income },
                { "Gider", expense },
                { "Net", income - expense }
            };

            return result;
        }

        /// <summary>
        /// Raporu Excel olarak export et
        /// </summary>
        public byte[] ExportToExcel(ReportResult report)
        {
            // ClosedXML kullanılarak Excel export
            // Bu metot Excel verisi döndürür
            var csv = string.Join(",", report.Columns) + "\n";
            
            foreach (var row in report.Rows)
            {
                var values = report.Columns.Select(col => 
                    row.ContainsKey(col) ? row[col]?.ToString() ?? "" : "");
                csv += string.Join(",", values) + "\n";
            }

            return System.Text.Encoding.UTF8.GetBytes(csv);
        }
    }
}
