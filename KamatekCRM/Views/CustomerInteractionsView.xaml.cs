using System.Windows.Controls;
using System.Windows.Input;
using KamatekCrm.ViewModels;

namespace KamatekCrm.Views
{
    public partial class CustomerInteractionsView : UserControl
    {
        public CustomerInteractionsView()
        {
            InitializeComponent();
            PreviewKeyDown += OnPreviewKeyDown;
        }

        public CustomerInteractionsView(CustomerInteractionsViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            PreviewKeyDown += OnPreviewKeyDown;
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (DataContext is CustomerInteractionsViewModel vm)
            {
                if (e.Key == Key.N && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                {
                    vm.OpenNewDrawer();
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape && vm.IsDrawerOpen)
                {
                    vm.CloseDrawer();
                    e.Handled = true;
                }
            }
        }
    }
}
