using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;
using KamatekCrm.Shared.Services;

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
            return MergeApartmentStructure(project, projectName, blockCount, floorCount, flatsPerFloor);
        }

        /// <summary>
        /// Mevcut ağacı bozmadan apartman yapısını birleştirir/genişletir (Non-destructive merge).
        /// Önceden eklenmiş blok, kat, daire düğümleri ve içlerindeki malzemeler kesinlikle silinmez, korunur.
        /// </summary>
        public static ScopeNode MergeApartmentStructure(
            ScopeNode existingProject,
            string projectName,
            int blockCount,
            int floorCount,
            int flatsPerFloor)
        {
            if (existingProject == null)
            {
                existingProject = CreateEmptyProjectTree(projectName);
            }
            else if (!string.IsNullOrWhiteSpace(projectName))
            {
                existingProject.Name = projectName;
            }

            for (int b = 0; b < blockCount; b++)
            {
                var blockName = blockCount > 1 ? $"{(char)('A' + b)} Blok" : "Ana Bina";
                var block = existingProject.Children.FirstOrDefault(c => c.Type == NodeType.Block && c.Name == blockName);
                if (block == null)
                {
                    block = existingProject.AddChild(blockName, NodeType.Block);
                }

                // Giriş katı (0. kat)
                var entrance = block.Children.FirstOrDefault(c => c.Type == NodeType.Entrance);
                if (entrance == null)
                {
                    block.AddChild("Giriş Katı", NodeType.Entrance);
                }

                for (int f = 1; f <= floorCount; f++)
                {
                    var floorName = $"{f}. Kat";
                    var floor = block.Children.FirstOrDefault(c => c.Type == NodeType.Floor && c.Name == floorName);
                    if (floor == null)
                    {
                        floor = block.AddChild(floorName, NodeType.Floor);
                    }

                    for (int d = 1; d <= flatsPerFloor; d++)
                    {
                        var flatNumber = (f - 1) * flatsPerFloor + d;
                        var flatName = $"Daire {flatNumber}";
                        var flat = floor.Children.FirstOrDefault(c => c.Type == NodeType.Flat && c.Name == flatName);
                        if (flat == null)
                        {
                            floor.AddChild(flatName, NodeType.Flat);
                        }
                    }
                }
            }

            existingProject.RestoreParentReferences();
            existingProject.NotifyTotalsChanged();
            return existingProject;
        }

        /// <summary>
        /// Kaynak düğümdeki malzeme kalemlerini belirtilen hedef düğümlere toplu olarak kopyalar.
        /// </summary>
        public static int BroadcastItemsToTargets(ScopeNode sourceNode, IEnumerable<ScopeNode> targetNodes, bool clearTargetFirst = true)
        {
            if (sourceNode == null || targetNodes == null) return 0;

            int updatedCount = 0;
            foreach (var target in targetNodes)
            {
                if (target == sourceNode) continue;

                if (clearTargetFirst)
                {
                    target.Items.Clear();
                }

                sourceNode.CopyItemsTo(target);
                target.NotifyTotalsChanged();
                updatedCount++;
            }

            sourceNode.RestoreParentReferences();
            return updatedCount;
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

        /// <summary>
        /// Proje ağacındaki tüm opsiyonel olmayan kalemleri kümülatif çarpanlarla toplar,
        /// alt iskontoyu Largest Remainder ile satırlara dağıtır ve kuruş farkı dengeli genel toplamı hesaplar.
        /// </summary>
        public static MultiLineCalculationResult CalculateScopeTotals(
            IEnumerable<ScopeNode> rootNodes,
            decimal discountPercent = 0m,
            decimal vatRate = 20m)
        {
            var lines = new List<LineCalculationInput>();
            var stack = new Stack<(ScopeNode Node, int CumulativeMultiplier)>(
                rootNodes.Reverse().Select(r => (r, 1)));

            while (stack.Count > 0)
            {
                var (node, cumulativeMultiplier) = stack.Pop();
                int currentMultiplier = cumulativeMultiplier * Math.Max(1, node.Multiplier);

                foreach (var item in node.Items ?? Enumerable.Empty<ScopeNodeItem>())
                {
                    if (item.IsOptional) continue;
                    lines.Add(new LineCalculationInput(
                        item.Id,
                        item.ProductId,
                        item.ProductName,
                        item.Quantity * currentMultiplier,
                        item.UnitPrice,
                        0m,
                        vatRate));
                }
                foreach (var child in (node.Children ?? Enumerable.Empty<ScopeNode>()).Reverse())
                {
                    stack.Push((child, currentMultiplier));
                }
            }

            decimal rawSubTotal = FinancialCalculator.RoundMoney(lines.Sum(l => l.Quantity * l.UnitPrice));
            decimal overallDiscountAmount = FinancialCalculator.RoundMoney(rawSubTotal * discountPercent / 100m);

            return FinancialCalculator.CalculateMultiLine(
                lines,
                overallDiscountAmount,
                laborCost: 0m,
                shippingCost: 0m,
                applyDiscountBeforeTax: true);
        }

        /// <summary>
        /// Tüm yapı ağacındaki malzemeleri ürün bazında (ProductId / ProductName) birleştirip
        /// kümülatif çarpanlarla toplam miktar, maliyet ve satış tutarlarını hesaplar.
        /// İskonto ve KDV kuruş farklarını Largest Remainder Method ile dağıtarak genel toplama kuruşu kuruşuna eşitler.
        /// </summary>
        public static ConsolidatedBomResult GenerateConsolidatedBom(
            IEnumerable<ScopeNode> rootNodes,
            decimal discountPercent = 0m,
            decimal vatRate = 20m)
        {
            var result = new ConsolidatedBomResult();
            if (rootNodes == null) return result;

            // 1. Ağacı gez ve yaprak kalemleri topla (kümülatif çarpan ve mahal yolu ile)
            var rawItems = new List<(ScopeNodeItem Item, int Multiplier, string Location)>();
            var stack = new Stack<(ScopeNode Node, int CumulativeMultiplier, string Path)>();

            foreach (var root in rootNodes.Reverse())
            {
                stack.Push((root, 1, root.Name));
            }

            while (stack.Count > 0)
            {
                var (node, cumulativeMultiplier, path) = stack.Pop();
                int currentMultiplier = cumulativeMultiplier * Math.Max(1, node.Multiplier);

                foreach (var item in node.Items ?? Enumerable.Empty<ScopeNodeItem>())
                {
                    if (item.IsOptional) continue;
                    rawItems.Add((item, currentMultiplier, path));
                }

                foreach (var child in (node.Children ?? Enumerable.Empty<ScopeNode>()).Reverse())
                {
                    var childPath = string.IsNullOrWhiteSpace(path) ? child.Name : $"{path} > {child.Name}";
                    stack.Push((child, currentMultiplier, childPath));
                }
            }

            if (!rawItems.Any()) return result;

            // 2. Ürün bazında grupla
            var grouped = rawItems
                .GroupBy(x => new
                {
                    x.Item.ProductId,
                    NameKey = (x.Item.ProductName ?? x.Item.Name ?? "").Trim().ToLowerInvariant()
                })
                .ToList();

            var bomItems = new List<ConsolidatedBomItem>();
            foreach (var group in grouped)
            {
                var first = group.First().Item;
                int totalQty = group.Sum(x => x.Item.Quantity * x.Multiplier);
                decimal unitPrice = first.UnitPrice;
                decimal unitCost = first.UnitCost + first.LaborCost;
                decimal grossRevenue = FinancialCalculator.RoundMoney(unitPrice * totalQty);
                decimal totalCost = FinancialCalculator.RoundMoney(unitCost * totalQty);

                var locations = group
                    .Select(x => x.Location)
                    .Distinct()
                    .OrderBy(l => l)
                    .ToList();

                bomItems.Add(new ConsolidatedBomItem
                {
                    ProductId = first.ProductId,
                    ProductName = first.ProductName ?? first.Name,
                    TotalQuantity = totalQty,
                    UnitPrice = unitPrice,
                    UnitCost = unitCost,
                    GrossRevenue = grossRevenue,
                    TotalCost = totalCost,
                    Locations = locations
                });
            }

            // 3. İskonto ve KDV Kuruş Dengelemesi (Largest Remainder Method)
            decimal totalGross = FinancialCalculator.RoundMoney(bomItems.Sum(b => b.GrossRevenue));
            decimal totalDiscount = FinancialCalculator.RoundMoney(totalGross * discountPercent / 100m);
            decimal targetNet = FinancialCalculator.RoundMoney(totalGross - totalDiscount);
            decimal targetVat = FinancialCalculator.RoundMoney(targetNet * vatRate / 100m);

            // İskonto dağıtımı (Largest Remainder Method)
            var grossWeights = bomItems.Select(b => b.GrossRevenue).ToList();
            var distributedDiscounts = FinancialCalculator.DistributeLargestRemainder(totalDiscount, grossWeights);

            for (int i = 0; i < bomItems.Count; i++)
            {
                bomItems[i].AllocatedDiscount = distributedDiscounts[i];
                bomItems[i].NetRevenue = FinancialCalculator.RoundMoney(bomItems[i].GrossRevenue - bomItems[i].AllocatedDiscount);
            }

            // KDV dağıtımı (Largest Remainder Method)
            var netWeights = bomItems.Select(b => b.NetRevenue).ToList();
            var distributedVat = FinancialCalculator.DistributeLargestRemainder(targetVat, netWeights);

            for (int i = 0; i < bomItems.Count; i++)
            {
                bomItems[i].VatAmount = distributedVat[i];
                bomItems[i].GrandTotal = FinancialCalculator.RoundMoney(bomItems[i].NetRevenue + bomItems[i].VatAmount);
            }

            result.Items = bomItems;
            return result;
        }
    }
}
