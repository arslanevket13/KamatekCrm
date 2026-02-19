using System.Windows;
using KamatekCrm.ViewModels;

namespace KamatekCrm.Views
{
    /// <summary>
    /// RepairRegistrationWindow.xaml etkileşim mantığı
    /// </summary>
    public partial class RepairRegistrationWindow : Window
    {
        public RepairRegistrationWindow(RepairViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
