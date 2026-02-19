using System.Windows;
using KamatekCrm.ViewModels;

namespace KamatekCrm.Views
{
    /// <summary>
    /// ProjectQuoteWindow.xaml code-behind
    /// </summary>
    public partial class ProjectQuoteWindow : Window
    {
        public ProjectQuoteWindow(ProjectQuoteViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
