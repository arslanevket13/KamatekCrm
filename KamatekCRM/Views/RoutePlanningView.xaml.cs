using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using KamatekCrm.ViewModels;
using Microsoft.Web.WebView2.Core;

namespace KamatekCrm.Views
{
    public partial class RoutePlanningView : UserControl
    {
        private bool _isWebViewReady;

        public RoutePlanningView()
        {
            InitializeComponent();
            InitializeWebViewAsync();
            this.DataContextChanged += OnDataContextChanged;
            this.Unloaded += OnUnloaded;
        }

        private async void InitializeWebViewAsync()
        {
            try
            {
                await MapWebView.EnsureCoreWebView2Async(null);
                _isWebViewReady = true;
                LoadingOverlay.Visibility = Visibility.Collapsed;

                if (MapWebView.CoreWebView2?.Settings != null)
                {
                    MapWebView.CoreWebView2.Settings.AreHostObjectsAllowed = false;
#if !DEBUG
                    MapWebView.CoreWebView2.Settings.AreDevToolsEnabled = false;
                    MapWebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
#endif
                }

                // WebView2 hazır olduğunda mevcut HTML'i yükle
                if (DataContext is RoutePlanningViewModel vm && !string.IsNullOrEmpty(vm.MapHtmlContent))
                {
                    MapWebView.NavigateToString(vm.MapHtmlContent);
                }
            }
            catch (Exception ex)
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
                (App.ServiceProvider?.GetService(typeof(Services.IToastService)) as Services.IToastService)?.ShowWarning(
                    "WebView2 Hatası",
                    $"Harita bileşeni yüklenemedi: {ex.Message}. Lütfen Microsoft Edge WebView2 Runtime yüklü olduğundan emin olun.");
            }
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is RoutePlanningViewModel oldVm)
                oldVm.PropertyChanged -= OnViewModelPropertyChanged;

            if (e.NewValue is RoutePlanningViewModel newVm)
            {
                newVm.PropertyChanged += OnViewModelPropertyChanged;
                if (_isWebViewReady && !string.IsNullOrEmpty(newVm.MapHtmlContent))
                    MapWebView.NavigateToString(newVm.MapHtmlContent);
            }
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(RoutePlanningViewModel.MapHtmlContent) && _isWebViewReady)
            {
                if (DataContext is RoutePlanningViewModel vm && !string.IsNullOrEmpty(vm.MapHtmlContent))
                {
                    Dispatcher.Invoke(() => MapWebView.NavigateToString(vm.MapHtmlContent));
                }
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is RoutePlanningViewModel vm)
                vm.PropertyChanged -= OnViewModelPropertyChanged;
        }
    }
}
