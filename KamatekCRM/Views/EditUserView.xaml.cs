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
        public EditUserView(User user, Microsoft.EntityFrameworkCore.IDbContextFactory<KamatekCrm.Data.AppDbContext> dbContextFactory, IToastService toastService, ILoadingService loadingService)
        {
            InitializeComponent();
            DataContext = new EditUserViewModel(user, dbContextFactory, toastService, loadingService);
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
