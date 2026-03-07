using System.Collections.Generic;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;

namespace KamatekCrm.Services
{
    /// <summary>
    /// Yapı ağacı otomatik oluşturucu servisi
    /// (ScopeNode tabanlı — eski StructureTreeItem kaldırıldı)
    /// </summary>
    public class StructureGeneratorService
    {
        /// <summary>
        /// Tek birim projesi oluştur
        /// </summary>
        public ScopeNode GenerateSingleUnit(string projectName)
        {
            var root = new ScopeNode
            {
                Name = projectName,
                Type = NodeType.Project,
                IsExpanded = true
            };

            root.AddChild("Ana Birim", NodeType.Flat);
            return root;
        }

        /// <summary>
        /// Apartman yapısı oluştur
        /// </summary>
        public ScopeNode GenerateApartment(
            string projectName,
            int floorCount,
            int unitsPerFloor,
            bool includeEntrance = true,
            bool includeGarden = false,
            bool includeParking = false)
        {
            var root = new ScopeNode
            {
                Name = projectName,
                Type = NodeType.Project,
                IsExpanded = true
            };

            // Giriş
            if (includeEntrance)
            {
                root.AddChild("Bina Girişi", NodeType.Entrance);
            }

            // Katlar ve daireler
            int flatNumber = 1;
            for (int floor = 1; floor <= floorCount; floor++)
            {
                var floorNode = root.AddChild($"{floor}. Kat", NodeType.Floor);

                for (int unit = 1; unit <= unitsPerFloor; unit++)
                {
                    floorNode.AddChild($"Daire {flatNumber}", NodeType.Flat);
                    flatNumber++;
                }
            }

            // Ortak alanlar
            if (includeGarden)
            {
                root.AddChild("Bahçe", NodeType.Garden);
            }

            if (includeParking)
            {
                root.AddChild("Otopark", NodeType.Parking);
            }

            return root;
        }

        /// <summary>
        /// Site yapısı oluştur
        /// </summary>
        public ScopeNode GenerateSite(
            string projectName,
            List<string> blockNames,
            int floorsPerBlock,
            int unitsPerFloor,
            bool includeSiteGarden = false,
            bool includeSiteParking = false)
        {
            var root = new ScopeNode
            {
                Name = projectName,
                Type = NodeType.Project,
                IsExpanded = true
            };

            foreach (var blockName in blockNames)
            {
                var blockNode = root.AddChild(blockName, NodeType.Block);

                // Blok girişi
                blockNode.AddChild($"{blockName} Girişi", NodeType.Entrance);

                // Katlar
                int flatNumber = 1;
                for (int floor = 1; floor <= floorsPerBlock; floor++)
                {
                    var floorNode = blockNode.AddChild($"{floor}. Kat", NodeType.Floor);

                    for (int unit = 1; unit <= unitsPerFloor; unit++)
                    {
                        floorNode.AddChild($"Daire {flatNumber}", NodeType.Flat);
                        flatNumber++;
                    }
                }
            }

            // Site ortak alanları
            if (includeSiteGarden)
            {
                root.AddChild("Site Bahçesi", NodeType.Garden);
            }

            if (includeSiteParking)
            {
                root.AddChild("Site Otoparkı", NodeType.Parking);
            }

            return root;
        }

        /// <summary>
        /// Ticari/Fabrika yapısı oluştur
        /// </summary>
        public ScopeNode GenerateCommercial(
            string projectName,
            List<(string name, NodeType type)> zones)
        {
            var root = new ScopeNode
            {
                Name = projectName,
                Type = NodeType.Project,
                IsExpanded = true
            };

            foreach (var (name, type) in zones)
            {
                root.AddChild(name, type);
            }

            return root;
        }

        /// <summary>
        /// Önceden tanımlı fabrika bölgeleri
        /// </summary>
        public List<(string name, NodeType type)> GetDefaultFactoryZones()
        {
            return new List<(string, NodeType)>
            {
                ("Giriş/Lobi", NodeType.Entrance),
                ("Üretim Alanı", NodeType.Zone),
                ("Depo", NodeType.Zone),
                ("Ofis", NodeType.Zone),
                ("Yemekhane", NodeType.Zone),
                ("Güvenlik", NodeType.Zone),
                ("Otopark", NodeType.Parking)
            };
        }
    }
}
