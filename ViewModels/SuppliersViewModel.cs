using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using KamatekCrm.Commands;
using KamatekCrm.Data;
using KamatekCrm.Models;
using KamatekCrm.Services;
using Microsoft.EntityFrameworkCore;

namespace KamatekCrm.ViewModels
{
    public class SuppliersViewModel : ViewModelBase
    {
        private readonly AppDbContext _context;

        #region Properties

        public ObservableCollection<Supplier> Suppliers { get; } = new();
        
        // Filtrelenmiş liste
        public ObservableCollection<Supplier> FilteredSuppliers { get; } = new();

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    ApplyFilter();
                }
            }
        }

        private Supplier? _selectedSupplier;
        public Supplier? SelectedSupplier
        {
            get => _selectedSupplier;
            set
            {
                if (SetProperty(ref _selectedSupplier, value))
                {
                    if (value != null)
                    {
                        IsEditing = true;
                        LoadPurchaseHistory();
                    }
                    else
                    {
                        IsEditing = false;
                        SupplierPurchaseHistory.Clear();
                    }
                    OnPropertyChanged(nameof(SupplierPurchaseHistory));
                }
            }
        }

        // Seçili tedarikçinin sipariş geçmişi
        public ObservableCollection<PurchaseOrder> SupplierPurchaseHistory { get; } = new();

        private bool _isEditing;
        public bool IsEditing
        {
            get => _isEditing;
            set => SetProperty(ref _isEditing, value);
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        #endregion

        #region Commands

        public ICommand SaveCommand { get; }
        public ICommand CreateNewCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand ClearSearchCommand { get; }

        #endregion

        public SuppliersViewModel()
        {
            _context = new AppDbContext();

            SaveCommand = new RelayCommand(_ => SaveChanges(), _ => CanSave());
            CreateNewCommand = new RelayCommand(_ => CreateNew());
            DeleteCommand = new RelayCommand(DeleteSupplier, CanDelete);
            RefreshCommand = new RelayCommand(_ => LoadData());
            ClearSearchCommand = new RelayCommand(_ => SearchText = string.Empty);

            LoadData();
        }

        private void ApplyFilter()
        {
            FilteredSuppliers.Clear();
            
            var filtered = string.IsNullOrWhiteSpace(SearchText)
                ? Suppliers
                : Suppliers.Where(s => 
                    (s.CompanyName?.ToLowerInvariant().Contains(SearchText.ToLowerInvariant()) ?? false) ||
                    (s.ContactPerson?.ToLowerInvariant().Contains(SearchText.ToLowerInvariant()) ?? false) ||
                    (s.PhoneNumber?.Contains(SearchText) ?? false) ||
                    (s.Email?.ToLowerInvariant().Contains(SearchText.ToLowerInvariant()) ?? false));

            foreach (var supplier in filtered)
            {
                FilteredSuppliers.Add(supplier);
            }
        }

        private void LoadData()
        {
            IsBusy = true;
            try
            {
                Suppliers.Clear();
                FilteredSuppliers.Clear();
                
                // Sadece aktif tedarikçileri yükle
                var list = _context.Suppliers
                    .Where(s => s.IsActive)
                    .OrderBy(s => s.CompanyName)
                    .ToList();
                    
                foreach (var item in list)
                {
                    Suppliers.Add(item);
                    FilteredSuppliers.Add(item);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Veri yükleme hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }
        
        private void LoadPurchaseHistory()
        {
            SupplierPurchaseHistory.Clear();
            if (SelectedSupplier == null) return;
             
            try
            {
                var orders = _context.PurchaseOrders
                    .Where(p => p.SupplierName == SelectedSupplier.CompanyName)
                    .OrderByDescending(p => p.OrderDate)
                    .Take(50)
                    .ToList();
                    
                foreach(var po in orders) 
                    SupplierPurchaseHistory.Add(po);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Sipariş geçmişi yüklenemedi: {ex.Message}");
            }
        }

        private void CreateNew()
        {
            var newSupplier = new Supplier
            {
                CompanyName = "Yeni Tedarikçi",
                IsActive = true,
                Balance = 0
            };
            
            _context.Suppliers.Add(newSupplier);
            Suppliers.Add(newSupplier);
            FilteredSuppliers.Add(newSupplier);
            SelectedSupplier = newSupplier;
            IsEditing = true;
        }

        private bool CanSave()
        {
            return SelectedSupplier != null && !string.IsNullOrWhiteSpace(SelectedSupplier.CompanyName);
        }

        private void SaveChanges()
        {
            try
            {
                if (SelectedSupplier == null) return;

                _context.SaveChanges();
                
                // Liste görünümünü güncelle
                ApplyFilter();
                
                MessageBox.Show("Tedarikçi bilgileri başarıyla kaydedildi.", "Başarılı", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (DbUpdateException dbEx)
            {
                MessageBox.Show($"Veritabanı kayıt hatası: {dbEx.InnerException?.Message ?? dbEx.Message}", 
                    "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Kayıt hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool CanDelete(object? parameter)
        {
            return SelectedSupplier != null && AuthService.CanDeleteRecords;
        }

        private void DeleteSupplier(object? parameter)
        {
            if (SelectedSupplier == null) return;

            var result = MessageBox.Show(
                $"'{SelectedSupplier.CompanyName}' tedarikçisini silmek istediğinize emin misiniz?\n\nBu işlem geri alınamaz.", 
                "Silme Onayı", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                
            if (result != MessageBoxResult.Yes) return;

            try
            {
                // Soft delete - IsActive = false
                SelectedSupplier.IsActive = false;
                _context.SaveChanges();
                
                // Listeden kaldır
                Suppliers.Remove(SelectedSupplier);
                FilteredSuppliers.Remove(SelectedSupplier);
                SelectedSupplier = null;
                
                MessageBox.Show("Tedarikçi başarıyla silindi (pasife alındı).", "Bilgi", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Silme hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
