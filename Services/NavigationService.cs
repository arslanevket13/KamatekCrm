using System;
using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using KamatekCrm.ViewModels;

namespace KamatekCrm.Services
{
    /// <summary>
    /// Uygulama içi view geçişlerini yöneten servis (DI Scoped/Singleton)
    /// </summary>
    public class NavigationService : INotifyPropertyChanged
    {
        private readonly IServiceProvider _serviceProvider;
        private object? _currentView;

        public NavigationService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
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
        /// Belirtilen ViewModel tipine geçiş yap (DI'dan çözerek)
        /// </summary>
        public void NavigateTo<TViewModel>() where TViewModel : notnull
        {
            CurrentView = _serviceProvider.GetRequiredService<TViewModel>();
        }

        /// <summary>
        /// Belirtilen ViewModel instance'ına geçiş yap
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
            NavigateTo<LoginViewModel>();
        }

        /// <summary>
        /// Ana içerik ekranına git
        /// </summary>
        public void NavigateToMainContent()
        {
            // Resolve MainContentViewModel
            CurrentView = _serviceProvider.GetRequiredService<MainContentViewModel>();
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
