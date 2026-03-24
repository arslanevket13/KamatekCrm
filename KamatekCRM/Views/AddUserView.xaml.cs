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

            // ViewModel event'ini dinle - Loaded event'i sonrası DataContext set edilmiş olur
            Loaded += (s, e) => 
            {
                if (DataContext is AddUserViewModel viewModel)
                {
                    viewModel.FormCleared += OnFormCleared;
                }
                AdTextBox.Focus();
            };
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
