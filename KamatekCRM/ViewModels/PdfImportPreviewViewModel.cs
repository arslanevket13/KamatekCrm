using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using KamatekCrm.Shared.Models;

namespace KamatekCrm.ViewModels
{
    public partial class PdfImportPreviewViewModel : ViewModelBase
    {
        public ObservableCollection<PurchaseOrderItem> ParsedItems { get; }

        public bool IsConfirmed { get; private set; }

        public PdfImportPreviewViewModel(System.Collections.Generic.List<PurchaseOrderItem> items)
        {
            ParsedItems = new ObservableCollection<PurchaseOrderItem>(items);
        }

        [RelayCommand]
        private void Confirm(object? parameter)
        {
            IsConfirmed = true;
            Cancel(parameter as Window);
        }

        [RelayCommand]
        private void Cancel(object? parameter)
        {
            IsConfirmed = false;
            Cancel(parameter as Window);
        }

        [RelayCommand]
        private void RemoveItem(object? parameter)
        {
            if (parameter is PurchaseOrderItem item)
            {
                ParsedItems.Remove(item);
            }
        }

        private void Cancel(Window? window)
        {
            window?.Close();
        }
    }
}

