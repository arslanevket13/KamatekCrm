using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace KamatekCrm.Views
{
    /// <summary>
    /// QuickAddModal - Universal action modal (Ctrl+K)
    /// </summary>
    public partial class QuickAddModal : Window
    {
        /// <summary>
        /// Event raised when an action is selected
        /// </summary>
        public event Action<string>? ActionSelected;

        public QuickAddModal()
        {
            InitializeComponent();
            Loaded += (s, e) => SearchBox.Focus();
            
            // Click outside to close
            MouseDown += (s, e) =>
            {
                if (e.OriginalSource == this)
                    Close();
            };
        }

        /// <summary>
        /// Handle action item click
        /// </summary>
        private void ActionItem_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is string actionName)
            {
                ActionSelected?.Invoke(actionName);
                Close();
            }
        }

        /// <summary>
        /// Handle keyboard navigation
        /// </summary>
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            
            if (e.Key == Key.Escape)
            {
                Close();
                e.Handled = true;
            }
        }
    }
}
