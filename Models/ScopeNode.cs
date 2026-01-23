using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json.Serialization;
using KamatekCrm.Enums;
using KamatekCrm.ViewModels;

namespace KamatekCrm.Models
{
    /// <summary>
    /// Proje kapsam ağacı node'u - Recursive, JSON-Serializable
    /// Site > Blok > Kat > Daire hiyerarşisi
    /// </summary>
    public class ScopeNode : ViewModelBase
    {
        private string _name = string.Empty;
        private bool _isExpanded = true;
        private bool _isSelected;

        #region Properties (JSON Serialized)

        /// <summary>
        /// Benzersiz tanımlayıcı
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8].ToUpper();

        /// <summary>
        /// Görüntüleme adı
        /// </summary>
        public string Name
        {
            get => _name;
            set
            {
                if (SetProperty(ref _name, value))
                {
                    OnPropertyChanged(nameof(HeaderDisplay));
                    OnPropertyChanged(nameof(DisplayText));
                }
            }
        }

        /// <summary>
        /// Node tipi (Project, Block, Floor, Flat, Zone, vb.)
        /// </summary>
        public NodeType Type { get; set; }

        /// <summary>
        /// Sıralama numarası
        /// </summary>
        public int Order { get; set; }

        /// <summary>
        /// Alt node'lar
        /// </summary>
        public ObservableCollection<ScopeNode> Children { get; set; } = new();

        /// <summary>
        /// Bu node'a atanmış kalemler (Malzeme/Hizmet)
        /// </summary>
        public List<ScopeNodeItem> Items { get; set; } = new();

        #endregion

        #region UI Properties (Not Serialized)

        /// <summary>
        /// Üst node referansı (Runtime'da set edilir)
        /// </summary>
        [JsonIgnore]
        public ScopeNode? Parent { get; set; }

        /// <summary>
        /// TreeView genişletilmiş mi?
        /// </summary>
        [JsonIgnore]
        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }

        /// <summary>
        /// TreeView seçili mi?
        /// </summary>
        [JsonIgnore]
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        #endregion

        #region Calculated Properties

        /// <summary>
        /// Node ikonu (Tip bazlı)
        /// </summary>
        [JsonIgnore]
        public string Icon => Type switch
        {
            NodeType.Project => "📁",
            NodeType.Block => "🏢",
            NodeType.Floor => "📋",
            NodeType.Flat => "🏠",
            NodeType.Zone => "🏭",
            NodeType.Entrance => "🚪",
            NodeType.Garden => "🌳",
            NodeType.Parking => "🅿️",
            NodeType.CommonArea => "🔷",
            _ => "📌"
        };

        /// <summary>
        /// Görüntüleme metni (Icon + Name)
        /// </summary>
        [JsonIgnore]
        public string DisplayText => $"{Icon} {Name}";

        /// <summary>
        /// Bu node'un alt toplamı (sadece kendi Items)
        /// </summary>
        [JsonIgnore]
        public decimal SubTotal => Items.Sum(i => i.TotalPrice);

        /// <summary>
        /// Bu node'un maliyet toplamı (sadece kendi Items)
        /// </summary>
        [JsonIgnore]
        public decimal SubTotalCost => Items.Sum(i => i.TotalCost);

        /// <summary>
        /// Recursive toplam (kendisi + tüm alt node'lar)
        /// </summary>
        [JsonIgnore]
        public decimal RecursiveTotal
        {
            get
            {
                decimal total = SubTotal;
                foreach (var child in Children)
                {
                    total += child.RecursiveTotal;
                }
                return total;
            }
        }

        /// <summary>
        /// Recursive maliyet toplamı
        /// </summary>
        [JsonIgnore]
        public decimal RecursiveTotalCost
        {
            get
            {
                decimal total = SubTotalCost;
                foreach (var child in Children)
                {
                    total += child.RecursiveTotalCost;
                }
                return total;
            }
        }

        /// <summary>
        /// Recursive kar
        /// </summary>
        [JsonIgnore]
        public decimal RecursiveProfit => RecursiveTotal - RecursiveTotalCost;

        /// <summary>
        /// Recursive kalem sayısı
        /// </summary>
        [JsonIgnore]
        public int RecursiveItemCount
        {
            get
            {
                int count = Items.Count;
                foreach (var child in Children)
                {
                    count += child.RecursiveItemCount;
                }
                return count;
            }
        }

        /// <summary>
        /// TreeView header'da gösterilecek maliyet badge
        /// </summary>
        [JsonIgnore]
        public string CostBadge => RecursiveTotal > 0 ? $"₺{RecursiveTotal:N0}" : "";

        /// <summary>
        /// Header görüntüleme (Icon + Name + Badge)
        /// </summary>
        [JsonIgnore]
        public string HeaderDisplay => RecursiveTotal > 0
            ? $"{Icon} {Name} - {CostBadge}"
            : $"{Icon} {Name}";

        #endregion

        #region Methods

        /// <summary>
        /// Alt node ekle
        /// </summary>
        public ScopeNode AddChild(string name, NodeType type)
        {
            var child = new ScopeNode
            {
                Name = name,
                Type = type,
                Parent = this,
                Order = Children.Count
            };
            Children.Add(child);
            NotifyTotalsChanged();
            return child;
        }

        /// <summary>
        /// Kalem ekle
        /// </summary>
        public ScopeNodeItem AddItem(int productId, string productName, decimal unitCost, decimal unitPrice, int quantity = 1)
        {
            var item = new ScopeNodeItem
            {
                ProductId = productId,
                ProductName = productName,
                UnitCost = unitCost,
                UnitPrice = unitPrice,
                Quantity = quantity
            };
            Items.Add(item);
            NotifyTotalsChanged();
            return item;
        }

        /// <summary>
        /// Kalem sil
        /// </summary>
        public void RemoveItem(ScopeNodeItem item)
        {
            Items.Remove(item);
            NotifyTotalsChanged();
        }

        /// <summary>
        /// Bu node ve tüm üst node'ları güncelle
        /// </summary>
        public void NotifyTotalsChanged()
        {
            OnPropertyChanged(nameof(SubTotal));
            OnPropertyChanged(nameof(SubTotalCost));
            OnPropertyChanged(nameof(RecursiveTotal));
            OnPropertyChanged(nameof(RecursiveTotalCost));
            OnPropertyChanged(nameof(RecursiveProfit));
            OnPropertyChanged(nameof(CostBadge));
            OnPropertyChanged(nameof(HeaderDisplay));
            Parent?.NotifyTotalsChanged();
        }

        /// <summary>
        /// Kalemleri başka bir node'a kopyala
        /// </summary>
        public void CopyItemsTo(ScopeNode target)
        {
            foreach (var item in Items)
            {
                target.AddItem(item.ProductId, item.ProductName, item.UnitCost, item.UnitPrice, item.Quantity);
            }
        }

        /// <summary>
        /// Node'u klonla (alt node'lar dahil)
        /// </summary>
        public ScopeNode Clone(string? newName = null)
        {
            var clone = new ScopeNode
            {
                Name = newName ?? this.Name,
                Type = this.Type,
                Order = this.Order
            };

            // Kalemleri kopyala
            foreach (var item in Items)
            {
                clone.Items.Add(item.Clone());
            }

            // Alt node'ları recursive kopyala
            foreach (var child in Children)
            {
                var childClone = child.Clone();
                childClone.Parent = clone;
                clone.Children.Add(childClone);
            }

            return clone;
        }

        /// <summary>
        /// Tüm alt node'ları düz liste olarak getir (recursive)
        /// </summary>
        public List<ScopeNode> GetAllDescendants()
        {
            var result = new List<ScopeNode>();
            foreach (var child in Children)
            {
                result.Add(child);
                result.AddRange(child.GetAllDescendants());
            }
            return result;
        }

        /// <summary>
        /// Belirli tipteki alt node'ları getir
        /// </summary>
        public List<ScopeNode> GetDescendantsByType(NodeType type)
        {
            return GetAllDescendants().Where(n => n.Type == type).ToList();
        }

        /// <summary>
        /// JSON'dan yüklendikten sonra parent referanslarını ayarla
        /// </summary>
        public void RestoreParentReferences()
        {
            foreach (var child in Children)
            {
                child.Parent = this;
                child.RestoreParentReferences();
            }
        }

        #endregion
    }
}
