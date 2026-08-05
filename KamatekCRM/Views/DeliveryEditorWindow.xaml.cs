using System.Windows;
using KamatekCrm.ViewModels;

namespace KamatekCrm.Views
{
    public partial class DeliveryEditorWindow : Window
    {
        private readonly DeliveryEditorViewModel _viewModel;

        public DeliveryEditorWindow(DeliveryEditorViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = viewModel;
            viewModel.OwnerWindow = this;

            viewModel.RequestClose += () =>
            {
                DialogResult = false;
                Close();
            };
            viewModel.RequestCloseWithSuccess += () =>
            {
                DialogResult = true;
                Close();
            };
        }
    }
}
