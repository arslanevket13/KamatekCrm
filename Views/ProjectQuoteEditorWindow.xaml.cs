using System.Windows;
using System.Windows.Controls;
using KamatekCrm.Models;
using KamatekCrm.ViewModels;

namespace KamatekCrm.Views
{
    /// <summary>
    /// ProjectQuoteEditorWindow.xaml code-behind
    /// </summary>
    public partial class ProjectQuoteEditorWindow : Window
    {
        public ProjectQuoteEditorWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Mevcut projeyi yüklemek için constructor
        /// </summary>
        public ProjectQuoteEditorWindow(int projectId)
        {
            InitializeComponent();
            DataContext = new ProjectQuoteEditorViewModel(projectId);
        }

        /// <summary>
        /// TreeView seçim değişikliği handler
        /// ViewModel'e SelectedNode'u bind etmek için gerekli
        /// </summary>
        private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (DataContext is ProjectQuoteEditorViewModel viewModel && e.NewValue is ScopeNode selectedNode)
            {
                viewModel.SelectedNode = selectedNode;
            }
        }
    }
}
