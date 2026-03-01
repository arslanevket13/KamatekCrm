using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KamatekCrm.Shared.Models;
using KamatekCrm.Data;
using KamatekCrm.API.Models;
using KamatekCrm.API.Hubs;
using Microsoft.AspNetCore.Authorization;

namespace KamatekCrm.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class LocationController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly INotificationService _notifications;
        private readonly ILogger<LocationController> _logger;

        public LocationController(AppDbContext context, INotificationService notifications, ILogger<LocationController> logger)
        {
            _context = context;
            _notifications = notifications;
            _logger = logger;
        }

        /// <summary>
        /// Teknisyen konum güncelle — Mobil uygulama bu endpoint'e konum gönderir
        /// </summary>
        [HttpPost("update")]
        public async Task<IActionResult> UpdateLocation([FromBody] LocationUpdateRequest request)
        {
            var location = new TechnicianLocation
            {
                UserId = request.UserId,
                Latitude = request.Latitude,
                Longitude = request.Longitude,
                Accuracy = request.Accuracy,
                Speed = request.Speed,
                Heading = request.Heading,
                BatteryLevel = request.BatteryLevel,
                IsBackground = request.IsBackground,
                Source = request.Source,
                Timestamp = DateTime.UtcNow
            };

            _context.TechnicianLocations.Add(location);
            await _context.SaveChangesAsync();

            return Ok(ApiResponse.Ok("Konum güncellendi."));
        }

        /// <summary>
        /// Toplu konum güncelle — Offline'dayken biriken konumları gönder
        /// </summary>
        [HttpPost("batch")]
        public async Task<IActionResult> BatchUpdateLocations([FromBody] List<LocationUpdateRequest> requests)
        {
            var locations = requests.Select(r => new TechnicianLocation
            {
                UserId = r.UserId,
                Latitude = r.Latitude,
                Longitude = r.Longitude,
                Accuracy = r.Accuracy,
                Speed = r.Speed,
                Heading = r.Heading,
                BatteryLevel = r.BatteryLevel,
                IsBackground = r.IsBackground,
                Source = r.Source,
                Timestamp = r.Timestamp ?? DateTime.UtcNow
            }).ToList();

            _context.TechnicianLocations.AddRange(locations);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Batch location update: {Count} points from User {UserId}",
                locations.Count, requests.FirstOrDefault()?.UserId);

            return Ok(ApiResponse.Ok($"{locations.Count} konum kaydedildi."));
        }

        /// <summary>
        /// Tüm aktif teknisyenlerin son konumu — Admin harita ekranı
        /// </summary>
        [HttpGet("active-technicians")]
        public async Task<IActionResult> GetActiveTechnicians()
        {
            var cutoff = DateTime.UtcNow.AddMinutes(-30);

            var locations = await _context.TechnicianLocations
                .Include(l => l.User)
                .Where(l => l.Timestamp >= cutoff)
                .GroupBy(l => l.UserId)
                .Select(g => g.OrderByDescending(l => l.Timestamp).First())
                .ToListAsync();

            var result = locations.Select(l => new
            {
                l.UserId,
                TechnicianName = l.User.Ad + " " + l.User.Soyad,
                l.Latitude,
                l.Longitude,
                l.Speed,
                l.Heading,
                l.BatteryLevel,
                l.Timestamp,
                MinutesAgo = (int)(DateTime.UtcNow - l.Timestamp).TotalMinutes,
                IsMoving = l.Speed > 3 // 3 km/h üstü hareket
            });

            return Ok(ApiResponse<object>.Ok(result));
        }

        /// <summary>
        /// Tek teknisyenin konum geçmişi — gün bazlı
        /// </summary>
        [HttpGet("history/{userId}")]
        public async Task<IActionResult> GetLocationHistory(int userId,
            [FromQuery] DateTime? date)
        {
            var targetDate = date?.Date ?? DateTime.UtcNow.Date;
            var nextDay = targetDate.AddDays(1);

            var history = await _context.TechnicianLocations
                .Where(l => l.UserId == userId && l.Timestamp >= targetDate && l.Timestamp < nextDay)
                .OrderBy(l => l.Timestamp)
                .Select(l => new
                {
                    l.Latitude,
                    l.Longitude,
                    l.Speed,
                    l.Timestamp,
                    l.BatteryLevel,
                    l.IsBackground
                })
                .ToListAsync();

            // Toplam mesafe hesapla (Haversine)
            double totalDistance = 0;
            for (int i = 1; i < history.Count; i++)
            {
                totalDistance += CalculateDistance(
                    history[i - 1].Latitude, history[i - 1].Longitude,
                    history[i].Latitude, history[i].Longitude);
            }

            return Ok(ApiResponse<object>.Ok(new
            {
                UserId = userId,
                Date = targetDate,
                Points = history,
                TotalPoints = history.Count,
                TotalDistanceKm = Math.Round(totalDistance, 2),
                TotalDurationMinutes = history.Count >= 2
                    ? (int)(history.Last().Timestamp - history.First().Timestamp).TotalMinutes : 0
            }));
        }

        /// <summary>
        /// Müşteriye en yakın teknisyenler — iş atama için
        /// </summary>
        [HttpGet("nearest")]
        public async Task<IActionResult> GetNearestTechnicians(
            [FromQuery] double latitude, [FromQuery] double longitude, [FromQuery] int top = 5)
        {
            var cutoff = DateTime.UtcNow.AddMinutes(-30);

            var locations = await _context.TechnicianLocations
                .Include(l => l.User)
                .Where(l => l.Timestamp >= cutoff)
                .GroupBy(l => l.UserId)
                .Select(g => g.OrderByDescending(l => l.Timestamp).First())
                .ToListAsync();

            var nearest = locations
                .Select(l => new
                {
                    l.UserId,
                    TechnicianName = l.User.Ad + " " + l.User.Soyad,
                    l.User.Phone,
                    l.Latitude,
                    l.Longitude,
                    DistanceKm = Math.Round(CalculateDistance(latitude, longitude, l.Latitude, l.Longitude), 2),
                    l.Speed,
                    l.BatteryLevel,
                    l.Timestamp
                })
                .OrderBy(x => x.DistanceKm)
                .Take(top)
                .ToList();

            return Ok(ApiResponse<object>.Ok(nearest));
        }

        /// <summary>
        /// Rota planı kaydet — günlük iş rotası
        /// </summary>
        [HttpPost("route-plan")]
        public async Task<IActionResult> CreateRoutePlan([FromBody] List<RoutePoint> points)
        {
            foreach (var point in points)
            {
                point.Date = DateTime.UtcNow.Date;
            }
            _context.RoutePoints.AddRange(points);
            await _context.SaveChangesAsync();
            return Ok(ApiResponse.Ok($"{points.Count} rota noktası kaydedildi."));
        }

        /// <summary>
        /// Teknisyenin günlük rota planı
        /// </summary>
        [HttpGet("route-plan/{userId}")]
        public async Task<IActionResult> GetRoutePlan(int userId, [FromQuery] DateTime? date)
        {
            var targetDate = date?.Date ?? DateTime.UtcNow.Date;

            var route = await _context.RoutePoints
                .Include(r => r.ServiceJob)
                .Where(r => r.UserId == userId && r.Date == targetDate)
                .OrderBy(r => r.OrderIndex)
                .ToListAsync();

            return Ok(ApiResponse<object>.Ok(route));
        }

        /// <summary>Haversine mesafe hesabı (km)</summary>
        private static double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371; // Dünya yarıçapı km
            var dLat = ToRad(lat2 - lat1);
            var dLon = ToRad(lon2 - lon1);
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }

        private static double ToRad(double deg) => deg * Math.PI / 180;
    }

    public class LocationUpdateRequest
    {
        public int UserId { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double? Accuracy { get; set; }
        public double? Speed { get; set; }
        public double? Heading { get; set; }
        public int? BatteryLevel { get; set; }
        public bool IsBackground { get; set; }
        public string? Source { get; set; }
        public DateTime? Timestamp { get; set; }
    }
}
