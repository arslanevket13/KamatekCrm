using System.Windows;

namespace KamatekCrm.Views
{
    public partial class QuotationWindow : Window
    {
        public QuotationWindow()
        {
            InitializeComponent();
            DataContext = new KamatekCrm.ViewModels.QuotationViewModel();
        }
    }
}
