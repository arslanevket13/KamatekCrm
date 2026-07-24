using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using KamatekCrm.Shared.Models;

namespace KamatekCrm.Services
{
    /// <summary>
    /// Türkiye adres verilerini yöneten servis (In-Memory Cache destekli)
    /// </summary>
    public class AddressService
    {
        private static List<City>? _cities;
        private static readonly object _lock = new object();
        private static IMemoryCache? _cache;

        public AddressService(IMemoryCache? cache = null)
        {
            _cache ??= cache ?? (App.ServiceProvider?.GetService(typeof(IMemoryCache)) as IMemoryCache);
        }

        private class CityJsonItem
        {
            [JsonPropertyName("plaka")]
            public int Plaka { get; set; }

            [JsonPropertyName("ilceler")]
            public Dictionary<string, List<string>>? Ilceler { get; set; }
        }

        /// <summary>
        /// Tüm şehirleri alfabetik sıralı olarak getirir (Cached)
        /// </summary>
        public static List<City> GetCities()
        {
            EnsureCacheInitialized();

            if (_cache != null)
            {
                return _cache.GetOrCreate("Address_Cities", entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24);
                    EnsureDataLoaded();
                    return _cities!.OrderBy(c => c.Name).ToList();
                }) ?? new List<City>();
            }

            EnsureDataLoaded();
            return _cities!.OrderBy(c => c.Name).ToList();
        }

        /// <summary>
        /// Belirli bir şehre ait ilçeleri alfabetik sıralı olarak getirir (Cached)
        /// </summary>
        public static List<District> GetDistricts(string cityName)
        {
            if (string.IsNullOrWhiteSpace(cityName)) return new List<District>();
            EnsureCacheInitialized();

            string cacheKey = $"Address_Districts_{cityName.ToLowerInvariant()}";
            if (_cache != null)
            {
                return _cache.GetOrCreate(cacheKey, entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24);
                    EnsureDataLoaded();
                    var city = _cities!.FirstOrDefault(c => string.Equals(c.Name, cityName, StringComparison.OrdinalIgnoreCase));
                    return city?.Districts.OrderBy(d => d.Name).ToList() ?? new List<District>();
                }) ?? new List<District>();
            }

            EnsureDataLoaded();
            var targetCity = _cities!.FirstOrDefault(c => string.Equals(c.Name, cityName, StringComparison.OrdinalIgnoreCase));
            return targetCity?.Districts.OrderBy(d => d.Name).ToList() ?? new List<District>();
        }

        /// <summary>
        /// Belirli bir ilçeye ait mahalleleri alfabetik sıralı olarak getirir (Cached)
        /// </summary>
        public static List<Neighborhood> GetNeighborhoods(string cityName, string districtName)
        {
            if (string.IsNullOrWhiteSpace(cityName) || string.IsNullOrWhiteSpace(districtName)) return new List<Neighborhood>();
            EnsureCacheInitialized();

            string cacheKey = $"Address_Neighborhoods_{cityName.ToLowerInvariant()}_{districtName.ToLowerInvariant()}";
            if (_cache != null)
            {
                return _cache.GetOrCreate(cacheKey, entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24);
                    EnsureDataLoaded();
                    var city = _cities!.FirstOrDefault(c => string.Equals(c.Name, cityName, StringComparison.OrdinalIgnoreCase));
                    if (city == null) return new List<Neighborhood>();
                    var district = city.Districts.FirstOrDefault(d => string.Equals(d.Name, districtName, StringComparison.OrdinalIgnoreCase));
                    return district?.Neighborhoods.OrderBy(n => n.Name).ToList() ?? new List<Neighborhood>();
                }) ?? new List<Neighborhood>();
            }

            EnsureDataLoaded();
            var targetCity2 = _cities!.FirstOrDefault(c => string.Equals(c.Name, cityName, StringComparison.OrdinalIgnoreCase));
            if (targetCity2 == null) return new List<Neighborhood>();
            var targetDistrict2 = targetCity2.Districts.FirstOrDefault(d => string.Equals(d.Name, districtName, StringComparison.OrdinalIgnoreCase));
            return targetDistrict2?.Neighborhoods.OrderBy(n => n.Name).ToList() ?? new List<Neighborhood>();
        }

        private static void EnsureCacheInitialized()
        {
            if (_cache == null && App.ServiceProvider != null)
            {
                _cache = App.ServiceProvider.GetService(typeof(IMemoryCache)) as IMemoryCache;
            }
        }

        private static void EnsureDataLoaded()
        {
            if (_cities == null)
            {
                lock (_lock)
                {
                    if (_cities == null)
                    {
                        InitializeAddressData();
                    }
                }
            }
        }

        private static void InitializeAddressData()
        {
            try
            {
                string? filePath = FindJsonFilePath();
                if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
                {
                    string jsonContent = File.ReadAllText(filePath);
                    var jsonOptions = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };

                    var rawData = JsonSerializer.Deserialize<Dictionary<string, CityJsonItem>>(jsonContent, jsonOptions);
                    if (rawData != null && rawData.Count > 0)
                    {
                        var result = new List<City>();
                        int cityId = 1;
                        int districtId = 1;
                        int neighborhoodId = 1;
                        var trCulture = new CultureInfo("tr-TR");

                        foreach (var kvp in rawData)
                        {
                            string rawName = kvp.Key;
                            string formattedCityName = trCulture.TextInfo.ToTitleCase(rawName.ToLower(trCulture));
                            var cityDto = kvp.Value;

                            var city = new City
                            {
                                Id = cityDto.Plaka > 0 ? cityDto.Plaka : cityId++,
                                Name = formattedCityName,
                                Districts = new List<District>()
                            };

                            if (cityDto.Ilceler != null)
                            {
                                foreach (var distKvp in cityDto.Ilceler)
                                {
                                    string districtName = distKvp.Key;
                                    var district = new District
                                    {
                                        Id = districtId++,
                                        CityId = city.Id,
                                        Name = districtName,
                                        Neighborhoods = new List<Neighborhood>()
                                    };

                                    if (distKvp.Value != null)
                                    {
                                        foreach (var nName in distKvp.Value)
                                        {
                                            var neighborhood = new Neighborhood
                                            {
                                                Id = neighborhoodId++,
                                                DistrictId = district.Id,
                                                Name = nName
                                            };
                                            district.Neighborhoods.Add(neighborhood);
                                        }
                                    }

                                    city.Districts.Add(district);
                                }
                            }

                            result.Add(city);
                        }

                        _cities = result;
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AddressService] JSON parsing error: {ex.Message}");
            }

            _cities = GetFallbackData();
        }

        private static string? FindJsonFilePath()
        {
            string[] fileNames = new[] { "turkiye_ilce_mahalle.json", "turkiye-adres.json" };
            string[] candidateDirectories = new[]
            {
                AppDomain.CurrentDomain.BaseDirectory,
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources"),
                Directory.GetCurrentDirectory(),
                Path.Combine(Directory.GetCurrentDirectory(), "Data"),
                Path.Combine(Directory.GetCurrentDirectory(), "Resources")
            };

            foreach (var dir in candidateDirectories)
            {
                foreach (var fileName in fileNames)
                {
                    if (string.IsNullOrEmpty(dir)) continue;
                    string fullPath = Path.Combine(dir, fileName);
                    if (File.Exists(fullPath))
                    {
                        return fullPath;
                    }
                }
            }

            string? currentDir = AppDomain.CurrentDomain.BaseDirectory;
            while (!string.IsNullOrEmpty(currentDir))
            {
                foreach (var fileName in fileNames)
                {
                    string candidate = Path.Combine(currentDir, fileName);
                    if (File.Exists(candidate)) return candidate;
                }
                var parent = Directory.GetParent(currentDir);
                if (parent == null) break;
                currentDir = parent.FullName;
            }

            return null;
        }

        private static List<City> GetFallbackData()
        {
            return new List<City>
            {
                new City
                {
                    Id = 26,
                    Name = "Eskişehir",
                    Districts = new List<District>
                    {
                        new District
                        {
                            Id = 1, CityId = 26, Name = "Odunpazarı",
                            Neighborhoods = new List<Neighborhood>
                            {
                                new Neighborhood { Id = 1, DistrictId = 1, Name = "71 Evler" },
                                new Neighborhood { Id = 2, DistrictId = 1, Name = "Akarbaşı" }
                            }
                        },
                        new District
                        {
                            Id = 2, CityId = 26, Name = "Tepebaşı",
                            Neighborhoods = new List<Neighborhood>
                            {
                                new Neighborhood { Id = 3, DistrictId = 2, Name = "Hoşnudiye" },
                                new Neighborhood { Id = 4, DistrictId = 2, Name = "Batıkent" }
                            }
                        }
                    }
                }
            };
        }
    }
}
