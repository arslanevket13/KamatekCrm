using System.Windows;
using KamatekCrm.ViewModels;

namespace KamatekCrm.Views
{
    /// <summary>
    /// RepairTrackingWindow.xaml etkileşim mantığı
    /// </summary>
    public partial class RepairTrackingWindow : Window
    {
        public RepairTrackingWindow(RepairViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        public RepairTrackingWindow(RepairViewModel viewModel, int jobId) : this(viewModel)
        {
            viewModel.SelectJobById(jobId);
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
