using System;
using System.Collections.ObjectModel;
using System.Linq;
using KamatekCrm.Enums;
using KamatekCrm.ViewModels;

namespace KamatekCrm.Models
{
    /// <summary>
    /// Yapı ağacı node'u - Recursive tree structure
    /// Site > Blok > Kat > Daire hiyerarşisi
    /// </summary>
    public class StructureTreeItem : ViewModelBase
    {
        private string _name = string.Empty;
        private bool _isExpanded = true;
        private bool _isSelected;

        #region Properties

        /// <summary>
        /// Benzersiz ID
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8].ToUpper();

        /// <summary>
        /// Görüntüleme adı
        /// </summary>
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        /// <summary>
        /// Node tipi
        /// </summary>
        public NodeType Type { get; set; }

        /// <summary>
        /// Üst node referansı
        /// </summary>
        public StructureTreeItem? Parent { get; set; }

        /// <summary>
        /// Alt node'lar
        /// </summary>
        public ObservableCollection<StructureTreeItem> Children { get; } = new();

        /// <summary>
        /// Bu node'a atanmış ürünler (Mahal Listesi)
        /// </summary>
        public ObservableCollection<ScopeItem> ScopeItems { get; } = new();

        /// <summary>
        /// TreeView genişletilmiş mi
        /// </summary>
        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }

        /// <summary>
        /// TreeView seçili mi
        /// </summary>
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
        public string DisplayText => $"{Icon} {Name}";

        /// <summary>
        /// Bu node'un kendi alt toplamı (sadece ScopeItems)
        /// </summary>
        public decimal SubTotal => ScopeItems.Sum(s => s.TotalPrice);

        /// <summary>
        /// Recursive toplam (kendisi + tüm alt node'lar)
        /// </summary>
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
        /// Alt node sayısı (recursive)
        /// </summary>
        public int TotalChildCount
        {
            get
            {
                int count = Children.Count;
                foreach (var child in Children)
                {
                    count += child.TotalChildCount;
                }
                return count;
            }
        }

        /// <summary>
        /// Scope item sayısı (recursive)
        /// </summary>
        public int TotalScopeItemCount
        {
            get
            {
                int count = ScopeItems.Count;
                foreach (var child in Children)
                {
                    count += child.TotalScopeItemCount;
                }
                return count;
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Alt node ekle
        /// </summary>
        public StructureTreeItem AddChild(string name, NodeType type)
        {
            var child = new StructureTreeItem
            {
                Name = name,
                Type = type,
                Parent = this
            };
            Children.Add(child);
            return child;
        }

        /// <summary>
        /// Scope item ekle
        /// </summary>
        public ScopeItem AddScopeItem(int productId, string productName, decimal unitPrice, int quantity = 1)
        {
            var item = new ScopeItem
            {
                ProductId = productId,
                ProductName = productName,
                UnitPrice = unitPrice,
                Quantity = quantity,
                ParentNodeId = this.Id
            };
            ScopeItems.Add(item);
            NotifyTotalsChanged();
            return item;
        }

        /// <summary>
        /// Scope item sil
        /// </summary>
        public void RemoveScopeItem(ScopeItem item)
        {
            ScopeItems.Remove(item);
            NotifyTotalsChanged();
        }

        /// <summary>
        /// Bu node'u ve tüm üst node'ları güncelle
        /// </summary>
        public void NotifyTotalsChanged()
        {
            OnPropertyChanged(nameof(SubTotal));
            OnPropertyChanged(nameof(RecursiveTotal));
            Parent?.NotifyTotalsChanged();
        }

        /// <summary>
        /// Scope item'ları başka bir node'a kopyala
        /// </summary>
        public void CopyScopeItemsTo(StructureTreeItem target)
        {
            foreach (var item in ScopeItems)
            {
                target.AddScopeItem(item.ProductId, item.ProductName, item.UnitPrice, item.Quantity);
            }
        }

        /// <summary>
        /// Tüm alt node'ları düz liste olarak getir
        /// </summary>
        public System.Collections.Generic.List<StructureTreeItem> GetAllDescendants()
        {
            var result = new System.Collections.Generic.List<StructureTreeItem>();
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
        public System.Collections.Generic.List<StructureTreeItem> GetDescendantsByType(NodeType type)
        {
            return GetAllDescendants().Where(n => n.Type == type).ToList();
        }

        #endregion
    }
}
