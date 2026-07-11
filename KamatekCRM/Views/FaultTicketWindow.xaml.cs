using System.Windows;
using KamatekCrm.ViewModels;

namespace KamatekCrm.Views
{
    /// <summary>
    /// FaultTicketWindow.xaml etkileşim mantığı
    /// Arıza & Servis Kaydı Penceresi
    /// </summary>
    public partial class FaultTicketWindow : Window
    {
        public FaultTicketWindow(FaultTicketViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;

            viewModel.RequestClose += (result) =>
            {
                DialogResult = result;
                Close();
            };
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

