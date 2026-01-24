using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using GongSolutions.Wpf.DragDrop;
using KamatekCrm.Commands;
using KamatekCrm.Data;
using KamatekCrm.Enums;
using KamatekCrm.Models;
using KamatekCrm.Services;
using Microsoft.EntityFrameworkCore;

namespace KamatekCrm.ViewModels
{
    /// <summary>
    /// Proje Teklif Editörü ViewModel - Üç Panelli Workbench
    /// Drag & Drop, Tree Yönetimi, Finansal Hesaplamalar
    /// </summary>
    public class ProjectQuoteEditorViewModel : ViewModelBase, IDropTarget
    {
        private readonly AppDbContext _context;
        private readonly ProjectScopeService _scopeService;

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

        #region Properties - Tree (Sol Panel)

        public ObservableCollection<ScopeNode> RootNodes { get; } = new();

        private ScopeNode? _selectedNode;
        public ScopeNode? SelectedNode
        {
            get => _selectedNode;
            set
            {
                if (SetProperty(ref _selectedNode, value))
                {
                    // Seçili node değiştiğinde orta paneli güncelle
                    RefreshCurrentNodeItems();
                    OnPropertyChanged(nameof(SelectedNodeName));
                    OnPropertyChanged(nameof(SelectedNodeSubTotal));
                    OnPropertyChanged(nameof(HasSelectedNode));
                    OnPropertyChanged(nameof(CanAddFloor));
                    OnPropertyChanged(nameof(CanAddFlat));
                }
            }
        }

        public string SelectedNodeName => SelectedNode?.Name ?? "Seçili Node Yok";
        public decimal SelectedNodeSubTotal => SelectedNode?.SubTotal ?? 0;
        public bool HasSelectedNode => SelectedNode != null;
        public bool CanAddFloor => SelectedNode?.Type == NodeType.Block;
        public bool CanAddFlat => SelectedNode?.Type == NodeType.Floor || SelectedNode?.Type == NodeType.Flat;

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

        #region Properties - Ürün Kataloğu (Sağ Panel)

        public ObservableCollection<Product> ProductCatalog { get; } = new();
        public ObservableCollection<Product> FilteredProducts { get; } = new();

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
        public decimal TotalProfit => TotalRevenue - TotalCost;
        public decimal OverallMargin => TotalRevenue > 0 ? (TotalProfit / TotalRevenue) * 100 : 0;

        public string TotalRevenueDisplay => $"₺{TotalRevenue:N0}";
        public string TotalCostDisplay => $"₺{TotalCost:N0}";
        public string TotalProfitDisplay => $"₺{TotalProfit:N0}";
        public string OverallMarginDisplay => $"%{OverallMargin:N1}";
        public string ProfitColor => TotalProfit >= 0 ? "#4CAF50" : "#F44336";

        #endregion

        #region Commands

        // Yapı Oluşturma
        public ICommand GenerateStructureCommand { get; }

        // Tree Yönetimi
        public ICommand AddBlockCommand { get; }
        public ICommand AddFloorCommand { get; }
        public ICommand AddFlatCommand { get; }
        public ICommand AddZoneCommand { get; }
        public ICommand DuplicateNodeCommand { get; }
        public ICommand RenameNodeCommand { get; }
        public ICommand RemoveNodeCommand { get; }
        public ICommand ApplyToSiblingsCommand { get; }

        // Kalem Yönetimi
        public ICommand AddItemCommand { get; }
        public ICommand RemoveItemCommand { get; }

        // Proje İşlemleri
        public ICommand SaveCommand { get; }
        public ICommand ExportPdfCommand { get; }
        public ICommand CancelCommand { get; }

        #endregion

        #region Constructor

        public ProjectQuoteEditorViewModel()
        {
            _context = new AppDbContext();
            _scopeService = new ProjectScopeService(_context);

            // Commands
            GenerateStructureCommand = new RelayCommand(_ => GenerateStructure());

            AddBlockCommand = new RelayCommand(_ => AddBlock());
            AddFloorCommand = new RelayCommand(_ => AddFloor(), _ => CanAddFloor);
            AddFlatCommand = new RelayCommand(_ => AddFlat(), _ => CanAddFlat);
            AddZoneCommand = new RelayCommand(_ => AddZone(), _ => HasSelectedNode);
            DuplicateNodeCommand = new RelayCommand(_ => DuplicateNode(), _ => SelectedNode != null && SelectedNode.Type != NodeType.Project);
            RenameNodeCommand = new RelayCommand(_ => RenameNode(), _ => HasSelectedNode);
            RemoveNodeCommand = new RelayCommand(_ => RemoveNode(), _ => SelectedNode != null && SelectedNode.Type != NodeType.Project);
            ApplyToSiblingsCommand = new RelayCommand(_ => ApplyToSiblings(), _ => SelectedNode?.Parent != null);

            AddItemCommand = new RelayCommand(_ => AddItemToNode(), _ => HasSelectedNode && SelectedProduct != null);
            RemoveItemCommand = new RelayCommand(_ => RemoveItemFromNode(), _ => SelectedItem != null);

            SaveCommand = new RelayCommand(_ => Save(), _ => CanSave());
            ExportPdfCommand = new RelayCommand(_ => ExportPdf());
            SendEmailCommand = new RelayCommand(_ => SendEmail(), _ => CanSave());
            CancelCommand = new RelayCommand(CloseWindow);

            LoadData();
        }

        /// <summary>
        /// Mevcut projeyi yüklemek için constructor
        /// </summary>
        public ProjectQuoteEditorViewModel(int projectId) : this()
        {
            LoadProject(projectId);
        }

        #endregion

        #region Data Loading

        private void LoadData()
        {
            // Müşterileri yükle
            var customers = _context.Customers.OrderBy(c => c.FullName).ToList();
            Customers.Clear();
            foreach (var c in customers)
                Customers.Add(c);

            // Ürün kataloğunu yükle
            var products = _context.Products
                .OrderBy(p => p.ProductCategoryType)
                .ThenBy(p => p.ProductName)
                .ToList();
            ProductCatalog.Clear();
            FilteredProducts.Clear();
            foreach (var p in products)
            {
                ProductCatalog.Add(p);
                FilteredProducts.Add(p);
            }
        }

        private void LoadProject(int projectId)
        {
            var (project, nodes) = _scopeService.LoadProject(projectId);
            if (project != null)
            {
                CurrentProject = project;
                ProjectName = project.Title;
                SelectedCustomer = Customers.FirstOrDefault(c => c.Id == project.CustomerId);

                RootNodes.Clear();
                foreach (var node in nodes)
                {
                    RootNodes.Add(node);
                }

                NotifyFinancialsChanged();
            }
        }

        private void FilterProducts()
        {
            FilteredProducts.Clear();
            var searchLower = ProductSearchText.ToLowerInvariant();

            foreach (var p in ProductCatalog)
            {
                if (string.IsNullOrEmpty(ProductSearchText) ||
                    p.ProductName.ToLowerInvariant().Contains(searchLower) ||
                    (p.SKU?.ToLowerInvariant().Contains(searchLower) ?? false))
                {
                    FilteredProducts.Add(p);
                }
            }
        }

        #endregion

        #region Yapı Oluşturma

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
                "Mevcut yapı silinecek. Devam edilsin mi?",
                "Yapı Oluştur",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            // Yeni yapı oluştur
            var projectNode = ProjectScopeService.CreateSampleApartmentStructure(
                ProjectName, BlockCount, FloorCount, FlatsPerFloor);

            RootNodes.Clear();
            RootNodes.Add(projectNode);

            SelectedNode = projectNode;
            NotifyFinancialsChanged();
        }

        #endregion

        #region Tree Yönetimi

        private void AddBlock()
        {
            var projectNode = RootNodes.FirstOrDefault();
            if (projectNode == null)
            {
                // Proje node'u yoksa oluştur
                projectNode = ProjectScopeService.CreateEmptyProjectTree(ProjectName ?? "Yeni Proje");
                RootNodes.Add(projectNode);
            }

            var blockLetter = (char)('A' + projectNode.Children.Count(c => c.Type == NodeType.Block));
            var block = projectNode.AddChild($"{blockLetter} Blok", NodeType.Block);

            SelectedNode = block;
            NotifyFinancialsChanged();
        }

        private void AddFloor()
        {
            if (SelectedNode?.Type != NodeType.Block) return;

            var floorCount = SelectedNode.Children.Count(c => c.Type == NodeType.Floor);
            var floor = SelectedNode.AddChild($"{floorCount + 1}. Kat", NodeType.Floor);

            SelectedNode = floor;
            NotifyFinancialsChanged();
        }

        private void AddFlat()
        {
            if (SelectedNode == null) return;

            ScopeNode? targetParent = null;

            // Eğer Kat seçiliyse, ona çocuk ekle
            // Eğer Daire seçiliyse, onun ebeveynine (Kat) kardeş ekle
            if (SelectedNode.Type == NodeType.Floor)
            {
                targetParent = SelectedNode;
            }
            else if (SelectedNode.Type == NodeType.Flat && SelectedNode.Parent != null)
            {
                targetParent = SelectedNode.Parent;
            }

            // IMPORTANT: Ensure the new node's 'Parent' property is set correctly!
            // Note: targetParent.AddChild() automatically sets the Parent property of the new child.
            if (targetParent != null)
            {
                var flatCount = targetParent.Children.Count(c => c.Type == NodeType.Flat);
                var flat = targetParent.AddChild($"Daire {flatCount + 1}", NodeType.Flat);
                
                // Yeni daireyi seç
                SelectedNode = flat;
                NotifyFinancialsChanged();
            }
        }

        private void AddZone()
        {
            if (SelectedNode == null) return;

            var zoneCount = SelectedNode.Children.Count(c => c.Type == NodeType.Zone);
            var zone = SelectedNode.AddChild($"Bölge {zoneCount + 1}", NodeType.Zone);

            SelectedNode = zone;
            NotifyFinancialsChanged();
        }

        private void DuplicateNode()
        {
            if (SelectedNode == null || SelectedNode.Parent == null) return;

            var clone = SelectedNode.Clone($"{SelectedNode.Name} (Kopya)");
            clone.Parent = SelectedNode.Parent;
            SelectedNode.Parent.Children.Add(clone);

            SelectedNode = clone;
            NotifyFinancialsChanged();
        }

        private void RenameNode()
        {
            if (SelectedNode == null) return;

            // Basit input dialog (Microsoft.VisualBasic.Interaction.InputBox)
            var newName = Microsoft.VisualBasic.Interaction.InputBox(
                "Yeni isim veya müşteri bilgisi (Örn: 'Daire 5 - Ahmet Bey'):", 
                "Birim Adı / Müşteri Bilgisi", 
                SelectedNode.Name);

            if (!string.IsNullOrWhiteSpace(newName))
            {
                SelectedNode.Name = newName;
            }
        }

        private void RemoveNode()
        {
            if (SelectedNode == null || SelectedNode.Type == NodeType.Project) return;

            var result = MessageBox.Show(
                $"'{SelectedNode.Name}' ve tüm alt öğeleri silinecek. Devam edilsin mi?",
                "Node Sil",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            var parent = SelectedNode.Parent;
            if (parent != null)
            {
                parent.Children.Remove(SelectedNode);
                parent.NotifyTotalsChanged(); // Update parent's badge/totals
                SelectedNode = parent;
            }
            else
            {
                RootNodes.Remove(SelectedNode);
                SelectedNode = RootNodes.FirstOrDefault();
            }

            NotifyFinancialsChanged();
        }

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

            foreach (var sibling in siblings)
            {
                sibling.Items.Clear();
                SelectedNode.CopyItemsTo(sibling);
            }

            NotifyFinancialsChanged();
            MessageBox.Show($"{siblings.Count} node güncellendi.", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        #endregion

        #region Kalem Yönetimi

        private void RefreshCurrentNodeItems()
        {
            CurrentNodeItems.Clear();
            if (SelectedNode == null) return;

            foreach (var item in SelectedNode.Items)
            {
                item.OnItemChanged += () =>
                {
                    SelectedNode.NotifyTotalsChanged();
                    NotifyFinancialsChanged();
                };
                CurrentNodeItems.Add(item);
            }
        }

        private void AddItemToNode()
        {
            if (SelectedNode == null || SelectedProduct == null) return;

            var item = ScopeNodeItem.FromProduct(SelectedProduct);
            item.OnItemChanged += () =>
            {
                SelectedNode.NotifyTotalsChanged();
                NotifyFinancialsChanged();
            };

            SelectedNode.Items.Add(item);
            CurrentNodeItems.Add(item);
            SelectedNode.NotifyTotalsChanged();
            NotifyFinancialsChanged();
        }

        private void RemoveItemFromNode()
        {
            if (SelectedNode == null || SelectedItem == null) return;

            SelectedNode.Items.Remove(SelectedItem);
            CurrentNodeItems.Remove(SelectedItem);
            SelectedNode.NotifyTotalsChanged();
            NotifyFinancialsChanged();
        }

        #endregion

        #region Drag & Drop (IDropTarget)

        void IDropTarget.DragOver(IDropInfo dropInfo)
        {
            // Ürün katalogdan sürükleme
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
                var item = ScopeNodeItem.FromProduct(product);
                item.OnItemChanged += () =>
                {
                    SelectedNode.NotifyTotalsChanged();
                    NotifyFinancialsChanged();
                };

                SelectedNode.Items.Add(item);
                CurrentNodeItems.Add(item);
                SelectedNode.NotifyTotalsChanged();
                NotifyFinancialsChanged();
            }
        }

        #endregion

        #region Finansal Güncellemeler

        private void NotifyFinancialsChanged()
        {
            OnPropertyChanged(nameof(TotalRevenue));
            OnPropertyChanged(nameof(TotalCost));
            OnPropertyChanged(nameof(TotalProfit));
            OnPropertyChanged(nameof(OverallMargin));
            OnPropertyChanged(nameof(TotalRevenueDisplay));
            OnPropertyChanged(nameof(TotalCostDisplay));
            OnPropertyChanged(nameof(TotalProfitDisplay));
            OnPropertyChanged(nameof(OverallMarginDisplay));
            OnPropertyChanged(nameof(ProfitColor));
            OnPropertyChanged(nameof(SelectedNodeSubTotal));
        }

        #endregion

        #region Kaydetme

        private bool CanSave()
        {
            return SelectedCustomer != null
                && !string.IsNullOrWhiteSpace(ProjectName)
                && RootNodes.Any();
        }

        private void Save()
        {
            try
            {
                CurrentProject.Title = ProjectName;
                CurrentProject.CustomerId = SelectedCustomer!.Id;

                _scopeService.SaveProject(CurrentProject, RootNodes.ToList());

                MessageBox.Show(
                    $"Proje başarıyla kaydedildi!\n\n" +
                    $"Proje Kodu: {CurrentProject.ProjectCode}\n" +
                    $"Toplam: {TotalRevenueDisplay}\n" +
                    $"Kar: {TotalProfitDisplay} ({OverallMarginDisplay})",
                    "Başarılı",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Kayıt sırasında hata oluştu: {ex.Message}",
                    "Hata",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void ExportPdf()
        {
            if (RootNodes == null || !RootNodes.Any())
            {
                MessageBox.Show("Dışa aktarılacak veri yok.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                FileName = $"Teklif_{ProjectName}_{DateTime.Now:yyyyMMdd}",
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
                    
                // Geçici proje nesnesi oluştur (Eğer henüz kaydedilmediyse UI'dan verileri al)
                var exportProject = CurrentProject;
                exportProject.Title = ProjectName;
                if(SelectedCustomer != null) exportProject.Customer = SelectedCustomer;

                pdfService.GenerateProjectQuote(exportProject, RootNodes.ToList(), filePath);

                if (openAfter)
                {
                    var result = MessageBox.Show(
                        "PDF başarıyla oluşturuldu. Dosyayı açmak ister misiniz?", 
                        "Başarılı", 
                        MessageBoxButton.YesNo, 
                        MessageBoxImage.Question);

                     if (result == MessageBoxResult.Yes)
                     {
                         new System.Diagnostics.Process
                         {
                             StartInfo = new System.Diagnostics.ProcessStartInfo(filePath)
                             {
                                 UseShellExecute = true
                             }
                         }.Start();
                     }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"PDF oluşturulurken hata: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public ICommand SendEmailCommand { get; }

        private async void SendEmail()
        {
             if (SelectedCustomer == null || string.IsNullOrWhiteSpace(SelectedCustomer.Email))
             {
                 MessageBox.Show("Müşterinin e-posta adresi kayıtlı değil.", "Hata", MessageBoxButton.OK, MessageBoxImage.Warning);
                 return;
             }

             if (RootNodes == null || !RootNodes.Any())
             {
                 MessageBox.Show("Gönderilecek veri yok.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                 return;
             }

             var result = MessageBox.Show(
                 $"{SelectedCustomer.Email} adresine teklif gönderilecek. Onaylıyor musunuz?",
                 "E-Posta Gönder",
                 MessageBoxButton.YesNo,
                 MessageBoxImage.Question);

             if (result != MessageBoxResult.Yes) return;

             try
             {
                 // Geçici dosya oluştur
                 string tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"Teklif_{ProjectName}_{DateTime.Now:yyyyMMdd}.pdf");
                 
                 // PDF Oluştur
                 GeneratePdf(tempPath, false);

                 // Gönder
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
        }

        private void CloseWindow(object? parameter)
        {
            if (parameter is Window window)
                window.Close();
        }

        #endregion
    }
}
