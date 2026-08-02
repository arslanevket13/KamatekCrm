using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;

namespace KamatekCrm.Services
{
    /// <summary>
    /// Proje kapsam ağacı serileştirme/deserileştirme servisi
    /// </summary>
    public static class ProjectScopeService
    {
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            ReferenceHandler = ReferenceHandler.IgnoreCycles
        };

        /// <summary>
        /// ScopeNode ağacını JSON string'e dönüştür
        /// </summary>
        public static string Serialize(List<ScopeNode> rootNodes)
        {
            return JsonSerializer.Serialize(rootNodes, _jsonOptions);
        }

        /// <summary>
        /// JSON string'i ScopeNode ağacına dönüştür
        /// </summary>
        public static List<ScopeNode> Deserialize(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return new List<ScopeNode>();

            try
            {
                var nodes = JsonSerializer.Deserialize<List<ScopeNode>>(json, _jsonOptions);
                if (nodes != null)
                {
                    // Parent referanslarını restore et
                    foreach (var node in nodes)
                    {
                        node.RestoreParentReferences();
                    }
                    return nodes;
                }
            }
            catch (JsonException)
            {
                // JSON parse hatası
            }

            return new List<ScopeNode>();
        }

        /// <summary>
        /// Yeni boş proje ağacı oluştur
        /// </summary>
        public static ScopeNode CreateEmptyProjectTree(string projectName)
        {
            return new ScopeNode
            {
                Name = projectName,
                Type = NodeType.Project,
                IsExpanded = true
            };
        }

        /// <summary>
        /// Örnek yapı oluştur (Apartman)
        /// </summary>
        public static ScopeNode CreateSampleApartmentStructure(string projectName, int blockCount, int floorCount, int flatsPerFloor)
        {
            var project = CreateEmptyProjectTree(projectName);

            for (int b = 0; b < blockCount; b++)
            {
                var blockName = blockCount > 1 ? $"{(char)('A' + b)} Blok" : "Ana Bina";
                var block = project.AddChild(blockName, NodeType.Block);

                // Giriş katı (0. kat)
                var entrance = block.AddChild("Giriş Katı", NodeType.Entrance);

                for (int f = 1; f <= floorCount; f++)
                {
                    var floor = block.AddChild($"{f}. Kat", NodeType.Floor);

                    for (int d = 1; d <= flatsPerFloor; d++)
                    {
                        var flatNumber = (f - 1) * flatsPerFloor + d;
                        floor.AddChild($"Daire {flatNumber}", NodeType.Flat);
                    }
                }
            }

            return project;
        }

        /// <summary>
        /// Site yapısı oluştur
        /// </summary>
        public static ScopeNode CreateSiteStructure(string projectName, int blockCount, int floorCount, int flatsPerFloor)
        {
            var project = CreateSampleApartmentStructure(projectName, blockCount, floorCount, flatsPerFloor);

            // Ortak alanlar ekle
            var commonAreas = project.AddChild("Ortak Alanlar", NodeType.CommonArea);
            commonAreas.AddChild("Site Girişi", NodeType.Entrance);
            commonAreas.AddChild("Otopark", NodeType.Parking);
            commonAreas.AddChild("Bahçe", NodeType.Garden);

            return project;
        }
    }
}
