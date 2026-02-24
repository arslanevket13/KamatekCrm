using System.Windows;
using KamatekCrm.Services;
using KamatekCrm.ViewModels;
using Wpf.Ui.Controls;

namespace KamatekCrm
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : FluentWindow
    {
        public MainWindow(NavigationService navigationService, ToastViewModel toastViewModel, LoadingViewModel loadingViewModel)
        {
            InitializeComponent();

            // DataContext olarak NavigationService'i kullan (DI'dan gelen instance)
            DataContext = navigationService;
            
            // Toast ViewModel'i bağla
            ToastControl.DataContext = toastViewModel;
            
            // Loading ViewModel'i bağla
            LoadingControl.DataContext = loadingViewModel;
        }
    }
}
