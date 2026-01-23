using System.Windows;
using KamatekCrm.ViewModels;

namespace KamatekCrm.Views
{
    /// <summary>
    /// AddUserView.xaml code-behind
    /// </summary>
    public partial class AddUserView : Window
    {
        public AddUserView()
        {
            InitializeComponent();

            // ViewModel event'ini dinle
            if (DataContext is AddUserViewModel viewModel)
            {
                viewModel.FormCleared += OnFormCleared;
            }

            // İlk yüklemede Ad alanına odaklan
            Loaded += (s, e) => AdTextBox.Focus();
        }

        /// <summary>
        /// Form temizlendiğinde Ad alanına odaklan
        /// </summary>
        private void OnFormCleared()
        {
            AdTextBox.Focus();
            AdTextBox.SelectAll();
        }
    }
}
