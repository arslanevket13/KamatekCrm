using System.Windows;

namespace KamatekCrm.Views
{
    /// <summary>
    /// Interaction logic for StockTransferView.xaml
    /// </summary>
    public partial class StockTransferView : Window
    {
        public StockTransferView()
        {
            InitializeComponent();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
