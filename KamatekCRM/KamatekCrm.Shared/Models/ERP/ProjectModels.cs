using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using KamatekCrm.Shared.Enums;

namespace KamatekCrm.Shared.Models
{
    public enum ProjectStatus { Active, Completed, Cancelled, Pending, Draft, PendingApproval }

    public class ServiceProject
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int? CustomerId { get; set; }
        public string ProjectCode { get; set; } = string.Empty;
        public string ProjectScopeJson { get; set; } = "[]";
        public decimal TotalBudget { get; set; }
        public decimal TotalCost { get; set; }
        public decimal TotalProfit { get; set; }
        public decimal DiscountPercent { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public PipelineStage PipelineStage { get; set; }
        public ProjectStatus Status { get; set; }
        public int TotalUnitCount { get; set; }
        public string SurveyNotes { get; set; } = "";
        public string QuoteItemsJson { get; set; } = "";
        [ForeignKey(nameof(CustomerId))]
        public virtual Customer? Customer { get; set; }
    }

    public class ScopeNode : INotifyPropertyChanged
    {
        private bool _isExpanded = true;
        private bool _isSelected;

        public int Id { get; set; }
        public string Name { get; set; } = "";
        public NodeType Type { get; set; }
        public decimal RecursiveTotal { get; set; }
        public decimal RecursiveTotalCost { get; set; }
        public decimal SubTotal { get; set; }

        public bool IsExpanded
        {
            get => _isExpanded;
            set { _isExpanded = value; OnPropertyChanged(nameof(IsExpanded)); }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(nameof(IsSelected)); }
        }

        public ScopeNode? Parent { get; set; }
        public virtual System.Collections.Generic.ICollection<ScopeNode> Children { get; set; } = new System.Collections.Generic.List<ScopeNode>();
        public virtual System.Collections.Generic.ICollection<ScopeNodeItem> Items { get; set; } = new System.Collections.Generic.List<ScopeNodeItem>();

        private static int _idCounter = 1;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void RestoreParentReferences()
        {
            foreach (var child in Children)
            {
                child.Parent = this;
                child.RestoreParentReferences();
            }
        }

        public void AddChild(ScopeNode child)
        {
            child.Parent = this;
            Children.Add(child);
            NotifyTotalsChanged();
        }

        public void AddChild(ScopeNodeItem item, int quantity)
        {
            item.Quantity = quantity;
            item.TotalPrice = item.UnitPrice * quantity;
            Items.Add(item);
            NotifyTotalsChanged();
        }

        public ScopeNode AddChild(string name, NodeType type)
        {
            var child = new ScopeNode
            {
                Id = _idCounter++,
                Name = name,
                Type = type,
                Parent = this,
                IsExpanded = true
            };
            Children.Add(child);
            OnPropertyChanged(nameof(Children));
            NotifyTotalsChanged();
            return child;
        }

        public void NotifyTotalsChanged()
        {
            SubTotal = Items.Sum(i => i.TotalPrice);
            RecursiveTotalCost = Items.Sum(i => i.TotalPrice) + Children.Sum(c => c.RecursiveTotalCost);
            RecursiveTotal = SubTotal + Children.Sum(c => c.RecursiveTotal);
            OnPropertyChanged(nameof(SubTotal));
            OnPropertyChanged(nameof(RecursiveTotal));
            OnPropertyChanged(nameof(RecursiveTotalCost));
            Parent?.NotifyTotalsChanged();
        }

        public void CopyItemsTo(ScopeNode other)
        {
            foreach (var item in Items)
            {
                var newItem = new ScopeNodeItem
                {
                    Id = _idCounter++,
                    Name = item.Name,
                    ProductName = item.ProductName,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    TotalPrice = item.TotalPrice
                };
                other.Items.Add(newItem);
            }
            other.NotifyTotalsChanged();
        }

        public ScopeNode Clone()
        {
            return Clone(Name + " (Kopya)");
        }

        public ScopeNode Clone(string newName)
        {
            var clone = new ScopeNode
            {
                Id = _idCounter++,
                Name = newName,
                Type = Type,
                IsExpanded = IsExpanded
            };

            foreach (var item in Items)
            {
                clone.Items.Add(new ScopeNodeItem
                {
                    Id = _idCounter++,
                    Name = item.Name,
                    ProductName = item.ProductName,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    TotalPrice = item.TotalPrice
                });
            }

            foreach (var child in Children)
            {
                var clonedChild = child.Clone();
                clonedChild.Parent = clone;
                clone.Children.Add(clonedChild);
            }

            clone.NotifyTotalsChanged();
            return clone;
        }

        public string HeaderDisplay => Type switch
        {
            NodeType.Project => $"📁 {Name}",
            NodeType.Block => $"🏢 {Name}",
            NodeType.Floor => $"🏠 {Name}",
            NodeType.Flat => $"🚪 {Name}",
            NodeType.Zone => $"📍 {Name}",
            _ => $"• {Name}"
        };
    }

    public class ScopeNodeItem : INotifyPropertyChanged
    {
        private static int _idCounter = 1;
        private int _quantity;
        private decimal _unitPrice;

        public int Id { get; set; }
        public string Name { get; set; } = "";

        public static ScopeNodeItem FromProduct(Product p)
        {
            return new ScopeNodeItem
            {
                Id = _idCounter++,
                Name = p.ProductName,
                ProductName = p.ProductName,
                Quantity = 1,
                UnitPrice = p.SalePrice,
                TotalPrice = p.SalePrice
            };
        }

        public string ProductName { get; set; } = "";

        public int Quantity
        {
            get => _quantity;
            set
            {
                _quantity = value;
                OnPropertyChanged(nameof(Quantity));
                NotifyChanged();
            }
        }

        public decimal UnitPrice
        {
            get => _unitPrice;
            set
            {
                _unitPrice = value;
                OnPropertyChanged(nameof(UnitPrice));
                NotifyChanged();
            }
        }

        public decimal TotalPrice { get; set; }

        public Action OnItemChanged { get; set; } = delegate { };

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void NotifyChanged()
        {
            TotalPrice = UnitPrice * Quantity;
            OnPropertyChanged(nameof(TotalPrice));
            OnItemChanged();
        }
    }

    public class StructureTreeItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public NodeType Type { get; set; }
        public void AddChild(StructureTreeItem item) { }
        public StructureTreeItem AddChild(string name, NodeType type) { return new StructureTreeItem(); }
    }
}
