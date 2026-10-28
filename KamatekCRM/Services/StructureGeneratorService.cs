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

        /// <summary>
        /// Mevcut ağacı bozmadan apartman yapısını birleştirir/genişletir (Non-destructive merge).
        /// Mevcut düğümler ve içindeki malzemeler korunur, yalnızca eksik düğümler eklenir.
        /// </summary>
        public ScopeNode MergeApartment(
            ScopeNode root,
            int floorCount,
            int unitsPerFloor,
            bool includeEntrance = true,
            bool includeGarden = false,
            bool includeParking = false)
        {
            if (includeEntrance && !root.Children.Any(c => c.Type == NodeType.Entrance))
            {
                root.AddChild("Bina Girişi", NodeType.Entrance);
            }

            int flatNumber = 1;
            for (int floor = 1; floor <= floorCount; floor++)
            {
                var floorName = $"{floor}. Kat";
                var floorNode = root.Children.FirstOrDefault(c => c.Type == NodeType.Floor && c.Name == floorName);
                if (floorNode == null)
                {
                    floorNode = root.AddChild(floorName, NodeType.Floor);
                }

                for (int unit = 1; unit <= unitsPerFloor; unit++)
                {
                    var flatName = $"Daire {flatNumber}";
                    var flatNode = floorNode.Children.FirstOrDefault(c => c.Type == NodeType.Flat && c.Name == flatName);
                    if (flatNode == null)
                    {
                        floorNode.AddChild(flatName, NodeType.Flat);
                    }
                    flatNumber++;
                }
            }

            if (includeGarden && !root.Children.Any(c => c.Type == NodeType.Garden))
            {
                root.AddChild("Bahçe", NodeType.Garden);
            }

            if (includeParking && !root.Children.Any(c => c.Type == NodeType.Parking))
            {
                root.AddChild("Otopark", NodeType.Parking);
            }

            root.RestoreParentReferences();
            root.NotifyTotalsChanged();
            return root;
        }

        /// <summary>
        /// Mevcut ağacı bozmadan site yapısını birleştirir/genişletir (Non-destructive merge).
        /// </summary>
        public ScopeNode MergeSite(
            ScopeNode root,
            List<string> blockNames,
            int floorsPerBlock,
            int unitsPerFloor,
            bool includeSiteGarden = false,
            bool includeSiteParking = false)
        {
            foreach (var blockName in blockNames)
            {
                var blockNode = root.Children.FirstOrDefault(c => c.Type == NodeType.Block && c.Name == blockName);
                if (blockNode == null)
                {
                    blockNode = root.AddChild(blockName, NodeType.Block);
                }

                // Blok girişi
                var entranceName = $"{blockName} Girişi";
                if (!blockNode.Children.Any(c => c.Type == NodeType.Entrance && c.Name == entranceName))
                {
                    blockNode.AddChild(entranceName, NodeType.Entrance);
                }

                // Katlar
                int flatNumber = 1;
                for (int floor = 1; floor <= floorsPerBlock; floor++)
                {
                    var floorName = $"{floor}. Kat";
                    var floorNode = blockNode.Children.FirstOrDefault(c => c.Type == NodeType.Floor && c.Name == floorName);
                    if (floorNode == null)
                    {
                        floorNode = blockNode.AddChild(floorName, NodeType.Floor);
                    }

                    for (int unit = 1; unit <= unitsPerFloor; unit++)
                    {
                        var flatName = $"Daire {flatNumber}";
                        var flatNode = floorNode.Children.FirstOrDefault(c => c.Type == NodeType.Flat && c.Name == flatName);
                        if (flatNode == null)
                        {
                            floorNode.AddChild(flatName, NodeType.Flat);
                        }
                        flatNumber++;
                    }
                }
            }

            if (includeSiteGarden && !root.Children.Any(c => c.Type == NodeType.Garden))
            {
                root.AddChild("Site Bahçesi", NodeType.Garden);
            }

            if (includeSiteParking && !root.Children.Any(c => c.Type == NodeType.Parking))
            {
                root.AddChild("Site Otoparkı", NodeType.Parking);
            }

            root.RestoreParentReferences();
            root.NotifyTotalsChanged();
            return root;
        }
    }
}
