using System.ComponentModel.DataAnnotations.Schema;

namespace KamatekCrm.Shared.Models
{
    /// <summary>
    /// Teknisyen konum kaydı — GPS koordinatları + zaman damgası.
    /// Mobil/web uygulamadan belirli aralıklarla gönderilir.
    /// </summary>
    public class TechnicianLocation
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double? Accuracy { get; set; }
        public double? Speed { get; set; }    // km/h
        public double? Heading { get; set; }  // derece (0-360)
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>Konum türü (GPS, WiFi, Cell)</summary>
        public string? Source { get; set; }

        /// <summary>Pil seviyesi (%)</summary>
        public int? BatteryLevel { get; set; }

        /// <summary>Uygulama ön/arka planda mı</summary>
        public bool IsBackground { get; set; }

        [ForeignKey(nameof(UserId))]
        public virtual User User { get; set; } = null!;
    }

    /// <summary>
    /// Rota planı — günlük iş sırası ve tahmini varış süreleri
    /// </summary>
    public class RoutePoint
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int? ServiceJobId { get; set; }
        public int? CustomerId { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string Address { get; set; } = "";
        public int OrderIndex { get; set; }
        public DateTime? EstimatedArrival { get; set; }
        public DateTime? ActualArrival { get; set; }
        public bool IsVisited { get; set; }
        public DateTime Date { get; set; } = DateTime.UtcNow.Date;

        [ForeignKey(nameof(UserId))]
        public virtual User User { get; set; } = null!;
        [ForeignKey(nameof(ServiceJobId))]
        public virtual ServiceJob? ServiceJob { get; set; }
    }
}
