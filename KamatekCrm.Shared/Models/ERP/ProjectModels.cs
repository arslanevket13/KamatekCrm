using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Services;

namespace KamatekCrm.Shared.Models
{
    public enum ProjectStatus { Active, Completed, Cancelled, Pending, Draft, PendingApproval }

    /// <summary>
    /// Teklif durumu yaşam döngüsü
    /// </summary>
    public enum QuoteStatus { Draft, Sent, Approved, Rejected, Expired, Revised }

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
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public PipelineStage PipelineStage { get; set; }
        public ProjectStatus Status { get; set; }
        public int TotalUnitCount { get; set; }
        public string SurveyNotes { get; set; } = "";
        public string QuoteItemsJson { get; set; } = "";

        // ── Teklif Yönetimi Alanları ──
        public string? QuoteNumber { get; set; }
        public QuoteStatus QuoteStatus { get; set; } = QuoteStatus.Draft;
        public int RevisionNumber { get; set; } = 1;
        public DateTime? SentDate { get; set; }
        public DateTime? ValidUntil { get; set; }
        public DateTime? ApprovedDate { get; set; }
        public DateTime? RejectedDate { get; set; }
        public string? RejectionReason { get; set; }
        public decimal KdvRate { get; set; } = 20;
        public string? Notes { get; set; }
        public string? PaymentTerms { get; set; }
        public string? RevisionsJson { get; set; }

        [ForeignKey(nameof(CustomerId))]
        public virtual Customer? Customer { get; set; }
    }

    /// <summary>
    /// Teklif revizyon geçmişi (JSON olarak ServiceProject.RevisionsJson'da saklanır)
    /// </summary>
    public class QuoteRevision
    {
        public int RevisionNumber { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public string ChangeDescription { get; set; } = "";
        public decimal TotalBudget { get; set; }
        public decimal DiscountPercent { get; set; }
        public string ScopeSnapshotJson { get; set; } = "";
    }

    public class ScopeNode : INotifyPropertyChanged
    {
        private bool _isExpanded = true;
        private bool _isSelected;
        private int _multiplier = 1;

        public int Id { get; set; }
        public string Name { get; set; } = "";
        public NodeType Type { get; set; }

        /// <summary>
        /// Düğüm çarpanı (ör. 10 adet tip daire, veya 3 blok). Varsayılan 1'dir.
        /// Altındaki tüm kalem ve düğümlerin toplamını çarpar.
        /// </summary>
        public int Multiplier
        {
            get => _multiplier;
            set
            {
                _multiplier = Math.Max(1, value);
                OnPropertyChanged(nameof(Multiplier));
                NotifyTotalsChanged();
            }
        }

        public decimal RecursiveTotal { get; set; }
        public decimal RecursiveTotalCost { get; set; }
        public decimal SubTotal { get; set; }
        public decimal SubTotalCost { get; set; }

        public decimal NodeSubTotal => RecursiveTotal;
        public decimal NodeCost => RecursiveTotalCost;
        public decimal NodeProfit => FinancialCalculator.RoundMoney(NodeSubTotal - NodeCost);
        public string NodeMarginDisplay => NodeSubTotal > 0
            ? $"%{FinancialCalculator.RoundMoney(NodeProfit / NodeSubTotal * 100m):N1}"
            : "%0";

        [System.Text.Json.Serialization.JsonIgnore]
        public static bool IsBatchUpdating { get; set; }

        public static IDisposable BeginBatchUpdate() => new BatchUpdateScope();

        private sealed class BatchUpdateScope : IDisposable
        {
            public BatchUpdateScope() => IsBatchUpdating = true;
            public void Dispose() => IsBatchUpdating = false;
        }

        public static void RecalculateTreeTotals(ScopeNode root)
        {
            if (root == null) return;
            foreach (var child in root.Children)
            {
                RecalculateTreeTotals(child);
            }
            root.NotifyTotalsChanged();
        }

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

        [System.Text.Json.Serialization.JsonIgnore]
        public ScopeNode? Parent { get; set; }
        public virtual ObservableCollection<ScopeNode> Children { get; set; } = new ObservableCollection<ScopeNode>();
        public virtual ObservableCollection<ScopeNodeItem> Items { get; set; } = new ObservableCollection<ScopeNodeItem>();

        private static int _idCounter = 1;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void ExpandParents()
        {
            IsExpanded = true;
            Parent?.ExpandParents();
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
            if (IsBatchUpdating) return;

            SubTotal = FinancialCalculator.RoundMoney(Items.Where(i => !i.IsOptional).Sum(i => i.TotalPrice));
            SubTotalCost = FinancialCalculator.RoundMoney(Items.Where(i => !i.IsOptional).Sum(i => i.TotalItemCost));
            decimal childrenTotal = FinancialCalculator.RoundMoney(Children.Sum(c => c.RecursiveTotal));
            decimal childrenCost = FinancialCalculator.RoundMoney(Children.Sum(c => c.RecursiveTotalCost));

            // Multiplier ile özyinelemeli çarpanlı toplam (ToEven kesinliğinde)
            RecursiveTotal = FinancialCalculator.RoundMoney((SubTotal + childrenTotal) * Multiplier);
            RecursiveTotalCost = FinancialCalculator.RoundMoney((SubTotalCost + childrenCost) * Multiplier);

            OnPropertyChanged(nameof(SubTotal));
            OnPropertyChanged(nameof(SubTotalCost));
            OnPropertyChanged(nameof(RecursiveTotal));
            OnPropertyChanged(nameof(RecursiveTotalCost));
            OnPropertyChanged(nameof(NodeSubTotal));
            OnPropertyChanged(nameof(NodeCost));
            OnPropertyChanged(nameof(NodeProfit));
            OnPropertyChanged(nameof(NodeMarginDisplay));
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
                    ProductId = item.ProductId,
                    ImagePath = item.ImagePath,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    UnitCost = item.UnitCost,
                    LaborCost = item.LaborCost,
                    IsOptional = item.IsOptional,
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
                Multiplier = Multiplier,
                IsExpanded = IsExpanded
            };

            foreach (var item in Items)
            {
                clone.Items.Add(new ScopeNodeItem
                {
                    Id = _idCounter++,
                    Name = item.Name,
                    ProductName = item.ProductName,
                    ProductId = item.ProductId,
                    ImagePath = item.ImagePath,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    UnitCost = item.UnitCost,
                    LaborCost = item.LaborCost,
                    IsOptional = item.IsOptional,
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

        /// <summary>
        /// Kendisini ve tüm alt düğümlerini özyinelemeli olarak döndürür.
        /// </summary>
        public IEnumerable<ScopeNode> GetAllDescendantsOrSelf()
        {
            yield return this;
            foreach (var child in Children)
            {
                foreach (var desc in child.GetAllDescendantsOrSelf())
                {
                    yield return desc;
                }
            }
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
        private int _quantity = 1;
        private decimal _unitPrice;
        private decimal _unitCost;
        private decimal _laborCost;
        private bool _isOptional;

        public int Id { get; set; }
        public string Name { get; set; } = "";
        public int? ProductId { get; set; }
        public string? ImagePath { get; set; }

        public static ScopeNodeItem FromProduct(Product p)
        {
            return new ScopeNodeItem
            {
                Id = _idCounter++,
                Name = p.ProductName,
                ProductName = p.ProductName,
                ProductId = p.Id,
                ImagePath = p.ImagePath,
                Quantity = 1,
                UnitCost = p.PurchasePrice,
                LaborCost = 0,
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
                _quantity = Math.Max(1, value);
                OnPropertyChanged(nameof(Quantity));
                NotifyChanged();
            }
        }

        public decimal UnitCost
        {
            get => _unitCost;
            set
            {
                _unitCost = Math.Max(0, value);
                OnPropertyChanged(nameof(UnitCost));
                NotifyChanged();
            }
        }

        public decimal LaborCost
        {
            get => _laborCost;
            set
            {
                _laborCost = Math.Max(0, value);
                OnPropertyChanged(nameof(LaborCost));
                NotifyChanged();
            }
        }

        public decimal UnitPrice
        {
            get => _unitPrice;
            set
            {
                _unitPrice = Math.Max(0, value);
                OnPropertyChanged(nameof(UnitPrice));
                NotifyChanged();
            }
        }

        public bool IsOptional
        {
            get => _isOptional;
            set
            {
                _isOptional = value;
                OnPropertyChanged(nameof(IsOptional));
                NotifyChanged();
            }
        }

        public decimal TotalPrice { get; set; }
        public decimal TotalItemCost => FinancialCalculator.RoundMoney((UnitCost + LaborCost) * Quantity);
        public decimal ItemProfit => FinancialCalculator.RoundMoney(TotalPrice - TotalItemCost);
        public string MarginDisplay => TotalPrice > 0 ? $"%{FinancialCalculator.RoundMoney((TotalPrice - TotalItemCost) / TotalPrice * 100m):N1}" : "%0";

        [System.Text.Json.Serialization.JsonIgnore]
        public Action OnItemChanged { get; set; } = delegate { };

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void NotifyChanged()
        {
            TotalPrice = FinancialCalculator.RoundMoney(UnitPrice * Quantity);
            OnPropertyChanged(nameof(TotalPrice));
            OnPropertyChanged(nameof(TotalItemCost));
            OnPropertyChanged(nameof(ItemProfit));
            OnPropertyChanged(nameof(MarginDisplay));
            OnItemChanged();
        }
    }

    /// <summary>
    /// Proje yapı ağacından çıkarılan konsolide malzeme icmali (Consolidated BOM).
    /// Hiyerarşik ağaçtaki tüm tekrarlayan ürünleri tekilleştirip kümülatif çarpanlarla toplar.
    /// Kuruş farkları Largest Remainder Method ile genel toplama eşitlenir.
    /// </summary>
    public class ConsolidatedBomItem
    {
        public int? ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string? Sku { get; set; }
        public int TotalQuantity { get; set; }
        public decimal UnitCost { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalCost { get; set; }
        public decimal GrossRevenue { get; set; }
        public decimal AllocatedDiscount { get; set; }
        public decimal NetRevenue { get; set; }
        public decimal VatAmount { get; set; }
        public decimal GrandTotal { get; set; }
        public decimal Profit => FinancialCalculator.RoundMoney(NetRevenue - TotalCost);
        public string MarginDisplay => NetRevenue > 0
            ? $"%{FinancialCalculator.RoundMoney(Profit / NetRevenue * 100m):N1}"
            : "%0";
        public List<string> Locations { get; set; } = new();
        public string LocationsSummary => Locations.Count <= 3
            ? string.Join(", ", Locations)
            : $"{string.Join(", ", Locations.Take(3))} ve +{Locations.Count - 3} mahal";
    }

    /// <summary>
    /// Konsolide Malzeme İcmali toplam ve özet sonucu
    /// </summary>
    public class ConsolidatedBomResult
    {
        public List<ConsolidatedBomItem> Items { get; set; } = new();
        public decimal TotalGrossRevenue => FinancialCalculator.RoundMoney(Items.Sum(i => i.GrossRevenue));
        public decimal TotalCost => FinancialCalculator.RoundMoney(Items.Sum(i => i.TotalCost));
        public decimal TotalDiscount => FinancialCalculator.RoundMoney(Items.Sum(i => i.AllocatedDiscount));
        public decimal TotalNetRevenue => FinancialCalculator.RoundMoney(Items.Sum(i => i.NetRevenue));
        public decimal TotalVat => FinancialCalculator.RoundMoney(Items.Sum(i => i.VatAmount));
        public decimal GrandTotal => FinancialCalculator.RoundMoney(Items.Sum(i => i.GrandTotal));
        public decimal TotalProfit => FinancialCalculator.RoundMoney(TotalNetRevenue - TotalCost);
        public decimal OverallMargin => TotalNetRevenue > 0
            ? Math.Round(TotalProfit / TotalNetRevenue * 100m, 2, MidpointRounding.AwayFromZero)
            : 0m;
        public int TotalItemCount => Items.Sum(i => i.TotalQuantity);
    }
}
