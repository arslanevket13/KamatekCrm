using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using KamatekCrm.Shared.Models;
using KamatekCrm.Shared.Repositories;
using KamatekCrm.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace KamatekCrm.ViewModels
{
    public partial class SuppliersViewModel : ViewModelBase
    {
        private readonly IUnitOfWork _unitOfWork;

        public SuppliersViewModel(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;            // Immediate Load
            _ = Refresh();
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
                    _ = Refresh();
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

        private bool CanSaveSupplier() => SelectedSupplier != null;
        private bool CanDeleteSupplier() => SelectedSupplier != null && SelectedSupplier.Id > 0;

        [RelayCommand]
        private void Clear()
        {
            SearchText = string.Empty;
        }

        #region Methods

        [RelayCommand]
        private async Task Refresh()
        {
            if (IsBusy) return;
            IsBusy = true;
            try
            {
                var query = ((UnitOfWork)_unitOfWork).Context.Suppliers.AsQueryable();

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

        [RelayCommand]
        private void AddSupplier()
        {
            SelectedSupplier = new Supplier
            {
                CompanyName = "Yeni Tedarikçi",
                IsActive = true
            };
        }

        [RelayCommand(CanExecute = nameof(CanSaveSupplier))]
        private async Task SaveSupplier()
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
                    ((UnitOfWork)_unitOfWork).Context.Suppliers.Add(SelectedSupplier);
                }
                else
                {
                     // Ensure attached if not tracked (simple approach for this task)
                     if (((UnitOfWork)_unitOfWork).Context.Entry(SelectedSupplier).State == EntityState.Detached)
                     {
                        ((UnitOfWork)_unitOfWork).Context.Suppliers.Attach(SelectedSupplier);
                        ((UnitOfWork)_unitOfWork).Context.Entry(SelectedSupplier).State = EntityState.Modified;
                     }
                }

                await _unitOfWork.SaveChangesAsync();
                await Refresh();
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

        [RelayCommand(CanExecute = nameof(CanDeleteSupplier))]
        private async Task DeleteSupplier()
        {
            if (SelectedSupplier == null) return;

            var res = MessageBox.Show($"'{SelectedSupplier.CompanyName}' silinecek. Onaylıyor musunuz?", "Silme Onayı", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (res == MessageBoxResult.Yes)
            {
                IsBusy = true;
                try
                {
                    ((UnitOfWork)_unitOfWork).Context.Suppliers.Remove(SelectedSupplier);
                    await _unitOfWork.SaveChangesAsync();
                    SelectedSupplier = null;
                    await Refresh();
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
