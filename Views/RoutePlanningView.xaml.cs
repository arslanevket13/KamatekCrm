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
        public RoutePlanningView()
        {
            InitializeComponent();
            InitializeAsync();
            this.DataContextChanged += RoutePlanningView_DataContextChanged;
            this.Unloaded += RoutePlanningView_Unloaded;
        }

        async void InitializeAsync()
        {
            try 
            {
                await MapWebView.EnsureCoreWebView2Async(null);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Harita bileşeni yüklenemedi: {ex.Message}\nLütfen 'WebView2 Runtime' yüklü olduğundan emin olun.", "Hata", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void RoutePlanningView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is RoutePlanningViewModel oldVm)
            {
                oldVm.PropertyChanged -= ViewModel_PropertyChanged;
            }

            if (e.NewValue is RoutePlanningViewModel newVm)
            {
                newVm.PropertyChanged += ViewModel_PropertyChanged;
                UpdateMapContent(newVm.MapHtmlContent);
            }
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(RoutePlanningViewModel.MapHtmlContent))
            {
                if (DataContext is RoutePlanningViewModel vm)
                {
                    UpdateMapContent(vm.MapHtmlContent);
                }
            }
        }

        private void UpdateMapContent(string htmlContent)
        {
            if (MapWebView != null && MapWebView.CoreWebView2 != null && !string.IsNullOrEmpty(htmlContent))
            {
                MapWebView.NavigateToString(htmlContent);
            }
        }
        
        private void RoutePlanningView_Unloaded(object sender, RoutedEventArgs e)
        {
             // Cleanup if needed
             if (DataContext is RoutePlanningViewModel vm)
             {
                 vm.PropertyChanged -= ViewModel_PropertyChanged;
             }
        }
    }
}
