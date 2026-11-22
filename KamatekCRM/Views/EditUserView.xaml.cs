using System.Windows;
using KamatekCrm.ApplicationCore.Interfaces;
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

        public EditUserView(
            User user,
            IUserAppService userAppService,
            IToastService toastService,
            ILoadingService loadingService,
            IAuthService? authService = null)
        {
            InitializeComponent();
            var viewModel = new EditUserViewModel(userAppService, toastService, loadingService, authService);
            viewModel.SeedFromPreview(user);
            AttachViewModel(viewModel, user.Id);
        }

        private void AttachViewModel(EditUserViewModel viewModel, int userId)
        {
            DataContext = viewModel;
            viewModel.SaveSuccessful += () =>
            {
                DialogResult = true;
                Close();
            };
            viewModel.CancelRequested += () =>
            {
                DialogResult = false;
                Close();
            };
            Loaded += async (_, _) => await viewModel.LoadAsync(userId);
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            KamatekCrm.Helpers.WindowControlHelper.SetupWindowControls(this);
        }
    }
}
