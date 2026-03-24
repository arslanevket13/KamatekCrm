using System.Windows;
using KamatekCrm.Shared.Models;
using KamatekCrm.ViewModels;
using KamatekCrm.Services;

namespace KamatekCrm.Views
{
    /// <summary>
    /// EditUserView.xaml code-behind
    /// </summary>
    public partial class EditUserView : Window
    {
        public EditUserView()
        {
            InitializeComponent();
        }

        // Keep this for legacy if needed, but parameterless is preferred for our new flow
        public EditUserView(User user, ApiClient apiClient, IToastService toastService, ILoadingService loadingService)
        {
            InitializeComponent();
            DataContext = new EditUserViewModel(user, apiClient, toastService, loadingService);
        }
    }
}
