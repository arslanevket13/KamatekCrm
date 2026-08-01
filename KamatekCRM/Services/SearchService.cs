using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KamatekCrm.Infrastructure.Data;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;
using Microsoft.EntityFrameworkCore;
using KamatekCrm.ApplicationCore.Interfaces;
using KamatekCrm.ApplicationCore.Security;

namespace KamatekCrm.Services
{
    /// <summary>
    /// Global arama sonucu
    /// </summary>
    public class SearchResult
    {
        public string Icon { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Subtitle { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string CategoryColor { get; set; } = "#757575";
        public int Id { get; set; }
        public string EntityType { get; set; } = string.Empty;
    }

    public interface ISearchService
    {
        List<SearchResult> Search(string query, int maxResults = 20);
        Task<List<SearchResult>> SearchAsync(string query, int maxResults = 20, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Global arama servisi - Müşteri, Ürün, İş Emri arama
    /// </summary>
    public class SearchService : ISearchService
    {
        private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
        private readonly IPersonalDataProtectionService _personalDataProtection;

        public SearchService(
            IDbContextFactory<AppDbContext> dbContextFactory,
            IPersonalDataProtectionService personalDataProtection)
        {
            _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
            _personalDataProtection = personalDataProtection;
        }

        /// <summary>
        /// Tüm entity'lerde arama yap (Senkron)
        /// </summary>
        public List<SearchResult> Search(string query, int maxResults = 20)
        {
            if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
                return new List<SearchResult>();

            var results = new List<SearchResult>();
            var search = query.ToLower().Trim();
            var canSearchContactData = _personalDataProtection.CanView(PersonalDataKind.Phone);

            try
            {
                using var context = _dbContextFactory.CreateDbContext();

                // Müşterilerde ara
                var customers = context.Customers
                    .Where(c => c.FullName.ToLower().Contains(search) ||
                               (canSearchContactData && c.PhoneNumber != null && c.PhoneNumber.Contains(search)) ||
                               (canSearchContactData && c.Email != null && c.Email.ToLower().Contains(search)) ||
                               (c.CustomerCode != null && c.CustomerCode.ToLower().Contains(search)))
                    .Take(maxResults / 3)
                    .ToList();

                foreach (var c in customers)
                {
                    results.Add(new SearchResult
                    {
                        Icon = "👤",
                        Title = c.FullName,
                        Subtitle = _personalDataProtection.Protect(
                            c.PhoneNumber ?? c.Email,
                            string.IsNullOrWhiteSpace(c.PhoneNumber) ? PersonalDataKind.Email : PersonalDataKind.Phone),
                        Category = "Müşteri",
                        CategoryColor = "#1976D2",
                        Id = c.Id,
                        EntityType = "Customer"
                    });
                }

                // Ürünlerde ara
                var products = context.Products
                    .Where(p => p.ProductName.ToLower().Contains(search) ||
                               (p.SKU != null && p.SKU.ToLower().Contains(search)) ||
                               (p.Barcode != null && p.Barcode.Contains(search)))
                    .Take(maxResults / 3)
                    .ToList();

                foreach (var p in products)
                {
                    results.Add(new SearchResult
                    {
                        Icon = "📦",
                        Title = p.ProductName,
                        Subtitle = $"Stok: {p.TotalStockQuantity} | {p.SalePrice:N2} ₺",
                        Category = "Ürün",
                        CategoryColor = "#388E3C",
                        Id = p.Id,
                        EntityType = "Product"
                    });
                }

                // İş Emirlerinde ara
                var jobs = context.ServiceJobs
                    .Where(j => j.Description.ToLower().Contains(search))
                    .Take(maxResults / 3)
                    .ToList();

                foreach (var j in jobs)
                {
                    results.Add(new SearchResult
                    {
                        Icon = "🔧",
                        Title = $"İş #{j.Id}",
                        Subtitle = j.Description.Length > 50 ? j.Description.Substring(0, 50) + "..." : j.Description,
                        Category = GetStatusText(j.Status),
                        CategoryColor = GetStatusColor(j.Status),
                        Id = j.Id,
                        EntityType = "ServiceJob"
                    });
                }
            }
            catch
            {
                // Arama hatası sessizce geçilir
            }

            return results.Take(maxResults).ToList();
        }

        /// <summary>
        /// Tüm entity'lerde arama yap (Asenkron)
        /// </summary>
        public async Task<List<SearchResult>> SearchAsync(string query, int maxResults = 20, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
                return new List<SearchResult>();

            var results = new List<SearchResult>();
            var search = query.ToLower().Trim();
            var canSearchContactData = _personalDataProtection.CanView(PersonalDataKind.Phone);

            try
            {
                await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

                var customers = await context.Customers
                    .Where(c => c.FullName.ToLower().Contains(search) ||
                               (canSearchContactData && c.PhoneNumber != null && c.PhoneNumber.Contains(search)) ||
                               (canSearchContactData && c.Email != null && c.Email.ToLower().Contains(search)) ||
                               (c.CustomerCode != null && c.CustomerCode.ToLower().Contains(search)))
                    .Take(maxResults / 3)
                    .ToListAsync(cancellationToken);

                foreach (var c in customers)
                {
                    results.Add(new SearchResult
                    {
                        Icon = "👤",
                        Title = c.FullName,
                        Subtitle = _personalDataProtection.Protect(
                            c.PhoneNumber ?? c.Email,
                            string.IsNullOrWhiteSpace(c.PhoneNumber) ? PersonalDataKind.Email : PersonalDataKind.Phone),
                        Category = "Müşteri",
                        CategoryColor = "#1976D2",
                        Id = c.Id,
                        EntityType = "Customer"
                    });
                }

                var products = await context.Products
                    .Where(p => p.ProductName.ToLower().Contains(search) ||
                               (p.SKU != null && p.SKU.ToLower().Contains(search)) ||
                               (p.Barcode != null && p.Barcode.Contains(search)))
                    .Take(maxResults / 3)
                    .ToListAsync(cancellationToken);

                foreach (var p in products)
                {
                    results.Add(new SearchResult
                    {
                        Icon = "📦",
                        Title = p.ProductName,
                        Subtitle = $"Stok: {p.TotalStockQuantity} | {p.SalePrice:N2} ₺",
                        Category = "Ürün",
                        CategoryColor = "#388E3C",
                        Id = p.Id,
                        EntityType = "Product"
                    });
                }

                var jobs = await context.ServiceJobs
                    .Where(j => j.Description.ToLower().Contains(search))
                    .Take(maxResults / 3)
                    .ToListAsync(cancellationToken);

                foreach (var j in jobs)
                {
                    results.Add(new SearchResult
                    {
                        Icon = "🔧",
                        Title = $"İş #{j.Id}",
                        Subtitle = j.Description.Length > 50 ? j.Description.Substring(0, 50) + "..." : j.Description,
                        Category = GetStatusText(j.Status),
                        CategoryColor = GetStatusColor(j.Status),
                        Id = j.Id,
                        EntityType = "ServiceJob"
                    });
                }
            }
            catch
            {
            }

            return results.Take(maxResults).ToList();
        }

        private static string GetStatusText(JobStatus status)
        {
            return status switch
            {
                JobStatus.Pending => "Bekliyor",
                JobStatus.InProgress => "Devam Ediyor",
                JobStatus.Completed => "Tamamlandı",
                _ => "Bilinmiyor"
            };
        }

        private static string GetStatusColor(JobStatus status)
        {
            return status switch
            {
                JobStatus.Pending => "#FF9800",
                JobStatus.InProgress => "#2196F3",
                JobStatus.Completed => "#4CAF50",
                _ => "#757575"
            };
        }
    }
}
