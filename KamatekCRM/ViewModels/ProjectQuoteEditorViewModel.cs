using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using GongSolutions.Wpf.DragDrop;
using CommunityToolkit.Mvvm.Input;
using KamatekCrm.ApplicationCore.DTOs.ProjectQuotes;
using KamatekCrm.ApplicationCore.Interfaces;
using KamatekCrm.ApplicationCore.Services;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;
using KamatekCrm.Shared.Services;
using KamatekCrm.Services;

namespace KamatekCrm.ViewModels
{
    /// <summary>
    /// Proje Teklif Editörü ViewModel - Yenilenmiş 3 Panelli Workbench
    /// Drag & Drop, Tree Yönetimi, Finansal Hesaplamalar, Undo/Redo, Toplu Marj
    /// </summary>
    public partial class ProjectQuoteEditorViewModel : ViewModelBase, IDropTarget
    {
        private readonly IProjectQuoteReadService _readService;
        private readonly IProjectQuoteCommandService _commandService;
        private readonly IDialogService _dialogService;
        private readonly IToastService _toastService;
        private readonly PdfService _pdfService;
        private readonly EmailService _emailService;
        private readonly Task _workspaceLoadTask;
        private Guid _saveOperationId = Guid.NewGuid();
        private ProjectQuotePricingResult _pricing = EmptyPricing();

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

        public decimal TotalRevenue => _pricing.GrossRevenue;
        public decimal TotalCost => _pricing.TotalCost;

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

        public decimal DiscountAmount => _pricing.DiscountAmount;
        public decimal SubTotalAfterDiscount => _pricing.NetRevenue;

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

        public decimal KdvAmount => _pricing.VatAmount;
        public decimal GrandTotal => _pricing.GrandTotal;
        public decimal TotalProfit => _pricing.TotalProfit;
        public decimal OverallMargin => _pricing.MarginPercent;

        public string TotalRevenueDisplay => $"₺{TotalRevenue:N2}";
        public string TotalCostDisplay => $"₺{TotalCost:N2}";
        public string DiscountAmountDisplay => $"-₺{DiscountAmount:N2}";
        public string SubTotalDisplay => $"₺{SubTotalAfterDiscount:N2}";
        public string KdvAmountDisplay => $"+₺{KdvAmount:N2}";
        public string GrandTotalDisplay => $"₺{GrandTotal:N2}";
        public string TotalProfitDisplay => $"₺{TotalProfit:N2}";
        public string OverallMarginDisplay => $"%{OverallMargin:N1}";
        public string ProfitColor => TotalProfit >= 0 ? "#4CAF50" : "#F44336";

        public string QuoteStatusDisplay => QuoteListViewModel.GetStatusText(CurrentProject.QuoteStatus);
        public string RevisionDisplay => $"R{CurrentProject.RevisionNumber}";

        #endregion

        #region Constructor

        public ProjectQuoteEditorViewModel(
            IProjectQuoteReadService readService,
            IProjectQuoteCommandService commandService,
            IDialogService dialogService,
            IToastService toastService,
            PdfService pdfService,
            EmailService emailService)
        {
            _readService = readService;
            _commandService = commandService;
            _dialogService = dialogService;
            _toastService = toastService;
            _pdfService = pdfService;
            _emailService = emailService;

            _workspaceLoadTask = RefreshAsync();
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
                var result = await _readService.GetWorkspaceAsync();
                if (result.IsFailure || result.Value is null)
                {
                    _toastService.ShowError(result.Error);
                    return;
                }

                Customers.Clear();
                foreach (var customer in result.Value.Customers)
                    Customers.Add(ToCustomer(customer));

                ProductCatalog.Clear();
                FilteredProducts.Clear();
                Categories.Clear();

                Categories.Add("Tümü");
                var categoryNames = result.Value.Products
                    .Select(product => product.Category.ToString())
                    .Distinct()
                    .OrderBy(c => c);

                foreach (var cat in categoryNames)
                {
                    Categories.Add(cat);
                }

                foreach (var product in result.Value.Products)
                {
                    var p = ToProduct(product);
                    ProductCatalog.Add(p);
                    FilteredProducts.Add(p);
                }
            }
            catch (Exception ex)
            {
                _toastService.ShowError($"Teklif çalışma alanı yüklenemedi: {ex.Message}");
            }
        }

        private async Task LoadProjectAsync(int projectId)
        {
            try
            {
                await _workspaceLoadTask;
                var result = await _readService.GetAsync(projectId);
                if (result.IsFailure || result.Value is null)
                {
                    _toastService.ShowError(result.Error);
                    return;
                }

                var detail = result.Value;
                var project = ToProject(detail);
                var nodes = ProjectScopeService.Deserialize(detail.ProjectScopeJson);
                CurrentProject = project;
                ProjectName = project.Title;
                SelectedCustomer = Customers.FirstOrDefault(c => c.Id == project.CustomerId);

                _discountPercent = project.DiscountPercent;
                OnPropertyChanged(nameof(DiscountPercent));
                _kdvRate = project.KdvRate;
                OnPropertyChanged(nameof(KdvRate));

                RootNodes.Clear();
                foreach (var node in nodes)
                {
                    RootNodes.Add(node);
                }

                _undoStack.Clear();
                _redoStack.Clear();
                SaveSnapshot();
                _saveOperationId = Guid.NewGuid();

                NotifyFinancialsChanged();
            }
            catch (Exception ex)
            {
                _toastService.ShowError($"Proje teklifi yüklenemedi: {ex.Message}");
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
        private async Task GenerateStructure()
        {
            if (string.IsNullOrWhiteSpace(ProjectName))
            {
                _toastService.ShowWarning("Lütfen proje adı girin.");
                return;
            }

            var confirmed = await _dialogService.ShowConfirmationAsync(
                $"Yapı oluşturulacak:\n\n" +
                $"Blok: {BlockCount}\n" +
                $"Kat: {FloorCount}\n" +
                $"Daire/Kat: {FlatsPerFloor}\n\n" +
                $"Toplam: {BlockCount * FloorCount * FlatsPerFloor} daire\n\n" +
                "Mevcut yapı yeniden oluşturulacak. Devam edilsin mi?",
                "Yapı Oluştur");

            if (!confirmed) return;

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
        private async Task ApplyBulkMargin()
        {
            var targetNode = SelectedNode ?? RootNodes.FirstOrDefault();
            if (targetNode == null) return;

            var input = await _dialogService.ShowInputAsync(
                $"'{targetNode.Name}' ve altındaki tüm ürünlere eklenmek istenen KAR MARJI (%) oranını girin:",
                "Toplu Kar Marjı Uygula",
                "20");

            if (decimal.TryParse(input, out decimal marginPercent))
            {
                if (marginPercent is < -100 or > 10_000)
                {
                    _toastService.ShowWarning("Kar marjı -100 ile 10.000 arasında olmalıdır.");
                    return;
                }
                SaveSnapshot();
                ApplyMarginRecursive(targetNode, marginPercent);
                NotifyFinancialsChanged();
                SaveSnapshot();
                _toastService.ShowSuccess($"%{marginPercent:N0} kar marjı uygulandı.");
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
        private async Task RemoveNode()
        {
            if (SelectedNode == null || SelectedNode.Type == NodeType.Project) return;

            var confirmed = await _dialogService.ShowConfirmationAsync(
                $"'{SelectedNode.Name}' ve tüm alt öğeleri silinecek. Devam edilsin mi?",
                "Kapsam Düğümünü Sil");

            if (!confirmed) return;

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
        private async Task ApplyToSiblings()
        {
            if (SelectedNode?.Parent == null) return;

            var siblings = SelectedNode.Parent.Children
                .Where(c => c.Type == SelectedNode.Type && c.Id != SelectedNode.Id)
                .ToList();

            if (!siblings.Any())
            {
                _toastService.ShowInfo("Aynı tipte başka kapsam düğümü bulunamadı.");
                return;
            }

            var confirmed = await _dialogService.ShowConfirmationAsync(
                $"Bu node'un kalemleri {siblings.Count} kardeş node'a kopyalanacak. Devam edilsin mi?",
                "Kardeşlere Uygula");

            if (!confirmed) return;

            SaveSnapshot();
            foreach (var sibling in siblings)
            {
                sibling.Items.Clear();
                SelectedNode.CopyItemsTo(sibling);
            }

            NotifyFinancialsChanged();
            SaveSnapshot();
            _toastService.ShowSuccess($"{siblings.Count} kapsam düğümü güncellendi.");
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
            var pricing = ProjectQuotePricingPolicy.Calculate(RootNodes, DiscountPercent, KdvRate);
            _pricing = pricing.Value ?? EmptyPricing();
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

        private static ProjectQuotePricingResult EmptyPricing() =>
            new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

        #endregion

        #region Kaydetme, PDF & E-Posta

        [RelayCommand]
        private async Task Save()
        {
            if (SelectedCustomer == null)
            {
                _toastService.ShowWarning("Projeyi kaydetmeden önce bir müşteri seçin.");
                return;
            }

            if (string.IsNullOrWhiteSpace(ProjectName))
            {
                _toastService.ShowWarning("Projeyi kaydetmeden önce proje adı girin.");
                return;
            }

            try
            {
                var result = await _commandService.SaveAsync(new SaveProjectQuoteCommand(
                    _saveOperationId,
                    CurrentProject.Id > 0 ? CurrentProject.Id : null,
                    CurrentProject.RevisionNumber,
                    SelectedCustomer.Id,
                    ProjectName,
                    ProjectScopeService.Serialize(RootNodes.ToList()),
                    DiscountPercent,
                    KdvRate));
                if (result.IsFailure || result.Value is null)
                {
                    _toastService.ShowError(result.Error);
                    return;
                }

                var saved = result.Value;
                CurrentProject.Id = saved.ProjectId;
                CurrentProject.Title = ProjectName.Trim();
                CurrentProject.CustomerId = SelectedCustomer.Id;
                CurrentProject.ProjectCode = saved.ProjectCode;
                CurrentProject.QuoteNumber = saved.QuoteNumber;
                CurrentProject.RevisionNumber = saved.RevisionNumber;
                CurrentProject.QuoteStatus = saved.Status;
                CurrentProject.ProjectScopeJson = ProjectScopeService.Serialize(RootNodes.ToList());
                CurrentProject.DiscountPercent = DiscountPercent;
                CurrentProject.KdvRate = KdvRate;
                CurrentProject.TotalBudget = saved.Pricing.NetRevenue;
                CurrentProject.TotalCost = saved.Pricing.TotalCost;
                CurrentProject.TotalProfit = saved.Pricing.TotalProfit;
                _pricing = saved.Pricing;
                _saveOperationId = Guid.NewGuid();
                NotifyFinancialsChanged();

                var message = saved.WasAlreadyApplied
                    ? $"Teklif daha önce kaydedilmişti: {saved.QuoteNumber} / R{saved.RevisionNumber}"
                    : saved.WasNoOp
                        ? $"Değişiklik bulunmadı: {saved.QuoteNumber} / R{saved.RevisionNumber}"
                        : $"Teklif kaydedildi: {saved.QuoteNumber} / R{saved.RevisionNumber} — {GrandTotalDisplay}";
                _toastService.ShowSuccess(message);
            }
            catch (Exception ex)
            {
                _toastService.ShowError($"Teklif kaydı tamamlanamadı: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task ExportPdf()
        {
            if (!RootNodes.Any())
            {
                _toastService.ShowWarning("Dışa aktarılacak teklif kapsamı yok.");
                return;
            }

            var filePath = await _dialogService.ShowSaveFileDialogAsync(
                "Proje Teklifini Kaydet",
                "PDF Belgeleri (.pdf)|*.pdf",
                $"Teklif_{SanitizeFileName(ProjectName)}_{DateTime.UtcNow:yyyyMMdd}.pdf");
            if (!string.IsNullOrWhiteSpace(filePath)) await GeneratePdfAsync(filePath, true);
        }

        private async Task<bool> GeneratePdfAsync(string filePath, bool openAfter)
        {
            try
            {
                var exportProject = CloneForExport();

                _pdfService.GenerateProjectQuote(exportProject, RootNodes.ToList(), filePath);

                if (openAfter)
                {
                    var shouldOpen = await _dialogService.ShowConfirmationAsync(
                        "PDF oluşturuldu. Şimdi açmak ister misiniz?",
                        "Teklif PDF'i Hazır");
                    if (shouldOpen)
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(filePath) { UseShellExecute = true });
                    }
                }
                else
                {
                    _toastService.ShowSuccess("Teklif PDF'i oluşturuldu.");
                }
                return true;
            }
            catch (Exception ex)
            {
                _toastService.ShowError($"PDF oluşturulamadı: {ex.Message}");
                return false;
            }
        }

        [RelayCommand]
        private async Task SendEmail()
        {
            if (SelectedCustomer == null || string.IsNullOrWhiteSpace(SelectedCustomer.Email))
            {
                _toastService.ShowWarning("Müşterinin e-posta adresi kayıtlı değil.");
                return;
            }

            var confirmed = await _dialogService.ShowConfirmationAsync(
                $"Teklif {SelectedCustomer.Email} adresine gönderilecek. Onaylıyor musunuz?",
                "E-Posta Gönder");
            if (!confirmed) return;

            string tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"Teklif_{ProjectName}_{DateTime.UtcNow:yyyyMMddHHmmss}.pdf");

            try
            {
                if (!await GeneratePdfAsync(tempPath, false)) return;
                string subject = $"Teklif: {ProjectName}";
                string body = $"Sayın {SelectedCustomer.FullName},<br><br>Projenize ait teknik ve ticari teklifimiz ektedir.<br><br>Saygılarımızla,<br>Kamatek Teknik Servis";

                await _emailService.SendQuoteEmailAsync(SelectedCustomer.Email, subject, body, tempPath);
                _toastService.ShowSuccess("Teklif e-postası gönderildi.");
            }
            catch (Exception ex)
            {
                _toastService.ShowError($"E-posta gönderilemedi: {ex.Message}");
            }
            finally
            {
                try { if (System.IO.File.Exists(tempPath)) System.IO.File.Delete(tempPath); } catch { }
            }
        }

        private ServiceProject CloneForExport() => new()
        {
            Id = CurrentProject.Id,
            Title = ProjectName.Trim(),
            Name = CurrentProject.Name,
            CustomerId = SelectedCustomer?.Id,
            Customer = SelectedCustomer,
            ProjectCode = CurrentProject.ProjectCode,
            ProjectScopeJson = ProjectScopeService.Serialize(RootNodes.ToList()),
            TotalBudget = SubTotalAfterDiscount,
            TotalCost = TotalCost,
            TotalProfit = TotalProfit,
            DiscountPercent = DiscountPercent,
            CreatedDate = CurrentProject.CreatedDate,
            PipelineStage = CurrentProject.PipelineStage,
            Status = CurrentProject.Status,
            TotalUnitCount = CurrentProject.TotalUnitCount,
            SurveyNotes = CurrentProject.SurveyNotes,
            QuoteItemsJson = CurrentProject.QuoteItemsJson,
            QuoteNumber = CurrentProject.QuoteNumber,
            QuoteStatus = CurrentProject.QuoteStatus,
            RevisionNumber = CurrentProject.RevisionNumber,
            SentDate = CurrentProject.SentDate,
            ValidUntil = CurrentProject.ValidUntil,
            ApprovedDate = CurrentProject.ApprovedDate,
            RejectedDate = CurrentProject.RejectedDate,
            RejectionReason = CurrentProject.RejectionReason,
            KdvRate = KdvRate,
            Notes = CurrentProject.Notes,
            PaymentTerms = CurrentProject.PaymentTerms,
            RevisionsJson = CurrentProject.RevisionsJson
        };

        private static Customer ToCustomer(ProjectQuoteCustomerDto source) => new()
        {
            Id = source.Id,
            CustomerCode = source.CustomerCode,
            FullName = source.FullName,
            PhoneNumber = source.PhoneNumber,
            Email = source.Email,
            City = source.City,
            District = source.District,
            Neighborhood = source.Neighborhood,
            Street = source.Street,
            BuildingNo = source.BuildingNo,
            ApartmentNo = source.ApartmentNo
        };

        private static Product ToProduct(ProjectQuoteProductDto source) => new()
        {
            Id = source.Id,
            ProductName = source.ProductName,
            SKU = source.Sku ?? string.Empty,
            ProductCategoryType = source.Category,
            PurchasePrice = source.PurchasePrice,
            SalePrice = source.SalePrice,
            ImagePath = source.ImagePath
        };

        private static ServiceProject ToProject(ProjectQuoteDetailDto source) => new()
        {
            Id = source.Id,
            Title = source.Title,
            Name = source.Title,
            CustomerId = source.CustomerId,
            ProjectCode = source.ProjectCode,
            ProjectScopeJson = source.ProjectScopeJson,
            TotalBudget = source.TotalBudget,
            TotalCost = source.TotalCost,
            TotalProfit = source.TotalProfit,
            DiscountPercent = source.DiscountPercent,
            CreatedDate = source.CreatedDate,
            PipelineStage = source.PipelineStage,
            Status = source.Status,
            TotalUnitCount = source.TotalUnitCount,
            SurveyNotes = source.SurveyNotes,
            QuoteItemsJson = source.QuoteItemsJson,
            QuoteNumber = source.QuoteNumber,
            QuoteStatus = source.QuoteStatus,
            RevisionNumber = source.RevisionNumber,
            SentDate = source.SentDate,
            ValidUntil = source.ValidUntil,
            ApprovedDate = source.ApprovedDate,
            RejectedDate = source.RejectedDate,
            RejectionReason = source.RejectionReason,
            KdvRate = source.KdvRate,
            Notes = source.Notes,
            PaymentTerms = source.PaymentTerms,
            RevisionsJson = source.RevisionsJson
        };

        private static string SanitizeFileName(string value)
        {
            var invalid = System.IO.Path.GetInvalidFileNameChars();
            return string.Concat(value.Trim().Select(character => invalid.Contains(character) ? '_' : character));
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
