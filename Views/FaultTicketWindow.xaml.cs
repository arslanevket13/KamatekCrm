using System.Windows;
using KamatekCrm.ViewModels;

namespace KamatekCrm.Views
{
    /// <summary>
    /// FaultTicketWindow.xaml etkileşim mantığı
    /// Arıza & Servis Kaydı Penceresi
    /// </summary>
    public partial class FaultTicketWindow : Window
    {
        public FaultTicketWindow(FaultTicketViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
