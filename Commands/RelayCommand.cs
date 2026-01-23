using System;
using System.Windows.Input;

namespace KamatekCrm.Commands
{
    /// <summary>
    /// ICommand implementasyonu - MVVM pattern için komut sınıfı
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Func<object?, bool>? _canExecute;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="execute">Çalıştırılacak action</param>
        /// <param name="canExecute">Komutun çalıştırılıp çalıştırılamayacağını belirleyen fonksiyon</param>
        public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        /// <summary>
        /// CanExecute değiştiğinde tetiklenir
        /// </summary>
        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        /// <summary>
        /// Komutun çalıştırılıp çalıştırılamayacağını kontrol eder
        /// </summary>
        public bool CanExecute(object? parameter)
        {
            return _canExecute == null || _canExecute(parameter);
        }

        /// <summary>
        /// Komutu çalıştırır
        /// </summary>
        public void Execute(object? parameter)
        {
            _execute(parameter);
        }
    }

    /// <summary>
    /// Generic ICommand implementasyonu
    /// </summary>
    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T?> _execute;
        private readonly Func<T?, bool>? _canExecute;

        /// <summary>
        /// Constructor
        /// </summary>
        public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        /// <summary>
        /// CanExecute değiştiğinde tetiklenir
        /// </summary>
        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        /// <summary>
        /// Komutun çalıştırılıp çalıştırılamayacağını kontrol eder
        /// </summary>
        public bool CanExecute(object? parameter)
        {
            return _canExecute == null || _canExecute((T?)parameter);
        }

        /// <summary>
        /// Komutu çalıştırır
        /// </summary>
        public void Execute(object? parameter)
        {
            _execute((T?)parameter);
        }
    }
}
