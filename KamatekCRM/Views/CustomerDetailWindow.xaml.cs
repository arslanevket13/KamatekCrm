using System.Windows;

namespace KamatekCrm.Views
{
    public partial class CustomerDetailWindow : Window
    {
        public CustomerDetailWindow()
        {
            InitializeComponent();
            if (Owner == null && Application.Current?.MainWindow != null && Application.Current.MainWindow != this)
            {
                Owner = Application.Current.MainWindow;
            }
        }

        public void Initialize(int customerId)
        {
            if (DetailView.DataContext is ViewModels.CustomerDetailViewModel vm)
            {
                vm.Initialize(customerId);
            }
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            KamatekCrm.Helpers.WindowControlHelper.SetupWindowControls(this);
        }
    }
}
