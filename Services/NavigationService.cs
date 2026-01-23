using System;
using System.ComponentModel;

namespace KamatekCrm.Services
{
    /// <summary>
    /// Uygulama içi view geçişlerini yöneten servis (Singleton)
    /// </summary>
    public class NavigationService : INotifyPropertyChanged
    {
        private static NavigationService? _instance;
        private static readonly object _lock = new object();

        private object? _currentView;

        /// <summary>
        /// Singleton instance
        /// </summary>
        public static NavigationService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new NavigationService();
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// Mevcut görünüm
        /// </summary>
        public object? CurrentView
        {
            get => _currentView;
            set
            {
                if (_currentView != value)
                {
                    _currentView = value;
                    OnPropertyChanged(nameof(CurrentView));
                }
            }
        }

        /// <summary>
        /// Belirtilen ViewModel'e geçiş yap
        /// </summary>
        public void NavigateTo(object viewModel)
        {
            CurrentView = viewModel;
        }

        /// <summary>
        /// Login ekranına git
        /// </summary>
        public void NavigateToLogin()
        {
            CurrentView = new ViewModels.LoginViewModel();
        }

        /// <summary>
        /// Ana içerik ekranına git
        /// </summary>
        public void NavigateToMainContent()
        {
            CurrentView = new ViewModels.MainContentViewModel();
        }

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}
