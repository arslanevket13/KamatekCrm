using System.Windows;

namespace KamatekCrm.Views
{
    public partial class QuickNewProductForPurchaseWindow : Window
    {
        public QuickNewProductForPurchaseWindow()
        {
            InitializeComponent();
            var vm = new KamatekCrm.ViewModels.QuickNewProductForPurchaseViewModel();
            vm.RequestClose += success =>
            {
                DialogResult = success;
                Close();
            };
            DataContext = vm;
            Loaded += (_, _) => ProductNameBox.Focus();
        }
    }
}
