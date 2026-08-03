using System.Windows.Controls;
using KamatekCrm.ViewModels;

namespace KamatekCrm.Views
{
    public partial class ManagerAgendaView : UserControl
    {
        public ManagerAgendaView()
        {
            InitializeComponent();
        }

        public ManagerAgendaView(ManagerAgendaViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
