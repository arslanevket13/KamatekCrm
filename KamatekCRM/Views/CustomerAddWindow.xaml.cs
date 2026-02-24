using System.Windows;

namespace KamatekCrm.Views
{
    public partial class CustomerAddWindow : Window
    {
        public CustomerAddWindow()
        {
            InitializeComponent();
            var vm = new KamatekCrm.ViewModels.CustomerAddViewModel();
            vm.RequestClose += success =>
            {
                DialogResult = success;
                Close();
            };
            DataContext = vm;
        }

        public CustomerAddWindow(ViewModels.CustomerAddViewModel viewModel)
        {
            InitializeComponent();
            viewModel.RequestClose += success =>
            {
                DialogResult = success;
                Close();
            };
            DataContext = viewModel;
        }
    }
}
