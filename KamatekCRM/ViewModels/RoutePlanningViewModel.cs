using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using KamatekCrm.Commands;
using KamatekCrm.Data;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace KamatekCrm.ViewModels
{
    /// <summary>
    /// Profesyonel Rota Planlama ViewModel
    /// Teknisyen seçimi, iş atama, sıralama, optimizasyon, harita ve DB kaydı
    /// </summary>
    public class RoutePlanningViewModel : ViewModelBase
    {
        private readonly AppDbContext _context;

        #region Constructor

        public RoutePlanningViewModel(AppDbContext context)
        {
            _context = context;

            _mapHtmlContent = string.Empty;
            Technicians = new ObservableCollection<User>();
            AvailableJobs = new ObservableCollection<RouteJobItem>();
            RoutePoints = new ObservableCollection<RoutePointItem>();

            // Commands
            AddToRouteCommand = new RelayCommand<RouteJobItem>(AddToRoute);
            RemoveFromRouteCommand = new RelayCommand<RoutePointItem>(RemoveFromRoute);
            MoveUpCommand = new RelayCommand<RoutePointItem>(MoveUp, CanMoveUp);
            MoveDownCommand = new RelayCommand<RoutePointItem>(MoveDown, CanMoveDown);
            SaveRouteCommand = new RelayCommand(_ => SaveRoute(), _ => RoutePoints.Count > 0);
            OptimizeRouteCommand = new RelayCommand(_ => OptimizeRoute(), _ => RoutePoints.Count > 2);
            ClearRouteCommand = new RelayCommand(_ => ClearRoute(), _ => RoutePoints.Count > 0);
            MarkVisitedCommand = new RelayCommand<RoutePointItem>(MarkVisited);
            RefreshCommand = new RelayCommand(_ => LoadData());

            LoadTechnicians();
        }

        #endregion

        #region Properties

        private string _mapHtmlContent;
        public string MapHtmlContent
        {
            get => _mapHtmlContent;
            set => SetProperty(ref _mapHtmlContent, value);
        }

        private DateTime _selectedDate = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);
        public DateTime SelectedDate
        {
            get => _selectedDate;
            set
            {
                if (SetProperty(ref _selectedDate, value))
                    LoadData();
            }
        }

        private User? _selectedTechnician;
        public User? SelectedTechnician
        {
            get => _selectedTechnician;
            set
            {
                if (SetProperty(ref _selectedTechnician, value))
                {
                    OnPropertyChanged(nameof(IsTechnicianSelected));
                    LoadData();
                }
            }
        }

        public bool IsTechnicianSelected => SelectedTechnician != null;

        public ObservableCollection<User> Technicians { get; }
        public ObservableCollection<RouteJobItem> AvailableJobs { get; }
        public ObservableCollection<RoutePointItem> RoutePoints { get; }

        // İstatistikler
        public int TotalPoints => RoutePoints.Count;
        public int CompletedPoints => RoutePoints.Count(r => r.IsVisited);
        public string TotalDistanceDisplay => $"{TotalDistance:N1} km";
        public string EstimatedDurationDisplay => $"~{EstimatedDurationMinutes} dk";
        
        private double _totalDistance;
        public double TotalDistance
        {
            get => _totalDistance;
            private set => SetProperty(ref _totalDistance, value);
        }

        private int _estimatedDurationMinutes;
        public int EstimatedDurationMinutes
        {
            get => _estimatedDurationMinutes;
            private set => SetProperty(ref _estimatedDurationMinutes, value);
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        #endregion

        #region Commands

        public ICommand AddToRouteCommand { get; }
        public ICommand RemoveFromRouteCommand { get; }
        public ICommand MoveUpCommand { get; }
        public ICommand MoveDownCommand { get; }
        public ICommand SaveRouteCommand { get; }
        public ICommand OptimizeRouteCommand { get; }
        public ICommand ClearRouteCommand { get; }
        public ICommand MarkVisitedCommand { get; }
        public ICommand RefreshCommand { get; }

        #endregion

        #region Data Loading

        private void LoadTechnicians()
        {
            try
            {
                var techs = _context.Users
                    .Where(u => u.IsTechnician && u.IsActive && !u.IsDeleted)
                    .OrderBy(u => u.Ad)
                    .ToList();

                Technicians.Clear();
                foreach (var t in techs)
                    Technicians.Add(t);

                if (Technicians.Any())
                    SelectedTechnician = Technicians.First();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Teknisyen listesi yüklenemedi");
            }
        }

        private void LoadData()
        {
            if (SelectedTechnician == null) return;
            LoadAvailableJobs();
            LoadExistingRoute();
        }

        private void LoadAvailableJobs()
        {
            try
            {
                var jobs = _context.ServiceJobs
                    .Include(j => j.Customer)
                    .Where(j => !j.IsDeleted
                        && j.ScheduledDate.HasValue
                        && j.ScheduledDate.Value.Date == SelectedDate.Date
                        && j.Status != JobStatus.Completed
                        && j.Status != JobStatus.Cancelled)
                    .OrderBy(j => j.Priority)
                    .ThenBy(j => j.ScheduledDate)
                    .ToList();

                AvailableJobs.Clear();

                // Zaten rotada olan iş ID'leri
                var routeJobIds = RoutePoints
                    .Where(r => r.ServiceJobId.HasValue)
                    .Select(r => r.ServiceJobId!.Value)
                    .ToHashSet();

                foreach (var job in jobs)
                {
                    if (routeJobIds.Contains(job.Id)) continue;

                    double lat = job.Customer?.Latitude ?? 0;
                    double lng = job.Customer?.Longitude ?? 0;

                    AvailableJobs.Add(new RouteJobItem
                    {
                        ServiceJobId = job.Id,
                        Title = job.Title,
                        CustomerName = job.Customer?.FullName ?? "Bilinmiyor",
                        CustomerPhone = job.Customer?.PhoneNumber ?? "",
                        Address = $"{job.Customer?.Street}, {job.Customer?.District}/{job.Customer?.City}".Trim(' ', ',', '/'),
                        Description = job.Description ?? "",
                        Priority = job.Priority,
                        JobCategory = job.JobCategory,
                        Latitude = lat,
                        Longitude = lng,
                        HasCoordinates = lat != 0 && lng != 0,
                        PriorityDisplay = job.Priority switch
                        {
                            JobPriority.Critical => "🔴 KRİTİK",
                            JobPriority.Urgent => "🔴 ACİL",
                            JobPriority.High => "🟠 Yüksek",
                            JobPriority.Medium => "🟡 Orta",
                            JobPriority.Normal => "🟢 Normal",
                            JobPriority.Low => "⚪ Düşük",
                            _ => "⚪ Normal"
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Planlanmış işler yüklenemedi");
            }
        }

        private void LoadExistingRoute()
        {
            try
            {
                var points = _context.RoutePoints
                    .Include(r => r.ServiceJob)
                        .ThenInclude(j => j!.Customer)
                    .Where(r => r.UserId == SelectedTechnician!.Id && r.Date.Date == SelectedDate.Date)
                    .OrderBy(r => r.OrderIndex)
                    .ToList();

                RoutePoints.Clear();
                int order = 1;
                foreach (var pt in points)
                {
                    RoutePoints.Add(new RoutePointItem
                    {
                        Id = pt.Id,
                        OrderIndex = order++,
                        ServiceJobId = pt.ServiceJobId,
                        CustomerId = pt.CustomerId,
                        Latitude = pt.Latitude,
                        Longitude = pt.Longitude,
                        Address = pt.Address,
                        IsVisited = pt.IsVisited,
                        EstimatedArrival = pt.EstimatedArrival,
                        ActualArrival = pt.ActualArrival,
                        Title = pt.ServiceJob?.Title ?? pt.Address,
                        CustomerName = pt.ServiceJob?.Customer?.FullName ?? "",
                        Description = pt.ServiceJob?.Description ?? ""
                    });
                }

                RecalculateStats();
                GenerateMapHtml();

                // Rotada olan işleri available'dan çıkar
                LoadAvailableJobs();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Mevcut rota yüklenemedi");
            }
        }

        #endregion

        #region Route Operations

        private void AddToRoute(RouteJobItem? job)
        {
            if (job == null) return;

            var newPoint = new RoutePointItem
            {
                OrderIndex = RoutePoints.Count + 1,
                ServiceJobId = job.ServiceJobId,
                Latitude = job.Latitude,
                Longitude = job.Longitude,
                Address = job.Address,
                Title = job.Title,
                CustomerName = job.CustomerName,
                Description = job.Description,
                IsVisited = false
            };

            RoutePoints.Add(newPoint);
            AvailableJobs.Remove(job);

            RecalculateStats();
            GenerateMapHtml();
        }

        private void RemoveFromRoute(RoutePointItem? point)
        {
            if (point == null) return;

            RoutePoints.Remove(point);
            ReindexRoutePoints();
            RecalculateStats();
            GenerateMapHtml();

            // İşi tekrar available'a ekle
            LoadAvailableJobs();
        }

        private bool CanMoveUp(RoutePointItem? point) => point != null && RoutePoints.IndexOf(point) > 0;
        private bool CanMoveDown(RoutePointItem? point) => point != null && RoutePoints.IndexOf(point) < RoutePoints.Count - 1;

        private void MoveUp(RoutePointItem? point)
        {
            if (point == null) return;
            int idx = RoutePoints.IndexOf(point);
            if (idx <= 0) return;
            RoutePoints.Move(idx, idx - 1);
            ReindexRoutePoints();
            RecalculateStats();
            GenerateMapHtml();
        }

        private void MoveDown(RoutePointItem? point)
        {
            if (point == null) return;
            int idx = RoutePoints.IndexOf(point);
            if (idx >= RoutePoints.Count - 1) return;
            RoutePoints.Move(idx, idx + 1);
            ReindexRoutePoints();
            RecalculateStats();
            GenerateMapHtml();
        }

        private void OptimizeRoute()
        {
            if (RoutePoints.Count < 3) return;

            // Nearest-neighbor algoritması
            var points = RoutePoints.ToList();
            var optimized = new List<RoutePointItem>();
            var remaining = new List<RoutePointItem>(points);

            // İlk noktadan başla
            var current = remaining[0];
            optimized.Add(current);
            remaining.Remove(current);

            while (remaining.Count > 0)
            {
                var nearest = remaining
                    .OrderBy(p => HaversineDistance(current.Latitude, current.Longitude, p.Latitude, p.Longitude))
                    .First();

                optimized.Add(nearest);
                remaining.Remove(nearest);
                current = nearest;
            }

            RoutePoints.Clear();
            int order = 1;
            foreach (var pt in optimized)
            {
                pt.OrderIndex = order++;
                RoutePoints.Add(pt);
            }

            RecalculateStats();
            GenerateMapHtml();
        }

        private void ClearRoute()
        {
            RoutePoints.Clear();
            RecalculateStats();
            GenerateMapHtml();
            LoadAvailableJobs();
        }

        private void MarkVisited(RoutePointItem? point)
        {
            if (point == null) return;
            point.IsVisited = !point.IsVisited;
            point.ActualArrival = point.IsVisited ? DateTime.UtcNow : null;
            NotifyStatsChanged();
            GenerateMapHtml();
        }

        private void SaveRoute()
        {
            if (SelectedTechnician == null) return;

            try
            {
                IsBusy = true;

                // Mevcut rotayı sil
                var existing = _context.RoutePoints
                    .Where(r => r.UserId == SelectedTechnician.Id && r.Date.Date == SelectedDate.Date)
                    .ToList();
                _context.RoutePoints.RemoveRange(existing);

                // Yeni rotayı kaydet
                foreach (var pt in RoutePoints)
                {
                    _context.RoutePoints.Add(new RoutePoint
                    {
                        UserId = SelectedTechnician.Id,
                        ServiceJobId = pt.ServiceJobId,
                        CustomerId = pt.CustomerId,
                        Latitude = pt.Latitude,
                        Longitude = pt.Longitude,
                        Address = pt.Address,
                        OrderIndex = pt.OrderIndex,
                        EstimatedArrival = pt.EstimatedArrival,
                        ActualArrival = pt.ActualArrival,
                        IsVisited = pt.IsVisited,
                        Date = SelectedDate.Date.ToUniversalTime()
                    });
                }

                _context.SaveChanges();

                MessageBox.Show(
                    $"Rota başarıyla kaydedildi!\n\n" +
                    $"Teknisyen: {SelectedTechnician.Ad} {SelectedTechnician.Soyad}\n" +
                    $"Tarih: {SelectedDate:dd.MM.yyyy}\n" +
                    $"Toplam Nokta: {RoutePoints.Count}\n" +
                    $"Toplam Mesafe: {TotalDistanceDisplay}",
                    "Rota Kaydedildi",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                // Reload to get DB IDs
                LoadExistingRoute();

                Log.Information("Rota kaydedildi: Teknisyen={TechId}, Tarih={Date}, Nokta={Count}",
                    SelectedTechnician.Id, SelectedDate.Date, RoutePoints.Count);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Rota kaydedilemedi");
                MessageBox.Show($"Rota kaydedilemedi: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        #endregion

        #region Calculations

        private void ReindexRoutePoints()
        {
            int order = 1;
            foreach (var pt in RoutePoints)
                pt.OrderIndex = order++;
        }

        private void RecalculateStats()
        {
            // Toplam mesafe
            double dist = 0;
            for (int i = 1; i < RoutePoints.Count; i++)
            {
                dist += HaversineDistance(
                    RoutePoints[i - 1].Latitude, RoutePoints[i - 1].Longitude,
                    RoutePoints[i].Latitude, RoutePoints[i].Longitude);
            }
            TotalDistance = Math.Round(dist, 1);

            // Tahmini süre (ortalama 30 km/h şehir içi + 15 dk durak başı)
            EstimatedDurationMinutes = RoutePoints.Count > 0
                ? (int)(dist / 30.0 * 60) + (RoutePoints.Count * 15)
                : 0;

            NotifyStatsChanged();
        }

        private void NotifyStatsChanged()
        {
            OnPropertyChanged(nameof(TotalPoints));
            OnPropertyChanged(nameof(CompletedPoints));
            OnPropertyChanged(nameof(TotalDistanceDisplay));
            OnPropertyChanged(nameof(EstimatedDurationDisplay));
        }

        private static double HaversineDistance(double lat1, double lon1, double lat2, double lon2)
        {
            if (lat1 == 0 || lon1 == 0 || lat2 == 0 || lon2 == 0) return 0;
            const double R = 6371;
            var dLat = (lat2 - lat1) * Math.PI / 180;
            var dLon = (lon2 - lon1) * Math.PI / 180;
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        }

        #endregion

        #region Map Generation

        private void GenerateMapHtml()
        {
            if (RoutePoints.Count == 0)
            {
                MapHtmlContent = GenerateEmptyMapHtml();
                return;
            }

            var markersJs = string.Join(",\n", RoutePoints.Select((p, i) =>
                $"{{lat:{p.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
                $"lng:{p.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
                $"title:'{EscapeJs(p.Title)}'," +
                $"customer:'{EscapeJs(p.CustomerName)}'," +
                $"desc:'{EscapeJs(p.Description)}'," +
                $"order:{i + 1}," +
                $"visited:{(p.IsVisited ? "true" : "false")}}}"
            ));

            var centerLat = RoutePoints.Average(p => p.Latitude).ToString(System.Globalization.CultureInfo.InvariantCulture);
            var centerLng = RoutePoints.Average(p => p.Longitude).ToString(System.Globalization.CultureInfo.InvariantCulture);

            MapHtmlContent = $@"<!DOCTYPE html>
<html>
<head>
<meta charset='utf-8'/>
<meta name='viewport' content='width=device-width,initial-scale=1.0'>
<link rel='stylesheet' href='https://unpkg.com/leaflet@1.9.4/dist/leaflet.css'/>
<script src='https://unpkg.com/leaflet@1.9.4/dist/leaflet.js'></script>
<style>
body{{margin:0;padding:0;font-family:'Segoe UI',sans-serif;}}
#map{{position:absolute;top:0;bottom:0;width:100%;}}
.custom-marker{{
  background:none;border:none;
}}
.marker-pin{{
  width:32px;height:32px;border-radius:50%;display:flex;align-items:center;
  justify-content:center;color:#fff;font-weight:700;font-size:14px;
  box-shadow:0 2px 8px rgba(0,0,0,0.3);border:2px solid #fff;
}}
.marker-pending{{background:linear-gradient(135deg,#f44336,#e91e63);}}
.marker-visited{{background:linear-gradient(135deg,#4caf50,#2e7d32);}}
.popup-content{{min-width:200px;}}
.popup-content h3{{margin:0 0 5px;color:#1976d2;font-size:14px;}}
.popup-content p{{margin:2px 0;font-size:12px;color:#555;}}
.popup-content .badge{{display:inline-block;padding:2px 8px;border-radius:10px;font-size:10px;font-weight:600;color:#fff;}}
.badge-pending{{background:#f44336;}}
.badge-visited{{background:#4caf50;}}
</style>
</head>
<body>
<div id='map'></div>
<script>
var map=L.map('map').setView([{centerLat},{centerLng}],13);
L.tileLayer('https://{{s}}.tile.openstreetmap.org/{{z}}/{{x}}/{{y}}.png',{{
  maxZoom:19,attribution:'&copy; OpenStreetMap contributors'
}}).addTo(map);

var markers=[{markersJs}];
var latlngs=[];

markers.forEach(function(m){{
  var iconHtml='<div class=""marker-pin '+(m.visited?'marker-visited':'marker-pending')+'"">'
    +m.order+'</div>';
  var icon=L.divIcon({{className:'custom-marker',html:iconHtml,iconSize:[32,32],iconAnchor:[16,16]}});
  var marker=L.marker([m.lat,m.lng],{{icon:icon}}).addTo(map);
  var statusBadge=m.visited?'<span class=""badge badge-visited"">✓ Ziyaret Edildi</span>'
    :'<span class=""badge badge-pending"">Bekliyor</span>';
  marker.bindPopup('<div class=""popup-content""><h3>#'+m.order+' '+m.title+'</h3>'
    +'<p><b>Müşteri:</b> '+m.customer+'</p>'
    +'<p>'+m.desc+'</p>'+statusBadge+'</div>');
  latlngs.push([m.lat,m.lng]);
}});

if(latlngs.length>1){{
  var polyline=L.polyline(latlngs,{{
    color:'#1976d2',weight:4,opacity:0.8,
    dashArray:'12,8',lineCap:'round',lineJoin:'round'
  }}).addTo(map);

  // Animate arrows
  var decorator=null;
  map.fitBounds(polyline.getBounds().pad(0.15));
}}else if(latlngs.length===1){{
  map.setView(latlngs[0],15);
}}
</script>
</body>
</html>";
        }

        private string GenerateEmptyMapHtml()
        {
            return @"<!DOCTYPE html>
<html>
<head>
<meta charset='utf-8'/>
<link rel='stylesheet' href='https://unpkg.com/leaflet@1.9.4/dist/leaflet.css'/>
<script src='https://unpkg.com/leaflet@1.9.4/dist/leaflet.js'></script>
<style>
body{margin:0;padding:0;font-family:'Segoe UI',sans-serif;}
#map{position:absolute;top:0;bottom:0;width:100%;}
.empty-msg{position:absolute;top:50%;left:50%;transform:translate(-50%,-50%);
  z-index:999;background:rgba(255,255,255,0.95);padding:30px 50px;border-radius:12px;
  text-align:center;box-shadow:0 4px 20px rgba(0,0,0,0.15);}
.empty-msg h2{color:#1976d2;margin:0 0 8px;}
.empty-msg p{color:#666;margin:0;}
</style>
</head>
<body>
<div id='map'></div>
<div class='empty-msg'>
  <h2>📍 Rota Planı Boş</h2>
  <p>Sol panelden iş ekleyerek rota oluşturun</p>
</div>
<script>
var map=L.map('map').setView([39.7766,30.5206],12);
L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png',{
  maxZoom:19,attribution:'&copy; OpenStreetMap'
}).addTo(map);
</script>
</body>
</html>";
        }

        private static string EscapeJs(string? s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\n", " ").Replace("\r", "");
        }

        #endregion
    }

    #region Display Models

    /// <summary>
    /// Sol panelde gösterilen planlanmış iş kartı
    /// </summary>
    public class RouteJobItem
    {
        public int ServiceJobId { get; set; }
        public string Title { get; set; } = "";
        public string CustomerName { get; set; } = "";
        public string CustomerPhone { get; set; } = "";
        public string Address { get; set; } = "";
        public string Description { get; set; } = "";
        public JobPriority Priority { get; set; }
        public JobCategory JobCategory { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public bool HasCoordinates { get; set; }
        public string PriorityDisplay { get; set; } = "";

        public string CategoryDisplay => JobCategory switch
        {
            JobCategory.CCTV => "📷 CCTV",
            JobCategory.BurglarAlarm => "🚨 Hırsız Alarmı",
            JobCategory.FireAlarm => "🔥 Yangın Alarmı",
            JobCategory.AccessControl => "🚪 Geçiş",
            JobCategory.Network => "🌐 Ağ",
            JobCategory.VideoIntercom => "📞 İnterkom",
            JobCategory.SmartHome => "🏠 Akıllı Ev",
            JobCategory.Security => "🛡️ Güvenlik",
            _ => "🔧 Diğer"
        };
    }

    /// <summary>
    /// Rota üzerindeki nokta (sıralı, durum bilgili)
    /// </summary>
    public class RoutePointItem : ViewModelBase
    {
        public int Id { get; set; }
        public int? ServiceJobId { get; set; }
        public int? CustomerId { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string Address { get; set; } = "";
        public string Title { get; set; } = "";
        public string CustomerName { get; set; } = "";
        public string Description { get; set; } = "";
        public DateTime? EstimatedArrival { get; set; }
        public DateTime? ActualArrival { get; set; }

        private int _orderIndex;
        public int OrderIndex
        {
            get => _orderIndex;
            set => SetProperty(ref _orderIndex, value);
        }

        private bool _isVisited;
        public bool IsVisited
        {
            get => _isVisited;
            set
            {
                if (SetProperty(ref _isVisited, value))
                {
                    OnPropertyChanged(nameof(StatusDisplay));
                    OnPropertyChanged(nameof(StatusColor));
                    OnPropertyChanged(nameof(StatusBgColor));
                }
            }
        }

        public string StatusDisplay => IsVisited ? "✅ Ziyaret Edildi" : "⏳ Bekliyor";
        public string StatusColor => IsVisited ? "#4CAF50" : "#FF9800";
        public string StatusBgColor => IsVisited ? "#E8F5E9" : "#FFF3E0";
    }

    #endregion
}
