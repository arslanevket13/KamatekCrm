using System;
using System.Windows.Input;
using System.Windows;
using System.Diagnostics.CodeAnalysis;

namespace KamatekCrm.Commands
{
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool>? _canExecute;

        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Must access instance delegates.")]
        public bool CanExecute(object? parameter) => _canExecute == null || _canExecute();

        [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Must access instance delegates.")]
        public void Execute(object? parameter) => _execute();

        public void RaiseCanExecuteChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }
    }
}