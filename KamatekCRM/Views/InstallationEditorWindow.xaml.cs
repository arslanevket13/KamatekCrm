using System.Windows;
using KamatekCrm.ViewModels;

namespace KamatekCrm.Views
{
    public partial class InstallationEditorWindow : Window
    {
        private readonly InstallationEditorViewModel _viewModel;

        public InstallationEditorWindow(InstallationEditorViewModel viewModel)
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
