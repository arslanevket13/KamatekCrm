using System.Windows;

namespace KamatekCrm.Views
{
    public partial class CustomerDetailWindow : Window
    {
        public CustomerDetailWindow()
        {
            InitializeComponent();
        }

        public void Initialize(int customerId)
        {
            if (DetailView.DataContext is ViewModels.CustomerDetailViewModel vm)
            {
                vm.Initialize(customerId);
            }
        }
    }
}
