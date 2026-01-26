using System.Windows;

namespace KamatekCrm.Views
{
    /// <summary>
    /// RepairTrackingWindow.xaml etkileşim mantığı
    /// </summary>
    public partial class RepairTrackingWindow : Window
    {

        public RepairTrackingWindow()
        {
            InitializeComponent();
        }

        public RepairTrackingWindow(int jobId) : this()
        {
            if (DataContext is ViewModels.RepairViewModel vm)
            {
                vm.SelectJobById(jobId);
            }
        }
    }
}
