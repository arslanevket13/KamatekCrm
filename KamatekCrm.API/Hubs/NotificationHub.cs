using Microsoft.AspNetCore.SignalR;
using Serilog;

namespace KamatekCrm.API.Hubs
{
    /// <summary>
    /// SignalR Real-time Hub — Anlık bildirimler ve canlı veri güncellemeleri.
    /// 
    /// Client tarafında dinlenecek event'ler:
    ///   - "ReceiveNotification"    → Genel bildirimler
    ///   - "JobStatusChanged"       → İş durumu değişikliği
    ///   - "NewJobAssigned"         → Yeni iş atandı
    ///   - "StockAlert"             → Stok uyarısı
    ///   - "DashboardRefresh"       → Dashboard verisi güncellendi
    ///   - "UserOnlineStatus"       → Kullanıcı çevrimiçi/çevrimdışı
    /// </summary>
    public class NotificationHub : Hub
    {
        /// <summary>
        /// Bağlantı kurulduğunda — kullanıcıyı rol grubuna ekle
        /// </summary>
        public override async Task OnConnectedAsync()
        {
            var userId = Context.UserIdentifier;
            var role = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;

            if (!string.IsNullOrEmpty(role))
                await Groups.AddToGroupAsync(Context.ConnectionId, $"role:{role}");

            if (!string.IsNullOrEmpty(userId))
                await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId}");

            Log.Information("SignalR connected: User={UserId} Role={Role} ConnectionId={ConnectionId}",
                userId, role, Context.ConnectionId);

            // Diğer kullanıcılara online bildir
            await Clients.Others.SendAsync("UserOnlineStatus", new
            {
                UserId = userId,
                IsOnline = true,
                ConnectedAt = DateTime.UtcNow
            });

            await base.OnConnectedAsync();
        }

        /// <summary>
        /// Bağlantı koptuğunda
        /// </summary>
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = Context.UserIdentifier;

            await Clients.Others.SendAsync("UserOnlineStatus", new
            {
                UserId = userId,
                IsOnline = false,
                DisconnectedAt = DateTime.UtcNow
            });

            Log.Information("SignalR disconnected: User={UserId}", userId);
            await base.OnDisconnectedAsync(exception);
        }

        /// <summary>
        /// Teknisyen GPS konumunu güncelle — sadece Admin grubuna gönder
        /// </summary>
        public async Task UpdateLocation(double latitude, double longitude)
        {
            var userId = Context.UserIdentifier;
            await Clients.Group("role:Admin").SendAsync("TechnicianLocationUpdate", new
            {
                UserId = userId,
                Latitude = latitude,
                Longitude = longitude,
                Timestamp = DateTime.UtcNow
            });
        }
    }

    /// <summary>
    /// Server-side bildirim gönderme servisi — Controller'lardan kullanılır
    /// </summary>
    public interface INotificationService
    {
        Task NotifyJobStatusChanged(int jobId, string oldStatus, string newStatus, string customerName);
        Task NotifyNewJobAssigned(int jobId, string title, int technicianUserId);
        Task NotifyStockAlert(string productName, int currentStock, int minStock);
        Task NotifyDashboardRefresh();
        Task NotifyAll(string title, string message, string type = "info");
    }

    public class NotificationService : INotificationService
    {
        private readonly IHubContext<NotificationHub> _hub;

        public NotificationService(IHubContext<NotificationHub> hub)
        {
            _hub = hub;
        }

        public async Task NotifyJobStatusChanged(int jobId, string oldStatus, string newStatus, string customerName)
        {
            await _hub.Clients.All.SendAsync("JobStatusChanged", new
            {
                JobId = jobId,
                OldStatus = oldStatus,
                NewStatus = newStatus,
                CustomerName = customerName,
                Timestamp = DateTime.UtcNow
            });
        }

        public async Task NotifyNewJobAssigned(int jobId, string title, int technicianUserId)
        {
            // Sadece ilgili teknisyene gönder
            await _hub.Clients.Group($"user:{technicianUserId}").SendAsync("NewJobAssigned", new
            {
                JobId = jobId,
                Title = title,
                Timestamp = DateTime.UtcNow
            });

            // Admin grubuna da bildir
            await _hub.Clients.Group("role:Admin").SendAsync("NewJobAssigned", new
            {
                JobId = jobId,
                Title = title,
                AssignedTo = technicianUserId,
                Timestamp = DateTime.UtcNow
            });
        }

        public async Task NotifyStockAlert(string productName, int currentStock, int minStock)
        {
            await _hub.Clients.Group("role:Admin").SendAsync("StockAlert", new
            {
                ProductName = productName,
                CurrentStock = currentStock,
                MinStock = minStock,
                Severity = currentStock == 0 ? "critical" : "warning",
                Timestamp = DateTime.UtcNow
            });
        }

        public async Task NotifyDashboardRefresh()
        {
            await _hub.Clients.All.SendAsync("DashboardRefresh", new
            {
                Timestamp = DateTime.UtcNow
            });
        }

        public async Task NotifyAll(string title, string message, string type = "info")
        {
            await _hub.Clients.All.SendAsync("ReceiveNotification", new
            {
                Title = title,
                Message = message,
                Type = type,
                Timestamp = DateTime.UtcNow
            });
        }
    }
}
