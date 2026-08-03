using System.Windows;
using KamatekCrm.ViewModels;

namespace KamatekCrm.Views
{
    public partial class QuickInteractionAddWindow : Window
    {
        public QuickInteractionAddWindow()
        {
            InitializeComponent();
        }

        public QuickInteractionAddWindow(QuickInteractionAddViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            Helpers.WindowControlHelper.SetupWindowControls(this);
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
