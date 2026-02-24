using System.Windows;
using KamatekCrm.Shared.Models;
using KamatekCrm.ViewModels;

namespace KamatekCrm.Views
{
    /// <summary>
    /// EditUserView.xaml code-behind
    /// </summary>
    public partial class EditUserView : Window
    {
        public EditUserView(User user)
        {
            InitializeComponent();

            var viewModel = new EditUserViewModel(user);
            viewModel.SaveSuccessful += () =>
            {
                DialogResult = true;
                Close();
            };
            
            viewModel.CancelRequested += () =>
            {
                DialogResult = false;
                Close();
            };
            
            DataContext = viewModel;
        }
    }
}
