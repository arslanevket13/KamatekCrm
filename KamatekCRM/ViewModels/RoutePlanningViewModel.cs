using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using KamatekCrm.Infrastructure.Data;
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
    public partial class RoutePlanningViewModel : ViewModelBase
    {
        private readonly AppDbContext _context;
        private readonly KamatekCrm.Services.IToastService? _toastService;

        #region Constructor

        public RoutePlanningViewModel(
            AppDbContext context,
            KamatekCrm.Services.IToastService? toastService = null)
        {
            _context = context;
            _toastService = toastService;

            _mapHtmlContent = string.Empty;
            Technicians = new ObservableCollection<User>();
            AvailableJobs = new ObservableCollection<RouteJobItem>();
            RoutePoints = new ObservableCollection<RoutePointItem>();

            // Commands

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
                    Refresh();
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
                    Refresh();
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

        [RelayCommand]
        private void Refresh()
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
                        ScheduledDate = job.ScheduledDate,
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
                        Description = pt.ServiceJob?.Description ?? "",
                        Priority = pt.ServiceJob?.Priority ?? JobPriority.Normal,
                        ScheduledDate = pt.ServiceJob?.ScheduledDate
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

        [RelayCommand]
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
                Priority = job.Priority,
                ScheduledDate = job.ScheduledDate,
                IsVisited = false
            };

            RoutePoints.Add(newPoint);
            AvailableJobs.Remove(job);

            RecalculateStats();
            GenerateMapHtml();
        }

        [RelayCommand]
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

        [RelayCommand]
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

        [RelayCommand]
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

        [RelayCommand]
        private void OptimizeRoute()
        {
            if (RoutePoints.Count < 2) return;

            // Ziyaret edilmiş noktaların sırası korunur
            var visited = RoutePoints.Where(p => p.IsVisited).OrderBy(p => p.OrderIndex).ToList();
            var unvisited = RoutePoints.Where(p => !p.IsVisited).ToList();

            if (unvisited.Count == 0) return;

            // Deterministik çok kriterli sıralama:
            // 1. Aciliyet Kriteri (Critical / Urgent öncelikli)
            // 2. Randevu Saati (Belirli randevular saat sırasıyla)
            // 3. Konum / Coğrafi Mesafe Yakınlığı (Nearest-Neighbor)
            var optimizedUnvisited = new List<RoutePointItem>();
            var referencePoint = visited.LastOrDefault() ?? unvisited.FirstOrDefault();
            var remaining = new List<RoutePointItem>(unvisited);

            while (remaining.Count > 0)
            {
                var urgentPool = remaining.Where(p => p.Priority is JobPriority.Critical or JobPriority.Urgent).ToList();
                List<RoutePointItem> candidatePool;

                if (urgentPool.Count > 0)
                {
                    candidatePool = urgentPool;
                }
                else
                {
                    var timedPool = remaining.Where(p => p.ScheduledDate.HasValue).ToList();
                    candidatePool = timedPool.Count > 0 ? timedPool : remaining;
                }

                RoutePointItem nextPoint;
                if (candidatePool.Any(p => p.ScheduledDate.HasValue))
                {
                    nextPoint = candidatePool
                        .OrderBy(p => p.ScheduledDate.HasValue ? p.ScheduledDate.Value : DateTime.MaxValue)
                        .ThenBy(p => referencePoint != null ? HaversineDistance(referencePoint.Latitude, referencePoint.Longitude, p.Latitude, p.Longitude) : 0)
                        .First();
                }
                else
                {
                    nextPoint = candidatePool
                        .OrderBy(p => referencePoint != null ? HaversineDistance(referencePoint.Latitude, referencePoint.Longitude, p.Latitude, p.Longitude) : 0)
                        .ThenBy(p => p.Priority)
                        .First();
                }

                optimizedUnvisited.Add(nextPoint);
                remaining.Remove(nextPoint);
                referencePoint = nextPoint;
            }

            RoutePoints.Clear();
            int order = 1;
            foreach (var pt in visited.Concat(optimizedUnvisited))
            {
                pt.OrderIndex = order++;
                RoutePoints.Add(pt);
            }

            RecalculateStats();
            GenerateMapHtml();
        }

        [RelayCommand]
        private void ClearRoute()
        {
            RoutePoints.Clear();
            RecalculateStats();
            GenerateMapHtml();
            LoadAvailableJobs();
        }

        [RelayCommand]
        private void MarkVisited(RoutePointItem? point)
        {
            if (point == null) return;
            point.IsVisited = !point.IsVisited;
            point.ActualArrival = point.IsVisited ? DateTime.UtcNow : null;
            NotifyStatsChanged();
            GenerateMapHtml();
        }

        [RelayCommand]
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

                string summaryMessage =
                    $"Rota kaydedildi: {SelectedTechnician.Ad} {SelectedTechnician.Soyad} ({SelectedDate:dd.MM.yyyy}) — {RoutePoints.Count} nokta, {TotalDistanceDisplay}";
                if (_toastService is not null)
                {
                    _toastService.ShowSuccess(summaryMessage);
                }

                // Reload to get DB IDs
                LoadExistingRoute();

                Log.Information("Rota kaydedildi: Teknisyen={TechId}, Tarih={Date}, Nokta={Count}",
                    SelectedTechnician.Id, SelectedDate.Date, RoutePoints.Count);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Rota kaydedilemedi");
                _toastService?.ShowError($"Rota kaydedilemedi: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        #endregion

        #region Calculations

        public int UnmappedPointsCount => RoutePoints.Count(p => !p.HasValidCoordinates);
        public bool HasUnmappedPoints => UnmappedPointsCount > 0;
        public string UnmappedPointsWarning => HasUnmappedPoints
            ? $"⚠️ {UnmappedPointsCount} noktada koordinat eksik (0,0 filtreli)"
            : string.Empty;

        private void ReindexRoutePoints()
        {
            int order = 1;
            foreach (var pt in RoutePoints)
                pt.OrderIndex = order++;
        }

        private void RecalculateStats()
        {
            // Toplam mesafe (yalnızca geçerli koordinatı olan ardışık noktalar)
            var validPoints = RoutePoints.Where(p => p.HasValidCoordinates).ToList();
            double dist = 0;
            for (int i = 1; i < validPoints.Count; i++)
            {
                dist += HaversineDistance(
                    validPoints[i - 1].Latitude, validPoints[i - 1].Longitude,
                    validPoints[i].Latitude, validPoints[i].Longitude);
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
            OnPropertyChanged(nameof(UnmappedPointsCount));
            OnPropertyChanged(nameof(HasUnmappedPoints));
            OnPropertyChanged(nameof(UnmappedPointsWarning));
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

            // (0,0) koordinatsız noktaları Gine Körfezi'ne basmamak için filtrele
            var validPoints = RoutePoints.Where(p => p.HasValidCoordinates).ToList();
            int unmappedCount = RoutePoints.Count - validPoints.Count;

            var allStopsJs = string.Join(",\n", RoutePoints.Select((p, i) =>
                $"{{lat:{p.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
                $"lng:{p.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
                $"hasCoords:{(p.HasValidCoordinates ? "true" : "false")}," +
                $"title:{EscapeJs(p.Title)}," +
                $"customer:{EscapeJs(p.CustomerName)}," +
                $"address:{EscapeJs(p.Address)}," +
                $"desc:{EscapeJs(p.Description)}," +
                $"order:{(p.OrderIndex > 0 ? p.OrderIndex : i + 1)}," +
                $"visited:{(p.IsVisited ? "true" : "false")}}}"
            ));

            double avgLat = validPoints.Count > 0 ? validPoints.Average(p => p.Latitude) : 39.7766;
            double avgLng = validPoints.Count > 0 ? validPoints.Average(p => p.Longitude) : 30.5206;
            var centerLat = avgLat.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var centerLng = avgLng.ToString(System.Globalization.CultureInfo.InvariantCulture);

            MapHtmlContent = $@"<!DOCTYPE html>
<html>
<head>
<meta charset='utf-8'/>
<meta name='viewport' content='width=device-width,initial-scale=1.0'>
<link rel='stylesheet' href='https://unpkg.com/leaflet@1.9.4/dist/leaflet.css'/>
<script src='https://unpkg.com/leaflet@1.9.4/dist/leaflet.js'></script>
<style>
body{{margin:0;padding:0;font-family:'Segoe UI',sans-serif;background:#f4f6f9;color:#1e293b;}}
#map{{position:absolute;top:0;bottom:0;width:100%;}}
.warn-banner{{position:absolute;top:12px;right:12px;z-index:1000;background:rgba(255,243,205,0.96);border:1px solid #f59e0b;color:#92400e;padding:8px 14px;border-radius:8px;font-size:12px;font-weight:600;box-shadow:0 2px 8px rgba(0,0,0,0.12);}}
.custom-marker{{background:none;border:none;}}
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
#offline-view{{display:none;position:absolute;inset:0;overflow:auto;padding:20px;box-sizing:border-box;background:#f8fafc;}}
.offline-header{{display:flex;justify-content:space-between;align-items:center;margin-bottom:14px;padding:12px 16px;background:#fff;border-radius:10px;border-left:4px solid #1976d2;box-shadow:0 1px 4px rgba(0,0,0,0.06);}}
.offline-svg-box{{background:#fff;border-radius:10px;padding:12px;margin-bottom:14px;box-shadow:0 1px 4px rgba(0,0,0,0.06);}}
.stop-grid{{display:grid;grid-template-columns:repeat(auto-fill,minmax(240px,1fr));gap:10px;}}
.stop-card{{background:#fff;border-radius:8px;padding:12px;border:1px solid #e2e8f0;display:flex;gap:10px;align-items:flex-start;}}
.stop-num{{width:28px;height:28px;border-radius:50%;color:#fff;font-weight:700;font-size:13px;display:flex;align-items:center;justify-content:center;flex-shrink:0;}}
</style>
</head>
<body>
<div id='map'></div>
{(unmappedCount > 0 ? $"<div class='warn-banner'>⚠️ {unmappedCount} durakta koordinat eksik olduğu için harita pinlerinden hariç tutuldu.</div>" : "")}
<div id='offline-view'></div>
<script>
var allStops=[{allStopsJs}];
var validMarkers=allStops.filter(function(m){{return m.hasCoords;}});

function renderOfflineFallback(reason){{
  document.getElementById('map').style.display='none';
  var container=document.getElementById('offline-view');
  container.style.display='block';
  var svgHtml='';
  if(validMarkers.length>0){{
    var minLat=Math.min.apply(null,validMarkers.map(function(m){{return m.lat;}}));
    var maxLat=Math.max.apply(null,validMarkers.map(function(m){{return m.lat;}}));
    var minLng=Math.min.apply(null,validMarkers.map(function(m){{return m.lng;}}));
    var maxLng=Math.max.apply(null,validMarkers.map(function(m){{return m.lng;}}));
    var dLat=Math.max(maxLat-minLat,0.005);
    var dLng=Math.max(maxLng-minLng,0.005);
    var pts=validMarkers.map(function(m){{
      var x=40+((m.lng-minLng)/dLng)*520;
      var y=190-((m.lat-minLat)/dLat)*150;
      return {{x:Math.round(x),y:Math.round(y),m:m}};
    }});
    var poly=pts.map(function(p){{return p.x+','+p.y;}}).join(' ');
    var circles=pts.map(function(p){{
      var c=p.m.visited?'#2e7d32':'#d32f2f';
      return '<circle cx=""'+p.x+'"" cy=""'+p.y+'"" r=""14"" fill=""'+c+'"" stroke=""#fff"" stroke-width=""2""/>'
        +'<text x=""'+p.x+'"" y=""'+(p.y+4)+'"" text-anchor=""middle"" fill=""#fff"" font-size=""11"" font-weight=""bold"">'+p.m.order+'</text>';
    }}).join('');
    svgHtml='<div class=""offline-svg-box""><svg viewBox=""0 0 600 220"" width=""100%"" height=""200"">'
      +'<rect width=""600"" height=""220"" rx=""8"" fill=""#eef2ff""/>'
      +(pts.length>1?'<polyline points=""'+poly+'"" fill=""none"" stroke=""#1976d2"" stroke-width=""3"" stroke-dasharray=""8,6""/>':'')
      +circles+'</svg></div>';
  }}
  var cards=allStops.map(function(m){{
    var bg=m.visited?'#2e7d32':'#d32f2f';
    var coordNote=m.hasCoords?(m.lat.toFixed(4)+', '+m.lng.toFixed(4)):'⚠️ Koordinat kaydı yok';
    return '<div class=""stop-card""><div class=""stop-num"" style=""background:'+bg+'"">'+m.order+'</div>'
      +'<div><div style=""font-weight:700;font-size:13px;"">'+m.customer+'</div>'
      +'<div style=""font-size:12px;color:#475569;"">'+(m.address||m.title)+'</div>'
      +'<div style=""font-size:11px;color:#64748b;margin-top:4px;"">'+coordNote+'</div></div></div>';
  }}).join('');
  container.innerHTML='<div class=""offline-header""><div><b>🧭 Saha Rota Şeması</b><div style=""font-size:12px;color:#64748b;"">'+reason+'</div></div>'
    +'<div style=""font-size:12px;font-weight:600;color:#1976d2;"">Toplam '+allStops.length+' Durak</div></div>'
    +svgHtml+'<div class=""stop-grid"">'+cards+'</div>';
}}

if(typeof L==='undefined'){{
  renderOfflineFallback('Çevrimdışı mod — harita CDN bağlantısı olmadan vektörel rota şeması gösteriliyor.');
}}else if(validMarkers.length===0){{
  renderOfflineFallback('Rotadaki durakların hiçbirinde geçerli GPS koordinatı (Enlem/Boylam) kayıtlı değil.');
}}else{{
  var map=L.map('map').setView([{centerLat},{centerLng}],13);
  L.tileLayer('https://{{s}}.tile.openstreetmap.org/{{z}}/{{x}}/{{y}}.png',{{
    maxZoom:19,attribution:'&copy; OpenStreetMap contributors'
  }}).addTo(map);

  var latlngs=[];
  validMarkers.forEach(function(m){{
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
    map.fitBounds(polyline.getBounds().pad(0.15));
  }}else if(latlngs.length===1){{
    map.setView(latlngs[0],15);
  }}
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
body{margin:0;padding:0;font-family:'Segoe UI',sans-serif;background:#f1f5f9;}
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
if(typeof L!=='undefined'){
  var map=L.map('map').setView([39.7766,30.5206],12);
  L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png',{
    maxZoom:19,attribution:'&copy; OpenStreetMap'
  }).addTo(map);
}
</script>
</body>
</html>";
        }

        private static string EscapeJs(string? s)
        {
            if (string.IsNullOrEmpty(s)) return "\"\"";
            // XSS ve script injection engellemek için HTML-encode ardından JSON serialize
            var encoded = System.Net.WebUtility.HtmlEncode(s);
            return JsonSerializer.Serialize(encoded);
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
        public DateTime? ScheduledDate { get; set; }
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
    public partial class RoutePointItem : ViewModelBase
    {
        public int Id { get; set; }
        public int? ServiceJobId { get; set; }
        public int? CustomerId { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public bool HasValidCoordinates => Latitude != 0 && Longitude != 0;
        public string Address { get; set; } = "";
        public string Title { get; set; } = "";
        public string CustomerName { get; set; } = "";
        public string Description { get; set; } = "";
        public JobPriority Priority { get; set; } = JobPriority.Normal;
        public DateTime? ScheduledDate { get; set; }
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
        public System.Windows.Media.Brush StatusColor => KamatekCrm.Services.ThemeService.ResolveBrush(IsVisited ? "StatusSuccessFg" : "StatusWarningFg");
        public System.Windows.Media.Brush StatusBgColor => KamatekCrm.Services.ThemeService.ResolveBrush(IsVisited ? "StatusSuccessBg" : "StatusWarningBg");
    }

    #endregion
}

