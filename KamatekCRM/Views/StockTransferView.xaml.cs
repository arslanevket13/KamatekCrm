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
            
            if (GetTemplateChild("PART_CloseButton") is System.Windows.Controls.Button closeButton)
            {
                closeButton.Click += (s, e) => this.Close();
            }
        }
    }
}
