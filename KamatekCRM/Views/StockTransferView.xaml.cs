using System.Windows;
using KamatekCrm.ViewModels;

namespace KamatekCrm.Views
{
    /// <summary>
    /// Interaction logic for StockTransferView.xaml
    /// </summary>
    public partial class StockTransferView : Window
    {
        public StockTransferView(StockTransferViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            KamatekCrm.Helpers.WindowControlHelper.SetupWindowControls(this);
        }
    }
}
