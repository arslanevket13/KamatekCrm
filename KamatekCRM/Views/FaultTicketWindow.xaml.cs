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
                try { DialogResult = result; } catch (Exception catchEx) { Serilog.Log.Debug(catchEx, "İşlem sırasında beklenen/ikincil hata yakalandı: {Source}", "FaultTicketWindow.xaml.cs"); }
                Close();
            };
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            KamatekCrm.Helpers.WindowControlHelper.SetupWindowControls(this);
        }
    }
}

