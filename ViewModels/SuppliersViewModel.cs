using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using KamatekCrm.Commands;
using KamatekCrm.Enums;
using KamatekCrm.Models;
using KamatekCrm.Repositories;
using Microsoft.EntityFrameworkCore;

namespace KamatekCrm.ViewModels
{
    public class SuppliersViewModel : ViewModelBase
    {
        private readonly IUnitOfWork _unitOfWork;

        #region Properties

        public ObservableCollection<Supplier> Suppliers { get; } = new();
        public ObservableCollection<Supplier> FilteredSuppliers { get; } = new();

        private Supplier? _selectedSupplier;
        public Supplier? SelectedSupplier
        {
            get => _selectedSupplier;
            set
            {
                if (SetProperty(ref _selectedSupplier, value))
                {
                    IsEditing = value != null;
                    if (value != null)
                    {
                        // Load related data if needed
                    }
                }
            }
        }

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

        #endregion

        #region Commands

        public ICommand LoadDataCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand CreateNewCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand ClearSearchCommand { get; }

        #endregion

        public SuppliersViewModel(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));

            // Initialize Commands
            LoadDataCommand = new RelayCommand(async _ => await LoadDataAsync());
            SaveCommand = new RelayCommand(async _ => await SaveAsync(), _ => CanSave());
            CreateNewCommand = new RelayCommand(_ => CreateNew());
            DeleteCommand = new RelayCommand(async _ => await DeleteAsync(), _ => CanDelete());
            RefreshCommand = new RelayCommand(async _ => await LoadDataAsync());
            ClearSearchCommand = new RelayCommand(_ => SearchText = string.Empty);

            // Initial Load
            LoadDataCommand.Execute(null);
        }

        // Default constructor for design-time
        public SuppliersViewModel() : this(new UnitOfWork()) { }

        private async Task LoadDataAsync()
        {
            if (IsBusy) return;
            IsBusy = true;

            try
            {
                Suppliers.Clear();
                var data = await _unitOfWork.Context.Suppliers
                    .Where(s => s.IsActive)
                    .OrderBy(s => s.CompanyName)
                    .ToListAsync();

                foreach (var item in data)
                {
                    Suppliers.Add(item);
                }

                ApplyFilter();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Veri yüklenirken hata oluştu: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void ApplyFilter()
        {
            FilteredSuppliers.Clear();
            var query = Suppliers.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var lowerSearch = SearchText.ToLowerInvariant();
                query = query.Where(s => 
                    (s.CompanyName?.ToLowerInvariant().Contains(lowerSearch) == true) ||
                    (s.ContactPerson?.ToLowerInvariant().Contains(lowerSearch) == true) ||
                    (s.PhoneNumber?.Contains(SearchText) == true));
            }

            foreach (var item in query)
            {
                FilteredSuppliers.Add(item);
            }
        }

        private void CreateNew()
        {
            SelectedSupplier = new Supplier
            {
                CompanyName = "Yeni Tedarikçi",
                IsActive = true,
                SupplierType = SupplierType.Wholesaler // Default
            };
            Suppliers.Add(SelectedSupplier);
            FilteredSuppliers.Add(SelectedSupplier);
            IsEditing = true;
        }

        private bool CanSave() => SelectedSupplier != null && !string.IsNullOrWhiteSpace(SelectedSupplier.CompanyName);

        private async Task SaveAsync()
        {
            if (SelectedSupplier == null) return;

            try
            {
                if (SelectedSupplier.Id == 0)
                {
                    if (!_unitOfWork.Context.Suppliers.Local.Contains(SelectedSupplier))
                    {
                        _unitOfWork.Context.Suppliers.Add(SelectedSupplier);
                    }
                }

                await _unitOfWork.SaveChangesAsync();
                MessageBox.Show("Tedarikçi kaydedildi.", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Kaydetme hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool CanDelete() => SelectedSupplier != null && SelectedSupplier.Id > 0;

        private async Task DeleteAsync()
        {
            if (SelectedSupplier == null) return;

            if (MessageBox.Show("Bu tedarikçiyi silmek istediğinize emin misiniz?", "Onay", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            try
            {
                SelectedSupplier.IsActive = false; // Soft Delete
                await _unitOfWork.SaveChangesAsync();
                Suppliers.Remove(SelectedSupplier);
                FilteredSuppliers.Remove(SelectedSupplier);
                SelectedSupplier = null;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Silme hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
