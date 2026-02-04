using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Windows.Input;
using KamatekCrm.Commands;
using KamatekCrm.Data;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace KamatekCrm.ViewModels
{
    public class RoutePlanningViewModel : ViewModelBase
    {
        private readonly AppDbContext _context;

        public RoutePlanningViewModel()
        {
            _context = new AppDbContext();
            // Initialize non-nullable fields
            _mapHtmlContent = string.Empty;
            LoadTodaysRoute();
        }

        #region Properties

        private string _mapHtmlContent;
        /// <summary>
        /// WebView2 için hazırlanmış HTML içeriği
        /// </summary>
        public string MapHtmlContent
        {
            get => _mapHtmlContent;
            set => SetProperty(ref _mapHtmlContent, value);
        }

        private DateTime _selectedDate = DateTime.Today;
        public DateTime SelectedDate
        {
            get => _selectedDate;
            set
            {
                if (SetProperty(ref _selectedDate, value))
                {
                    LoadTodaysRoute();
                }
            }
        }

        public ObservableCollection<MapMarkerItem> RoutePoints { get; set; } = new();

        #endregion

        private void LoadTodaysRoute()
        {
            // 1. Bugüne ait işleri çek
            var jobs = _context.ServiceJobs
                .Include(j => j.Customer) // Müşteri koordinatları için
                .Where(j => j.ScheduledDate.HasValue && j.ScheduledDate.Value.Date == SelectedDate.Date)
                .OrderBy(j => j.Priority) // Önce aciller
                .ToList();

            RoutePoints.Clear();

            // 2. Harita Noktalarına Dönüştür
            // Veri yoksa demo amaçlı Eskişehir lokasyonları ekleyelim
            if (!jobs.Any())
            {
                AddDemoData();
            }
            else
            {
                foreach (var job in jobs)
                {
                    // Koordinat yoksa rastgele bir Eskişehir konumu ata (Demo mod)
                    double lat = job.Customer?.Latitude ?? 39.7766 + (new Random().NextDouble() * 0.05 - 0.025);
                    double lng = job.Customer?.Longitude ?? 30.5206 + (new Random().NextDouble() * 0.05 - 0.025);

                    RoutePoints.Add(new MapMarkerItem
                    {
                        Title = $"{job.Customer?.FullName} ({job.JobCategory})",
                        Description = job.Description ?? "", // Handle possible null description
                        Lat = lat,
                        Lng = lng,
                        IsCompleted = job.Status == JobStatus.Completed
                    });
                }
            }

            // 3. HTML Oluştur
            GenerateHtml();
        }

        private void AddDemoData()
        {
            // Eskişehir Merkezli Demo Rota
            RoutePoints.Add(new MapMarkerItem { Title = "Ofis/Depo", Lat = 39.7766, Lng = 30.5206, Description = "Başlangıç Noktası" });
            RoutePoints.Add(new MapMarkerItem { Title = "Müşteri A (Arıza)", Lat = 39.7820, Lng = 30.5050, Description = "Kamera Sistemi Arızası" });
            RoutePoints.Add(new MapMarkerItem { Title = "Müşteri B (Montaj)", Lat = 39.7650, Lng = 30.5350, Description = "Yeni Alarm Kurulumu" });
            RoutePoints.Add(new MapMarkerItem { Title = "Müşteri C (Bakım)", Lat = 39.7900, Lng = 30.5100, Description = "Yıllık Bakım" });
        }

        private void GenerateHtml()
        {
            var markersJson = JsonSerializer.Serialize(RoutePoints);

            // Leaflet.js Harita Şablonu
            MapHtmlContent = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8' />
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <link rel='stylesheet' href='https://unpkg.com/leaflet@1.9.4/dist/leaflet.css' integrity='sha256-p4NxAoJBhIIN+hmNHrzRCf9tD/miZyoHS5obTRR9BMY=' crossorigin='' />
    <script src='https://unpkg.com/leaflet@1.9.4/dist/leaflet.js' integrity='sha256-20nQCchB9co0qIjJZRGuk2/Z9VM+kNiyxNV1lvTlZBo=' crossorigin=''></script>
    <style>
        body {{ margin: 0; padding: 0; }}
        #map {{ position: absolute; top: 0; bottom: 0; width: 100%; }}
    </style>
</head>
<body>
    <div id='map'></div>
    <script>
        var map = L.map('map').setView([39.7766, 30.5206], 13); // Eskişehir

        L.tileLayer('https://tile.openstreetmap.org/{{z}}/{{x}}/{{y}}.png', {{
            maxZoom: 19,
            attribution: '&copy; <a href=\'http://www.openstreetmap.org/copyright\'>OpenStreetMap</a>'
        }}).addTo(map);

        var markers = {markersJson};
        var latlngs = [];

        markers.forEach(function(m) {{
            var marker = L.marker([m.Lat, m.Lng]).addTo(map);
            marker.bindPopup('<b>' + m.Title + '</b><br>' + m.Description);
            latlngs.push([m.Lat, m.Lng]);
        }});

        if (latlngs.length > 1) {{
            var polyline = L.polyline(latlngs, {{color: 'blue', dashArray: '10, 10'}}).addTo(map);
            map.fitBounds(polyline.getBounds());
        }}
    </script>
</body>
</html>";
        }
    }

    public class MapMarkerItem
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public double Lat { get; set; }
        public double Lng { get; set; }
        public bool IsCompleted { get; set; }
    }
}
