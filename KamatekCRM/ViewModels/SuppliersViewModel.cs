using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using KamatekCrm.Commands;
using KamatekCrm.Shared.Models;
using KamatekCrm.Repositories;
using Microsoft.EntityFrameworkCore;

namespace KamatekCrm.ViewModels
{
    public class SuppliersViewModel : ViewModelBase
    {
        private readonly IUnitOfWork _unitOfWork;

        public SuppliersViewModel(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
            
            // Commands
            LoadDataCommand = new RelayCommand(async _ => await LoadData());
            SaveCommand = new RelayCommand(async _ => await Save(), _ => SelectedSupplier != null);
            DeleteCommand = new RelayCommand(async _ => await Delete(), _ => SelectedSupplier != null && SelectedSupplier.Id > 0);
            AddNewCommand = new RelayCommand(_ => AddNew());

            // Immediate Load
            _ = LoadData();
        }

        #region Properties

        private ObservableCollection<Supplier> _suppliers = new ObservableCollection<Supplier>();
        public ObservableCollection<Supplier> Suppliers
        {
            get => _suppliers;
            set => SetProperty(ref _suppliers, value);
        }

        private Supplier? _selectedSupplier;
        public Supplier? SelectedSupplier
        {
            get => _selectedSupplier;
            set => SetProperty(ref _selectedSupplier, value); // Requery handled by CommandManager
        }

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    _ = LoadData();
                }
            }
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        #endregion

        #region Commands

        public ICommand LoadDataCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand AddNewCommand { get; }

        #endregion

        #region Methods

        private async Task LoadData()
        {
            if (IsBusy) return;
            IsBusy = true;
            try
            {
                var query = _unitOfWork.Context.Suppliers.AsQueryable();

                if (!string.IsNullOrWhiteSpace(SearchText))
                {
                    string lower = SearchText.ToLower();
                    query = query.Where(x => x.CompanyName.ToLower().Contains(lower) 
                                          || (x.ContactPerson != null && x.ContactPerson.ToLower().Contains(lower)));
                }

                var list = await query.OrderBy(x => x.CompanyName).ToListAsync();
                Suppliers = new ObservableCollection<Supplier>(list);
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

        private void AddNew()
        {
            SelectedSupplier = new Supplier
            {
                CompanyName = "Yeni Tedarikçi",
                IsActive = true
            };
        }

        private async Task Save()
        {
            if (SelectedSupplier == null) return;
            
            // Validation
            if (string.IsNullOrWhiteSpace(SelectedSupplier.CompanyName))
            {
                MessageBox.Show("Firma ünvanı boş olamaz.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IsBusy = true;
            try
            {
                if (SelectedSupplier.Id == 0)
                {
                    _unitOfWork.Context.Suppliers.Add(SelectedSupplier);
                }
                else
                {
                     // Ensure attached if not tracked (simple approach for this task)
                     if (_unitOfWork.Context.Entry(SelectedSupplier).State == EntityState.Detached)
                     {
                        _unitOfWork.Context.Suppliers.Attach(SelectedSupplier);
                        _unitOfWork.Context.Entry(SelectedSupplier).State = EntityState.Modified;
                     }
                }

                await _unitOfWork.SaveChangesAsync();
                await LoadData();
                MessageBox.Show("Kayıt Başarılı.", "Bilgi");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Kaydetme hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task Delete()
        {
            if (SelectedSupplier == null) return;

            var res = MessageBox.Show($"'{SelectedSupplier.CompanyName}' silinecek. Onaylıyor musunuz?", "Silme Onayı", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (res == MessageBoxResult.Yes)
            {
                IsBusy = true;
                try
                {
                    _unitOfWork.Context.Suppliers.Remove(SelectedSupplier);
                    await _unitOfWork.SaveChangesAsync();
                    SelectedSupplier = null;
                    await LoadData();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Silme hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    IsBusy = false;
                }
            }
        }

        #endregion
    }
}
