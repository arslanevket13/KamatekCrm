using System.Windows;
using KamatekCrm.Models;
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
            DataContext = viewModel;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
