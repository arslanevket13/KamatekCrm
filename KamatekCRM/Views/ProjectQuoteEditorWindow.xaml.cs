using System;
using System.Windows;
using System.Windows.Controls;
using KamatekCrm.Shared.Models;
using KamatekCrm.ViewModels;

namespace KamatekCrm.Views
{
    /// <summary>
    /// ProjectQuoteEditorWindow.xaml code-behind
    /// </summary>
    public partial class ProjectQuoteEditorWindow : Window
    {
        public ProjectQuoteEditorWindow(ProjectQuoteEditorViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            Closing += OnWindowClosing;
            Closed += (_, _) => (DataContext as IDisposable)?.Dispose();
        }

        private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            // Pencere kapanırken odak bir TextBox üzerindeyse TSF TextStore.GetVisualInfo
            // PresentationSource null olduğu için 0x80040206 (TS_E_NOLAYOUT) fırlatabilir.
            System.Windows.Input.Keyboard.ClearFocus();
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

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            KamatekCrm.Helpers.WindowControlHelper.SetupWindowControls(this);
        }
    }
}
