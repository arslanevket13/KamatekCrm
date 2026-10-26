using System.Windows;

namespace KamatekCrm.Views
{
    public partial class QuotationWindow : Window
    {
        public QuotationWindow(KamatekCrm.ViewModels.QuotationViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            if (Owner == null && Application.Current?.MainWindow != null && Application.Current.MainWindow != this)
            {
                Owner = Application.Current.MainWindow;
            }
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            KamatekCrm.Helpers.WindowControlHelper.SetupWindowControls(this);
        }

        private void CloseWindow_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
