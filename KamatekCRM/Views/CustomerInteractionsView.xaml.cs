using System.Windows.Controls;
using KamatekCrm.ViewModels;

namespace KamatekCrm.Views
{
    public partial class CustomerInteractionsView : UserControl
    {
        public CustomerInteractionsView()
        {
            InitializeComponent();
        }

        public CustomerInteractionsView(CustomerInteractionsViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
