using System.Windows;
using KamatekCrm.ViewModels;

namespace KamatekCrm.Views
{
    /// <summary>
    /// ProjectQuoteWindow.xaml code-behind
    /// </summary>
    public partial class ProjectQuoteWindow : Window
    {
        public ProjectQuoteWindow(ProjectQuoteViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
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
