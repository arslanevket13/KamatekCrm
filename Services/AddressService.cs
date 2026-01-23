using KamatekCrm.Models;

namespace KamatekCrm.Services
{
    /// <summary>
    /// Türkiye adres verilerini yöneten servis
    /// </summary>
    public class AddressService
    {
        private static List<City>? _cities;

        /// <summary>
        /// Tüm şehirleri alfabetik sıralı olarak getirir
        /// </summary>
        public static List<City> GetCities()
        {
            if (_cities == null)
            {
                InitializeAddressData();
            }
            return _cities!.OrderBy(c => c.Name).ToList();
        }

        /// <summary>
        /// Belirli bir şehre ait ilçeleri alfabetik sıralı olarak getirir
        /// </summary>
        public static List<District> GetDistricts(string cityName)
        {
            if (_cities == null)
            {
                InitializeAddressData();
            }

            var city = _cities!.FirstOrDefault(c => c.Name == cityName);
            if (city == null) return new List<District>();

            return city.Districts.OrderBy(d => d.Name).ToList();
        }

        /// <summary>
        /// Belirli bir ilçeye ait mahalleleri alfabetik sıralı olarak getirir
        /// </summary>
        public static List<Neighborhood> GetNeighborhoods(string cityName, string districtName)
        {
            if (_cities == null)
            {
                InitializeAddressData();
            }

            var city = _cities!.FirstOrDefault(c => c.Name == cityName);
            if (city == null) return new List<Neighborhood>();

            var district = city.Districts.FirstOrDefault(d => d.Name == districtName);
            if (district == null) return new List<Neighborhood>();

            return district.Neighborhoods.OrderBy(n => n.Name).ToList();
        }

        /// <summary>
        /// Adres verilerini başlatır (Örnek: Eskişehir)
        /// </summary>
        private static void InitializeAddressData()
        {
            _cities = new List<City>
            {
                new City
                {
                    Id = 1,
                    Name = "Eskişehir",
                    Districts = new List<District>
                    {
                        new District
                        {
                            Id = 1,
                            Name = "Odunpazarı",
                            CityId = 1,
                            Neighborhoods = new List<Neighborhood>
                            {
                                new Neighborhood { Id = 1, Name = "71 Evler", DistrictId = 1 },
                                new Neighborhood { Id = 2, Name = "Alanönü", DistrictId = 1 },
                                new Neighborhood { Id = 3, Name = "Arifiye", DistrictId = 1 },
                                new Neighborhood { Id = 4, Name = "Batıkent", DistrictId = 1 },
                                new Neighborhood { Id = 5, Name = "Büyükdere", DistrictId = 1 },
                                new Neighborhood { Id = 6, Name = "Çamlıca", DistrictId = 1 },
                                new Neighborhood { Id = 7, Name = "Emek", DistrictId = 1 },
                                new Neighborhood { Id = 8, Name = "Ertuğrulgazi", DistrictId = 1 },
                                new Neighborhood { Id = 9, Name = "Eskibağlar", DistrictId = 1 },
                                new Neighborhood { Id = 10, Name = "Göztepe", DistrictId = 1 },
                                new Neighborhood { Id = 11, Name = "Gültepe", DistrictId = 1 },
                                new Neighborhood { Id = 12, Name = "Hacıalim", DistrictId = 1 },
                                new Neighborhood { Id = 13, Name = "Karapınar", DistrictId = 1 },
                                new Neighborhood { Id = 14, Name = "Kurtuluş", DistrictId = 1 },
                                new Neighborhood { Id = 15, Name = "Mamure", DistrictId = 1 },
                                new Neighborhood { Id = 16, Name = "Osman Gazi", DistrictId = 1 },
                                new Neighborhood { Id = 17, Name = "Paşa", DistrictId = 1 },
                                new Neighborhood { Id = 18, Name = "Şarhöyük", DistrictId = 1 },
                                new Neighborhood { Id = 19, Name = "Şeker", DistrictId = 1 },
                                new Neighborhood { Id = 20, Name = "Vişnelik", DistrictId = 1 },
                                new Neighborhood { Id = 21, Name = "Yenibağlar", DistrictId = 1 }
                            }
                        },
                        new District
                        {
                            Id = 2,
                            Name = "Tepebaşı",
                            CityId = 1,
                            Neighborhoods = new List<Neighborhood>
                            {
                                new Neighborhood { Id = 22, Name = "Atatürk", DistrictId = 2 },
                                new Neighborhood { Id = 23, Name = "Bahçelievler", DistrictId = 2 },
                                new Neighborhood { Id = 24, Name = "Çankaya", DistrictId = 2 },
                                new Neighborhood { Id = 25, Name = "Cumhuriye", DistrictId = 2 },
                                new Neighborhood { Id = 26, Name = "Erenköy", DistrictId = 2 },
                                new Neighborhood { Id = 27, Name = "Eskişehir Osmangazi Üniversitesi", DistrictId = 2 },
                                new Neighborhood { Id = 28, Name = "Fevziçakmak", DistrictId = 2 },
                                new Neighborhood { Id = 29, Name = "Gazipaşa", DistrictId = 2 },
                                new Neighborhood { Id = 30, Name = "Gülistan", DistrictId = 2 },
                                new Neighborhood { Id = 31, Name = "Hoşnudiye", DistrictId = 2 },
                                new Neighborhood { Id = 32, Name = "İhsaniye", DistrictId = 2 },
                                new Neighborhood { Id = 33, Name = "İstiklal", DistrictId = 2 },
                                new Neighborhood { Id = 34, Name = "Kızılay", DistrictId = 2 },
                                new Neighborhood { Id = 35, Name = "Osmangazi", DistrictId = 2 },
                                new Neighborhood { Id = 36, Name = "Şirintepe", DistrictId = 2 },
                                new Neighborhood { Id = 37, Name = "Yenidoğan", DistrictId = 2 },
                                new Neighborhood { Id = 38, Name = "Yıldıztepe", DistrictId = 2 }
                            }
                        },
                        new District
                        {
                            Id = 3,
                            Name = "Sivrihisar",
                            CityId = 1,
                            Neighborhoods = new List<Neighborhood>
                            {
                                new Neighborhood { Id = 39, Name = "Akarbaşı", DistrictId = 3 },
                                new Neighborhood { Id = 40, Name = "Akpınar", DistrictId = 3 },
                                new Neighborhood { Id = 41, Name = "Çukurhisar", DistrictId = 3 },
                                new Neighborhood { Id = 42, Name = "Merkez", DistrictId = 3 }
                            }
                        },
                        new District
                        {
                            Id = 4,
                            Name = "Seyitgazi",
                            CityId = 1,
                            Neighborhoods = new List<Neighborhood>
                            {
                                new Neighborhood { Id = 43, Name = "Çukurca", DistrictId = 4 },
                                new Neighborhood { Id = 44, Name = "Gümele", DistrictId = 4 },
                                new Neighborhood { Id = 45, Name = "Merkez", DistrictId = 4 },
                                new Neighborhood { Id = 46, Name = "Yazılıkaya", DistrictId = 4 }
                            }
                        }
                    }
                },
                // Diğer şehirler buraya eklenebilir
                new City
                {
                    Id = 2,
                    Name = "Ankara",
                    Districts = new List<District>
                    {
                        new District
                        {
                            Id = 5,
                            Name = "Çankaya",
                            CityId = 2,
                            Neighborhoods = new List<Neighborhood>
                            {
                                new Neighborhood { Id = 47, Name = "Kızılay", DistrictId = 5 },
                                new Neighborhood { Id = 48, Name = "Bahçelievler", DistrictId = 5 },
                                new Neighborhood { Id = 49, Name = "Çankaya", DistrictId = 5 }
                            }
                        },
                        new District
                        {
                            Id = 6,
                            Name = "Keçiören",
                            CityId = 2,
                            Neighborhoods = new List<Neighborhood>
                            {
                                new Neighborhood { Id = 50, Name = "Aktepe", DistrictId = 6 },
                                new Neighborhood { Id = 51, Name = "Bağlarbaşı", DistrictId = 6 },
                                new Neighborhood { Id = 52, Name = "Etlik", DistrictId = 6 }
                            }
                        }
                    }
                },
                new City
                {
                    Id = 3,
                    Name = "İstanbul",
                    Districts = new List<District>
                    {
                        new District
                        {
                            Id = 7,
                            Name = "Kadıköy",
                            CityId = 3,
                            Neighborhoods = new List<Neighborhood>
                            {
                                new Neighborhood { Id = 53, Name = "Moda", DistrictId = 7 },
                                new Neighborhood { Id = 54, Name = "Fenerbahçe", DistrictId = 7 },
                                new Neighborhood { Id = 55, Name = "Göztepe", DistrictId = 7 }
                            }
                        },
                        new District
                        {
                            Id = 8,
                            Name = "Beşiktaş",
                            CityId = 3,
                            Neighborhoods = new List<Neighborhood>
                            {
                                new Neighborhood { Id = 56, Name = "Levent", DistrictId = 8 },
                                new Neighborhood { Id = 57, Name = "Etiler", DistrictId = 8 },
                                new Neighborhood { Id = 58, Name = "Ortaköy", DistrictId = 8 }
                            }
                        }
                    }
                }
            };
        }
    }
}
