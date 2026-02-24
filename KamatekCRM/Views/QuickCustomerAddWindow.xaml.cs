using System.Windows;

namespace KamatekCrm.Views
{
    public partial class QuickCustomerAddWindow : Window
    {
        public QuickCustomerAddWindow()
        {
            InitializeComponent();
            var vm = new KamatekCrm.ViewModels.QuickCustomerAddViewModel();
            vm.RequestClose += success =>
            {
                DialogResult = success;
                Close();
            };
            DataContext = vm;
            Loaded += (_, _) => FullNameBox.Focus();
        }
    }
}
