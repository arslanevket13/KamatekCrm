using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using KamatekCrm.Commands;
using KamatekCrm.Shared.Models;

namespace KamatekCrm.ViewModels
{
    public class PdfImportPreviewViewModel : ViewModelBase
    {
        public ObservableCollection<PurchaseOrderItem> ParsedItems { get; }

        public ICommand ConfirmCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand RemoveItemCommand { get; }

        public bool IsConfirmed { get; private set; }

        public PdfImportPreviewViewModel(System.Collections.Generic.List<PurchaseOrderItem> items)
        {
            ParsedItems = new ObservableCollection<PurchaseOrderItem>(items);

            ConfirmCommand = new RelayCommand(ExecuteConfirm, _ => ParsedItems.Count > 0);
            CancelCommand = new RelayCommand(ExecuteCancel);
            RemoveItemCommand = new RelayCommand(ExecuteRemoveItem);
        }

        private void ExecuteConfirm(object? parameter)
        {
            IsConfirmed = true;
            CloseWindow(parameter as Window);
        }

        private void ExecuteCancel(object? parameter)
        {
            IsConfirmed = false;
            CloseWindow(parameter as Window);
        }

        private void ExecuteRemoveItem(object? parameter)
        {
            if (parameter is PurchaseOrderItem item)
            {
                ParsedItems.Remove(item);
            }
        }

        private void CloseWindow(Window? window)
        {
            window?.Close();
        }
    }
}
