using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KamatekCrm.ViewModels;

namespace KamatekCrm.Views
{
    /// <summary>
    /// QuickAddModal - RBAC filtreli, aranabilir ve klavye destekli komut paleti (Ctrl+K)
    /// </summary>
    public partial class QuickAddModal : Window
    {
        private QuickAddViewModel? _viewModel;

        /// <summary>
        /// Event raised when an action is selected
        /// </summary>
        public event Action<string>? ActionSelected;

        public QuickAddModal()
        {
            InitializeComponent();
            if (Owner == null && Application.Current?.MainWindow != null && Application.Current.MainWindow != this)
            {
                Owner = Application.Current.MainWindow;
            }

            Loaded += (_, _) => SearchBox.Focus();
            PreviewKeyDown += QuickAddModal_PreviewKeyDown;

            // Click outside to close
            MouseDown += (_, e) =>
            {
                if (e.OriginalSource == this)
                    Close();
            };
        }

        public QuickAddModal(QuickAddViewModel viewModel) : this()
        {
            _viewModel = viewModel;
            DataContext = viewModel;

            viewModel.ActionChosen += actionId => ActionSelected?.Invoke(actionId);
            viewModel.CloseRequested += Close;
        }

        private void ActionsListBox_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_viewModel?.SelectedItem != null && _viewModel.ExecuteSelectedCommand.CanExecute(null))
            {
                _viewModel.ExecuteSelectedCommand.Execute(null);
            }
        }

        private void QuickAddModal_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (_viewModel == null)
            {
                if (e.Key == Key.Escape)
                {
                    Close();
                    e.Handled = true;
                }
                return;
            }

            switch (e.Key)
            {
                case Key.Escape:
                    _viewModel.CloseCommand.Execute(null);
                    e.Handled = true;
                    break;
                case Key.Up:
                    _viewModel.MoveSelectionUpCommand.Execute(null);
                    if (_viewModel.SelectedItem != null) ActionsListBox.ScrollIntoView(_viewModel.SelectedItem);
                    e.Handled = true;
                    break;
                case Key.Down:
                    _viewModel.MoveSelectionDownCommand.Execute(null);
                    if (_viewModel.SelectedItem != null) ActionsListBox.ScrollIntoView(_viewModel.SelectedItem);
                    e.Handled = true;
                    break;
                case Key.Enter:
                    if (_viewModel.ExecuteSelectedCommand.CanExecute(null))
                    {
                        _viewModel.ExecuteSelectedCommand.Execute(null);
                    }
                    e.Handled = true;
                    break;
            }
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            KamatekCrm.Helpers.WindowControlHelper.SetupWindowControls(this);
        }
    }
}
