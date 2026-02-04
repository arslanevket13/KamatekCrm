using System.Windows;
using KamatekCrm.Services;
using KamatekCrm.ViewModels;

namespace KamatekCrm
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // DataContext olarak NavigationService'i kullan
            DataContext = NavigationService.Instance;
        }
    }
}
