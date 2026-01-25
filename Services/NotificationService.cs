using System;
using System.Collections.Generic;
using System.Linq;
using KamatekCrm.Data;
using KamatekCrm.Enums;
using KamatekCrm.Models;
using Microsoft.EntityFrameworkCore;

namespace KamatekCrm.Services
{
    public class NotificationService
    {
        public List<NotificationItem> GetNotifications()
        {
            var notifications = new List<NotificationItem>();

            try
            {
                using (var context = new AppDbContext())
                {
                    // 1. Düşük Stok Uyarısı
                    var lowStock = context.Products
                        .Where(p => p.TotalStockQuantity <= 5)
                        .Select(p => new { p.ProductName, p.TotalStockQuantity })
                        .Take(5)
                        .ToList();

                    foreach (var item in lowStock)
                    {
                        notifications.Add(new NotificationItem
                        {
                            Title = "Düşük Stok",
                            Message = $"{item.ProductName} stoğu kritik seviyede ({item.TotalStockQuantity} adet).",
                            Type = NotificationType.Warning,
                            ActionLabel = "Sipariş Ver"
                        });
                    }

                    // 2. Unutulmuş Teklifler (7 günden eski Lead/Quoted)
                    var staleDate = DateTime.Today.AddDays(-7);
                    var staleQuotes = context.ServiceProjects // ServiceProject kullanıyoruz
                        .Include(p => p.Customer)
                        .Where(p => (p.PipelineStage == PipelineStage.Lead || p.PipelineStage == PipelineStage.Quoted) 
                                 && p.CreatedDate <= staleDate)
                        .Take(5)
                        .ToList();

                    foreach (var quote in staleQuotes)
                    {
                        notifications.Add(new NotificationItem
                        {
                            Title = "Bekleyen Fırsat",
                            Message = $"{quote.Customer?.FullName} - {quote.Title} (7+ gündür işlem görmedi).",
                            Type = NotificationType.Info,
                            ActionLabel = "İncele"
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Notification Service Error: {ex.Message}");
            }

            return notifications;
        }
    }

    public class NotificationItem
    {
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
