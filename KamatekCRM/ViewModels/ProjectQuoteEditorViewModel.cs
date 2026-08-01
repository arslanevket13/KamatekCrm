using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using GongSolutions.Wpf.DragDrop;
using CommunityToolkit.Mvvm.Input;
using KamatekCrm.Infrastructure.Data;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;
using KamatekCrm.Services;
using Microsoft.EntityFrameworkCore;

namespace KamatekCrm.ViewModels
{
    /// <summary>
    /// Proje Teklif Editörü ViewModel - Yenilenmiş 3 Panelli Workbench
    /// Drag & Drop, Tree Yönetimi, Finansal Hesaplamalar, Undo/Redo, Toplu Marj
    /// </summary>
    public partial class ProjectQuoteEditorViewModel : ViewModelBase, IDropTarget
    {
        private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
        private readonly ProjectScopeService _scopeService;

        // Undo / Redo Yığınları
        private readonly Stack<string> _undoStack = new();
        private readonly Stack<string> _redoStack = new();
        private bool _isApplyingUndoRedo;

        #region Properties - Proje Bilgileri

        private ServiceProject _currentProject = new();
        public ServiceProject CurrentProject
        {
            get => _currentProject;
            set => SetProperty(ref _currentProject, value);
        }

        private Customer? _selectedCustomer;
        public Customer? SelectedCustomer
        {
            get => _selectedCustomer;
            set
            {
                if (SetProperty(ref _selectedCustomer, value) && value != null)
                {
                    CurrentProject.CustomerId = value.Id;
                }
            }
        }

        public ObservableCollection<Customer> Customers { get; } = new();

        private string _projectName = string.Empty;
        public string ProjectName
        {
            get => _projectName;
            set
            {
                if (SetProperty(ref _projectName, value))
                {
                    CurrentProject.Title = value;
                }
            }
        }

        #endregion

        #region Properties - Yapı Oluşturucu

        private int _blockCount = 1;
        public int BlockCount
        {
            get => _blockCount;
            set => SetProperty(ref _blockCount, Math.Max(1, value));
        }

        private int _floorCount = 5;
        public int FloorCount
        {
            get => _floorCount;
            set => SetProperty(ref _floorCount, Math.Max(1, value));
        }

        private int _flatsPerFloor = 4;
        public int FlatsPerFloor
        {
            get => _flatsPerFloor;
            set => SetProperty(ref _flatsPerFloor, Math.Max(1, value));
        }

        #endregion

        #region Properties - Tree (Sol Panel) & Undo/Redo

        public ObservableCollection<ScopeNode> RootNodes { get; } = new();

        private string _treeSearchText = string.Empty;
        public string TreeSearchText
        {
            get => _treeSearchText;
            set
            {
                if (SetProperty(ref _treeSearchText, value))
                {
                    FilterTreeNodes();
                }
            }
        }

        private ScopeNode? _selectedNode;
        public ScopeNode? SelectedNode
        {
            get => _selectedNode;
            set
            {
                if (_selectedNode != value)
                {
                    if (_selectedNode != null)
                    {
                        _selectedNode.IsSelected = false;
                    }

                    if (SetProperty(ref _selectedNode, value))
                    {
                        if (_selectedNode != null)
                        {
                            _selectedNode.ExpandParents();
                            _selectedNode.IsSelected = true;
                        }

                        RefreshCurrentNodeItems();
                        OnPropertyChanged(nameof(SelectedNodeName));
                        OnPropertyChanged(nameof(SelectedNodeSubTotal));
                        OnPropertyChanged(nameof(HasSelectedNode));
                        OnPropertyChanged(nameof(CanAddFloor));
                        OnPropertyChanged(nameof(CanAddFlat));
                    }
                }
            }
        }

        public string SelectedNodeName => SelectedNode?.Name ?? "Seçili Node Yok";
        public decimal SelectedNodeSubTotal => SelectedNode?.SubTotal ?? 0;
        public bool HasSelectedNode => SelectedNode != null;
        public bool CanAddFloor => SelectedNode?.Type == NodeType.Block;
        public bool CanAddFlat => SelectedNode?.Type == NodeType.Floor || SelectedNode?.Type == NodeType.Flat;

        public bool CanUndo => _undoStack.Count > 0;
        public bool CanRedo => _redoStack.Count > 0;

        #endregion

        #region Properties - Mahal Listesi (Orta Panel)

        public ObservableCollection<ScopeNodeItem> CurrentNodeItems { get; } = new();

        private ScopeNodeItem? _selectedItem;
        public ScopeNodeItem? SelectedItem
        {
            get => _selectedItem;
            set => SetProperty(ref _selectedItem, value);
        }

        #endregion

        #region Properties - Ürün Kataloğu & Kategori (Sağ Panel)

        public ObservableCollection<Product> ProductCatalog { get; } = new();
        public ObservableCollection<Product> FilteredProducts { get; } = new();
        public ObservableCollection<string> Categories { get; } = new();

        private string _selectedCategory = "Tümü";
        public string SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                if (SetProperty(ref _selectedCategory, value))
                {
                    FilterProducts();
                }
            }
        }

        private string _productSearchText = string.Empty;
        public string ProductSearchText
        {
            get => _productSearchText;
            set
            {
                if (SetProperty(ref _productSearchText, value))
                {
                    FilterProducts();
                }
            }
        }

        private Product? _selectedProduct;
        public Product? SelectedProduct
        {
            get => _selectedProduct;
            set => SetProperty(ref _selectedProduct, value);
        }

        #endregion

        #region Properties - Finansal Özet

        public decimal TotalRevenue => RootNodes.Sum(n => n.RecursiveTotal);
        public decimal TotalCost => RootNodes.Sum(n => n.RecursiveTotalCost);

        private decimal _discountPercent;
        public decimal DiscountPercent
        {
            get => _discountPercent;
            set
            {
                if (SetProperty(ref _discountPercent, Math.Clamp(value, 0, 100)))
                {
                    CurrentProject.DiscountPercent = _discountPercent;
                    NotifyFinancialsChanged();
                }
            }
        }

        public decimal DiscountAmount => TotalRevenue * (DiscountPercent / 100);
        public decimal SubTotalAfterDiscount => TotalRevenue - DiscountAmount;

        private decimal _kdvRate = 20;
        public decimal KdvRate
        {
            get => _kdvRate;
            set
            {
                if (SetProperty(ref _kdvRate, Math.Clamp(value, 0, 100)))
                {
                    CurrentProject.KdvRate = _kdvRate;
                    NotifyFinancialsChanged();
                }
            }
        }

        public decimal KdvAmount => SubTotalAfterDiscount * (KdvRate / 100);
        public decimal GrandTotal => SubTotalAfterDiscount + KdvAmount;
        public decimal TotalProfit => SubTotalAfterDiscount - TotalCost;
        public decimal OverallMargin => SubTotalAfterDiscount > 0 ? (TotalProfit / SubTotalAfterDiscount) * 100 : 0;

        public string TotalRevenueDisplay => $"₺{TotalRevenue:N0}";
        public string TotalCostDisplay => $"₺{TotalCost:N0}";
        public string DiscountAmountDisplay => $"-₺{DiscountAmount:N0}";
        public string SubTotalDisplay => $"₺{SubTotalAfterDiscount:N0}";
        public string KdvAmountDisplay => $"+₺{KdvAmount:N0}";
        public string GrandTotalDisplay => $"₺{GrandTotal:N0}";
        public string TotalProfitDisplay => $"₺{TotalProfit:N0}";
        public string OverallMarginDisplay => $"%{OverallMargin:N1}";
        public string ProfitColor => TotalProfit >= 0 ? "#4CAF50" : "#F44336";

        public string QuoteStatusDisplay => QuoteListViewModel.GetStatusText(CurrentProject.QuoteStatus);
        public string RevisionDisplay => $"R{CurrentProject.RevisionNumber}";

        #endregion

        #region Constructor

        public ProjectQuoteEditorViewModel(IDbContextFactory<AppDbContext> dbContextFactory, ProjectScopeService scopeService)
        {
            _dbContextFactory = dbContextFactory;
            _scopeService = scopeService;

            _ = RefreshAsync();
        }

        public void LoadExistingProject(int projectId)
        {
            _ = LoadProjectAsync(projectId);
        }

        #endregion

        #region Data Loading (Async Short-Lived DbContext)

        private async Task RefreshAsync()
        {
            try
            {
                using var context = await _dbContextFactory.CreateDbContextAsync();
                
                var customers = await context.Customers.OrderBy(c => c.FullName).ToListAsync();
                Customers.Clear();
                foreach (var c in customers)
                    Customers.Add(c);

                var products = await context.Products
                    .OrderBy(p => p.ProductCategoryType)
                    .ThenBy(p => p.ProductName)
                    .ToListAsync();

                ProductCatalog.Clear();
                FilteredProducts.Clear();
                Categories.Clear();

                Categories.Add("Tümü");
                var categoryNames = products
                    .Select(p => p.ProductCategoryType.ToString())
                    .Distinct()
                    .OrderBy(c => c);

                foreach (var cat in categoryNames)
                {
                    Categories.Add(cat);
                }

                foreach (var p in products)
                {
                    ProductCatalog.Add(p);
                    FilteredProducts.Add(p);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Veri yüklenirken hata: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadProjectAsync(int projectId)
        {
            try
            {
                var (project, nodes) = await Task.Run(() => _scopeService.LoadProject(projectId));
                if (project != null)
                {
                    CurrentProject = project;
                    ProjectName = project.Title;
                    SelectedCustomer = Customers.FirstOrDefault(c => c.Id == project.CustomerId);

                    _discountPercent = project.DiscountPercent;
                    OnPropertyChanged(nameof(DiscountPercent));
                    _kdvRate = project.KdvRate > 0 ? project.KdvRate : 20;
                    OnPropertyChanged(nameof(KdvRate));

                    RootNodes.Clear();
                    foreach (var node in nodes)
                    {
                        RootNodes.Add(node);
                    }

                    _undoStack.Clear();
                    _redoStack.Clear();
                    SaveSnapshot();

                    NotifyFinancialsChanged();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Proje yükleme hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void FilterProducts()
        {
            FilteredProducts.Clear();
            var searchLower = ProductSearchText.ToLowerInvariant();

            foreach (var p in ProductCatalog)
            {
                bool matchesCategory = SelectedCategory == "Tümü" || p.ProductCategoryType.ToString().Equals(SelectedCategory, StringComparison.OrdinalIgnoreCase);
                bool matchesSearch = string.IsNullOrEmpty(ProductSearchText) ||
                                     p.ProductName.ToLowerInvariant().Contains(searchLower) ||
                                     (p.SKU?.ToLowerInvariant().Contains(searchLower) ?? false);

                if (matchesCategory && matchesSearch)
                {
                    FilteredProducts.Add(p);
                }
            }
        }

        private void FilterTreeNodes()
        {
            if (string.IsNullOrWhiteSpace(TreeSearchText)) return;

            foreach (var node in RootNodes)
            {
                ApplyTreeFilterRecursive(node, TreeSearchText.Trim().ToLowerInvariant());
            }
        }

        private bool ApplyTreeFilterRecursive(ScopeNode node, string text)
        {
            bool selfMatches = node.Name.ToLowerInvariant().Contains(text);
            bool childMatches = false;

            foreach (var child in node.Children)
            {
                if (ApplyTreeFilterRecursive(child, text))
                {
                    childMatches = true;
                }
            }

            node.IsExpanded = selfMatches || childMatches;
            return selfMatches || childMatches;
        }

        #endregion

        #region Undo / Redo Mechanism

        private void SaveSnapshot()
        {
            if (_isApplyingUndoRedo) return;

            try
            {
                var snapshot = ProjectScopeService.Serialize(RootNodes.ToList());
                _undoStack.Push(snapshot);
                _redoStack.Clear();

                OnPropertyChanged(nameof(CanUndo));
                OnPropertyChanged(nameof(CanRedo));
            }
            catch { }
        }

        [RelayCommand]
        private void Undo()
        {
            if (_undoStack.Count <= 1) return;

            _isApplyingUndoRedo = true;
            try
            {
                var currentSnapshot = _undoStack.Pop();
                _redoStack.Push(currentSnapshot);

                var previousSnapshot = _undoStack.Peek();
                RestoreFromSnapshot(previousSnapshot);
            }
            finally
            {
                _isApplyingUndoRedo = false;
                OnPropertyChanged(nameof(CanUndo));
                OnPropertyChanged(nameof(CanRedo));
            }
        }

        [RelayCommand]
        private void Redo()
        {
            if (_redoStack.Count == 0) return;

            _isApplyingUndoRedo = true;
            try
            {
                var nextSnapshot = _redoStack.Pop();
                _undoStack.Push(nextSnapshot);
                RestoreFromSnapshot(nextSnapshot);
            }
            finally
            {
                _isApplyingUndoRedo = false;
                OnPropertyChanged(nameof(CanUndo));
                OnPropertyChanged(nameof(CanRedo));
            }
        }

        private void RestoreFromSnapshot(string json)
        {
            var nodes = ProjectScopeService.Deserialize(json);
            RootNodes.Clear();
            foreach (var node in nodes)
            {
                RootNodes.Add(node);
            }

            SelectedNode = RootNodes.FirstOrDefault();
            NotifyFinancialsChanged();
        }

        #endregion

        #region Yapı Oluşturma & Toplu Marj

        [RelayCommand]
        private void GenerateStructure()
        {
            if (string.IsNullOrWhiteSpace(ProjectName))
            {
                MessageBox.Show("Lütfen proje adı girin.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show(
                $"Yapı oluşturulacak:\n\n" +
                $"Blok: {BlockCount}\n" +
                $"Kat: {FloorCount}\n" +
                $"Daire/Kat: {FlatsPerFloor}\n\n" +
                $"Toplam: {BlockCount * FloorCount * FlatsPerFloor} daire\n\n" +
                "Mevcut yapı yeniden oluşturulacak. Devam edilsin mi?",
                "Yapı Oluştur",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            SaveSnapshot();

            var projectNode = ProjectScopeService.CreateSampleApartmentStructure(
                ProjectName, BlockCount, FloorCount, FlatsPerFloor);

            RootNodes.Clear();
            RootNodes.Add(projectNode);

            SelectedNode = projectNode;
            NotifyFinancialsChanged();
            SaveSnapshot();
        }

        [RelayCommand]
        private void ApplyBulkMargin()
        {
            var targetNode = SelectedNode ?? RootNodes.FirstOrDefault();
            if (targetNode == null) return;

            var input = Microsoft.VisualBasic.Interaction.InputBox(
                $"'{targetNode.Name}' ve altındaki tüm ürünlere eklenmek istenen KAR MARJI (%) oranını girin:",
                "Toplu Kar Marjı Uygula",
                "20");

            if (decimal.TryParse(input, out decimal marginPercent))
            {
                SaveSnapshot();
                ApplyMarginRecursive(targetNode, marginPercent);
                NotifyFinancialsChanged();
                SaveSnapshot();
                MessageBox.Show($"%{marginPercent:N0} kar marjı başarıyla uygulandı.", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ApplyMarginRecursive(ScopeNode node, decimal marginPercent)
        {
            foreach (var item in node.Items)
            {
                var cost = item.TotalItemCost / Math.Max(1, item.Quantity);
                item.UnitPrice = cost * (1 + (marginPercent / 100m));
            }

            foreach (var child in node.Children)
            {
                ApplyMarginRecursive(child, marginPercent);
            }
            node.NotifyTotalsChanged();
        }

        #endregion

        #region Tree Yönetimi

        [RelayCommand]
        private void AddBlock()
        {
            SaveSnapshot();
            var projectNode = RootNodes.FirstOrDefault();
            if (projectNode == null)
            {
                projectNode = ProjectScopeService.CreateEmptyProjectTree(ProjectName ?? "Yeni Proje");
                RootNodes.Add(projectNode);
            }

            var blockLetter = (char)('A' + projectNode.Children.Count(c => c.Type == NodeType.Block));
            var block = projectNode.AddChild($"{blockLetter} Blok", NodeType.Block);

            SelectedNode = block;
            NotifyFinancialsChanged();
            SaveSnapshot();
        }

        [RelayCommand]
        private void AddFloor()
        {
            SaveSnapshot();
            var projectNode = RootNodes.FirstOrDefault();
            if (projectNode == null)
            {
                projectNode = ProjectScopeService.CreateEmptyProjectTree(string.IsNullOrWhiteSpace(ProjectName) ? "Yeni Proje" : ProjectName);
                RootNodes.Add(projectNode);
            }

            ScopeNode? targetBlock = SelectedNode?.Type == NodeType.Block ? SelectedNode : projectNode.Children.FirstOrDefault(c => c.Type == NodeType.Block);
            targetBlock ??= projectNode.AddChild("A Blok", NodeType.Block);

            var floorCount = targetBlock.Children.Count(c => c.Type == NodeType.Floor);
            var floor = targetBlock.AddChild($"{floorCount + 1}. Kat", NodeType.Floor);

            SelectedNode = floor;
            NotifyFinancialsChanged();
            SaveSnapshot();
        }

        [RelayCommand]
        private void AddFlat()
        {
            SaveSnapshot();
            var projectNode = RootNodes.FirstOrDefault();
            if (projectNode == null)
            {
                projectNode = ProjectScopeService.CreateEmptyProjectTree(string.IsNullOrWhiteSpace(ProjectName) ? "Yeni Proje" : ProjectName);
                RootNodes.Add(projectNode);
            }

            ScopeNode? targetFloor = SelectedNode?.Type == NodeType.Floor ? SelectedNode : null;
            if (targetFloor == null)
            {
                var firstBlock = projectNode.Children.FirstOrDefault(c => c.Type == NodeType.Block) ?? projectNode.AddChild("A Blok", NodeType.Block);
                targetFloor = firstBlock.Children.FirstOrDefault(c => c.Type == NodeType.Floor) ?? firstBlock.AddChild("1. Kat", NodeType.Floor);
            }

            var flatCount = targetFloor.Children.Count(c => c.Type == NodeType.Flat);
            var flat = targetFloor.AddChild($"Daire {flatCount + 1}", NodeType.Flat);

            SelectedNode = flat;
            NotifyFinancialsChanged();
            SaveSnapshot();
        }

        [RelayCommand]
        private void AddZone()
        {
            if (SelectedNode == null) return;
            SaveSnapshot();

            var zoneCount = SelectedNode.Children.Count(c => c.Type == NodeType.Zone);
            var zone = SelectedNode.AddChild($"Bölge {zoneCount + 1}", NodeType.Zone);

            SelectedNode = zone;
            NotifyFinancialsChanged();
            SaveSnapshot();
        }

        [RelayCommand]
        private void DuplicateNode()
        {
            if (SelectedNode == null || SelectedNode.Parent == null) return;

            SaveSnapshot();
            var clone = SelectedNode.Clone($"{SelectedNode.Name} (Kopya)");
            clone.Parent = SelectedNode.Parent;
            SelectedNode.Parent.Children.Add(clone);

            SelectedNode = clone;
            NotifyFinancialsChanged();
            SaveSnapshot();
        }

        [RelayCommand]
        private void RenameNode(object? parameter)
        {
            if (SelectedNode == null) return;

            var newName = parameter as string;
            if (!string.IsNullOrWhiteSpace(newName))
            {
                SaveSnapshot();
                SelectedNode.Name = newName;
                OnPropertyChanged(nameof(SelectedNodeName));
                SaveSnapshot();
            }
        }

        [RelayCommand]
        private void RemoveNode()
        {
            if (SelectedNode == null || SelectedNode.Type == NodeType.Project) return;

            var result = MessageBox.Show(
                $"'{SelectedNode.Name}' ve tüm alt öğeleri silinecek. Devam edilsin mi?",
                "Node Sil",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            SaveSnapshot();
            var parent = SelectedNode.Parent;
            if (parent != null)
            {
                parent.Children.Remove(SelectedNode);
                parent.NotifyTotalsChanged();
                SelectedNode = parent;
            }
            else
            {
                RootNodes.Remove(SelectedNode);
                SelectedNode = RootNodes.FirstOrDefault();
            }

            NotifyFinancialsChanged();
            SaveSnapshot();
        }

        [RelayCommand]
        private void ApplyToSiblings()
        {
            if (SelectedNode?.Parent == null) return;

            var siblings = SelectedNode.Parent.Children
                .Where(c => c.Type == SelectedNode.Type && c.Id != SelectedNode.Id)
                .ToList();

            if (!siblings.Any())
            {
                MessageBox.Show("Aynı tipte başka node bulunamadı.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                $"Bu node'un kalemleri {siblings.Count} kardeş node'a kopyalanacak. Devam edilsin mi?",
                "Kardeşlere Uygula",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            SaveSnapshot();
            foreach (var sibling in siblings)
            {
                sibling.Items.Clear();
                SelectedNode.CopyItemsTo(sibling);
            }

            NotifyFinancialsChanged();
            SaveSnapshot();
            MessageBox.Show($"{siblings.Count} node güncellendi.", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        #endregion

        #region Kalem Yönetimi & Miktar Step

        private void RefreshCurrentNodeItems()
        {
            CurrentNodeItems.Clear();
            if (SelectedNode == null) return;

            foreach (var item in SelectedNode.Items)
            {
                item.OnItemChanged = () =>
                {
                    SelectedNode.NotifyTotalsChanged();
                    NotifyFinancialsChanged();
                };
                CurrentNodeItems.Add(item);
            }
        }

        [RelayCommand]
        private void AddItem()
        {
            if (SelectedNode == null || SelectedProduct == null) return;

            SaveSnapshot();
            var item = ScopeNodeItem.FromProduct(SelectedProduct);
            item.OnItemChanged = () =>
            {
                SelectedNode.NotifyTotalsChanged();
                NotifyFinancialsChanged();
            };

            SelectedNode.Items.Add(item);
            CurrentNodeItems.Add(item);
            SelectedNode.NotifyTotalsChanged();
            NotifyFinancialsChanged();
            SaveSnapshot();
        }

        [RelayCommand]
        private void RemoveItem()
        {
            if (SelectedNode == null || SelectedItem == null) return;

            SaveSnapshot();
            SelectedNode.Items.Remove(SelectedItem);
            CurrentNodeItems.Remove(SelectedItem);
            SelectedNode.NotifyTotalsChanged();
            NotifyFinancialsChanged();
            SaveSnapshot();
        }

        [RelayCommand]
        private void IncrementItemQuantity(ScopeNodeItem? item)
        {
            if (item != null)
            {
                item.Quantity += 1;
            }
        }

        [RelayCommand]
        private void DecrementItemQuantity(ScopeNodeItem? item)
        {
            if (item != null && item.Quantity > 1)
            {
                item.Quantity -= 1;
            }
        }

        #endregion

        #region Drag & Drop (IDropTarget)

        void IDropTarget.DragOver(IDropInfo dropInfo)
        {
            if (dropInfo.Data is Product && SelectedNode != null)
            {
                dropInfo.DropTargetAdorner = DropTargetAdorners.Highlight;
                dropInfo.Effects = DragDropEffects.Copy;
            }
        }

        void IDropTarget.Drop(IDropInfo dropInfo)
        {
            if (dropInfo.Data is Product product && SelectedNode != null)
            {
                SaveSnapshot();
                var item = ScopeNodeItem.FromProduct(product);
                item.OnItemChanged = () =>
                {
                    SelectedNode.NotifyTotalsChanged();
                    NotifyFinancialsChanged();
                };

                SelectedNode.Items.Add(item);
                CurrentNodeItems.Add(item);
                SelectedNode.NotifyTotalsChanged();
                NotifyFinancialsChanged();
                SaveSnapshot();
            }
        }

        #endregion

        #region Finansal Güncellemeler

        public void NotifyFinancialsChanged()
        {
            OnPropertyChanged(nameof(TotalRevenue));
            OnPropertyChanged(nameof(TotalCost));
            OnPropertyChanged(nameof(DiscountAmount));
            OnPropertyChanged(nameof(SubTotalAfterDiscount));
            OnPropertyChanged(nameof(KdvAmount));
            OnPropertyChanged(nameof(GrandTotal));
            OnPropertyChanged(nameof(TotalProfit));
            OnPropertyChanged(nameof(OverallMargin));
            OnPropertyChanged(nameof(TotalRevenueDisplay));
            OnPropertyChanged(nameof(TotalCostDisplay));
            OnPropertyChanged(nameof(DiscountAmountDisplay));
            OnPropertyChanged(nameof(SubTotalDisplay));
            OnPropertyChanged(nameof(KdvAmountDisplay));
            OnPropertyChanged(nameof(GrandTotalDisplay));
            OnPropertyChanged(nameof(TotalProfitDisplay));
            OnPropertyChanged(nameof(OverallMarginDisplay));
            OnPropertyChanged(nameof(ProfitColor));
            OnPropertyChanged(nameof(SelectedNodeSubTotal));
            OnPropertyChanged(nameof(QuoteStatusDisplay));
            OnPropertyChanged(nameof(RevisionDisplay));
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));
        }

        #endregion

        #region Kaydetme, PDF & E-Posta

        [RelayCommand]
        private async Task Save()
        {
            if (SelectedCustomer == null)
            {
                MessageBox.Show("Lütfen projeyi kaydetmeden önce bir MÜŞTERİ seçiniz.", "Müşteri Seçilmedi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(ProjectName))
            {
                MessageBox.Show("Lütfen projeyi kaydetmeden önce bir PROJE ADI giriniz.", "Proje Adı Eksik", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                CurrentProject.Title = ProjectName;
                CurrentProject.CustomerId = SelectedCustomer.Id;

                using var context = await _dbContextFactory.CreateDbContextAsync();

                if (string.IsNullOrEmpty(CurrentProject.QuoteNumber))
                {
                    var year = DateTime.UtcNow.Year;
                    var count = await context.ServiceProjects.CountAsync(p => p.QuoteNumber != null && p.CreatedDate.Year == year) + 1;
                    CurrentProject.QuoteNumber = $"TEK-{year}-{count:D3}";
                }

                if (CurrentProject.Id > 0)
                {
                    var revisions = new List<QuoteRevision>();
                    if (!string.IsNullOrEmpty(CurrentProject.RevisionsJson))
                    {
                        revisions = JsonSerializer.Deserialize<List<QuoteRevision>>(CurrentProject.RevisionsJson) ?? new();
                    }

                    revisions.Add(new QuoteRevision
                    {
                        RevisionNumber = CurrentProject.RevisionNumber,
                        CreatedDate = DateTime.UtcNow,
                        ChangeDescription = $"R{CurrentProject.RevisionNumber} kaydedildi",
                        TotalBudget = SubTotalAfterDiscount,
                        DiscountPercent = DiscountPercent,
                        ScopeSnapshotJson = ProjectScopeService.Serialize(RootNodes.ToList())
                    });

                    CurrentProject.RevisionsJson = JsonSerializer.Serialize(revisions);
                    CurrentProject.RevisionNumber++;
                }

                await Task.Run(() => _scopeService.SaveProject(CurrentProject, RootNodes.ToList()));

                MessageBox.Show(
                    $"Proje başarıyla kaydedildi!\n\n" +
                    $"Proje Kodu: {CurrentProject.ProjectCode}\n" +
                    $"Teklif No: {CurrentProject.QuoteNumber}\n" +
                    $"Revizyon: R{CurrentProject.RevisionNumber}\n" +
                    $"Toplam: {GrandTotalDisplay}\n" +
                    $"Kar: {TotalProfitDisplay} ({OverallMarginDisplay})",
                    "Başarılı",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Kayıt sırasında hata oluştu: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void ExportPdf()
        {
            if (!RootNodes.Any())
            {
                MessageBox.Show("Dışa aktarılacak veri yok.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                FileName = $"Teklif_{ProjectName}_{DateTime.UtcNow:yyyyMMdd}",
                DefaultExt = ".pdf",
                Filter = "PDF Belgeleri (.pdf)|*.pdf"
            };

            if (dialog.ShowDialog() == true)
            {
                GeneratePdf(dialog.FileName, true);
            }
        }

        private void GeneratePdf(string filePath, bool openAfter)
        {
            try
            {
                var pdfService = new PdfService();
                var exportProject = CurrentProject;
                exportProject.Title = ProjectName;
                if (SelectedCustomer != null) exportProject.Customer = SelectedCustomer;

                pdfService.GenerateProjectQuote(exportProject, RootNodes.ToList(), filePath);

                if (openAfter)
                {
                    var result = MessageBox.Show("PDF oluşturuldu. Açmak ister misiniz?", "Başarılı", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (result == MessageBoxResult.Yes)
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(filePath) { UseShellExecute = true });
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"PDF oluşturulurken hata: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task SendEmail()
        {
            if (SelectedCustomer == null || string.IsNullOrWhiteSpace(SelectedCustomer.Email))
            {
                MessageBox.Show("Müşterinin e-posta adresi kayıtlı değil.", "Hata", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show($"{SelectedCustomer.Email} adresine teklif gönderilecek. Onaylıyor musunuz?", "E-Posta Gönder", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;

            string tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"Teklif_{ProjectName}_{DateTime.UtcNow:yyyyMMddHHmmss}.pdf");

            try
            {
                GeneratePdf(tempPath, false);
                var emailService = new EmailService();
                string subject = $"Teklif: {ProjectName}";
                string body = $"Sayın {SelectedCustomer.FullName},<br><br>Projenize ait teknik ve ticari teklifimiz ektedir.<br><br>Saygılarımızla,<br>Kamatek Teknik Servis";

                await emailService.SendQuoteEmailAsync(SelectedCustomer.Email, subject, body, tempPath);
                MessageBox.Show("E-posta başarıyla gönderildi.", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"E-posta hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                try { if (System.IO.File.Exists(tempPath)) System.IO.File.Delete(tempPath); } catch { }
            }
        }

        [RelayCommand]
        private void Cancel(object? parameter)
        {
            if (parameter is Window window)
                window.Close();
        }

        #endregion
    }
}
