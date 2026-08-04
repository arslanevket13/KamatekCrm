using System.Windows.Controls;
using System.Windows.Input;

namespace KamatekCrm.Views
{
    /// <summary>
    /// Interaction logic for ServiceJobsView.xaml
    /// </summary>
    public partial class ServiceJobsView : UserControl
    {
        public ServiceJobsView()
        {
            InitializeComponent();
        }

        private void DataGridRow_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is DataGridRow row)
            {
                row.IsSelected = true;
                row.Focus();
            }
        }
    }
}
