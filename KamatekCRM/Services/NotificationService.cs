using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KamatekCrm.Data;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace KamatekCrm.Services
{
    public class NotificationService
    {
        // Memory Cache for displayed notifications to prevent loops
        // Gerçek hayat senaryosunda bu bir DB tablosu veya LocalStorage olmalı.
        private static readonly HashSet<string> _readNotifications = new HashSet<string>();
        
        // Servis sınıfı her çağrıldığında yeni instance oluşuyorsa Static kullanmak zorundayız.
        // Singleton olarak kaydedildiyse instance field olabilir. Güvence için static kullanıyoruz.

        public async Task<List<NotificationItem>> GetNotificationsAsync()
        {
            var notifications = new List<NotificationItem>();

            try
            {
                using (var context = new AppDbContext())
                {
                    // 1. Düşük Stok Uyarısı
                    var lowStock = await context.Products
                        .Where(p => p.TotalStockQuantity <= 5)
                        .Select(p => new { p.ProductName, p.TotalStockQuantity })
                        .Take(5)
                        .ToListAsync();

                    foreach (var item in lowStock)
                    {
                        var key = $"STOCK_{item.ProductName}_{DateTime.Today.ToShortDateString()}";
                        if (!_readNotifications.Contains(key)) // Okunmadıysa ekle
                        {
                            notifications.Add(new NotificationItem
                            {
                                Id = key,
                                Title = "Düşük Stok",
                                Message = $"{item.ProductName} stoğu kritik seviyede ({item.TotalStockQuantity} adet).",
                                Type = NotificationType.Warning,
                                ActionLabel = "Sipariş Ver"
                            });
                        }
                    }

                    // 2. Unutulmuş Teklifler (7 günden eski Lead/Quoted)
                    var staleDate = DateTime.Today.AddDays(-7);
                    var staleQuotes = await context.ServiceProjects
                        .Include(p => p.Customer)
                        .Where(p => (p.PipelineStage == PipelineStage.Lead || p.PipelineStage == PipelineStage.Quoted) 
                                 && p.CreatedDate <= staleDate)
                        .Take(5)
                        .ToListAsync();

                    foreach (var quote in staleQuotes)
                    {
                        var key = $"QUOTE_{quote.Id}_{DateTime.Today.ToShortDateString()}";
                        if (!_readNotifications.Contains(key))
                        {
                            notifications.Add(new NotificationItem
                            {
                                Id = key,
                                Title = "Bekleyen Fırsat",
                                Message = $"{quote.Customer?.FullName} - {quote.Title} (7+ gündür işlem görmedi).",
                                Type = NotificationType.Info,
                                ActionLabel = "İncele"
                            });
                        }
                    }
                }
            }
            catch (PostgresException pgEx)
            {
                System.Diagnostics.Debug.WriteLine($"[CRITICAL] Notification Service Database Error: {pgEx.Message}");
                return new List<NotificationItem>(); // UI çökmesini engelle
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Notification Service General Error: {ex.Message}");
                return new List<NotificationItem>(); // UI çökmesini engelle
            }

            return notifications;
        }

        /// <summary>
        /// Bildirimi okundu olarak işaretle
        /// </summary>
        public void MarkAsRead(string id)
        {
            if (!string.IsNullOrEmpty(id))
            {
                _readNotifications.Add(id);
            }
        }
    }

    public class NotificationItem
    {
        public string Id { get; set; } = string.Empty; // Unique Key for tracking read state
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public NotificationType Type { get; set; } = NotificationType.Info;
        public string ActionLabel { get; set; } = string.Empty;
    }

    public enum NotificationType
    {
        Info,
        Warning,
        Error,
        Success
    }
}
