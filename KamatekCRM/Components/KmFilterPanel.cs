using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;

namespace KamatekCrm.Components
{
    /// <summary>
    /// Multi-filter panel with date range, category dropdown, status selector, and clear all.
    /// Usage: <km:KmFilterPanel DateFrom="{Binding DateFrom}" DateTo="{Binding DateTo}" 
    ///         StatusItems="{Binding StatusFilters}" ApplyCommand="{Binding ApplyFilters}"/>
    /// </summary>
    public class KmFilterPanel : Control
    {
        static KmFilterPanel()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(KmFilterPanel),
                new FrameworkPropertyMetadata(typeof(KmFilterPanel)));
        }

        public KmFilterPanel()
        {
            ClearAllCommand = new RelayCommand(ExecuteClearAll);
        }

        #region Dependency Properties

        public static readonly DependencyProperty DateFromProperty =
            DependencyProperty.Register(nameof(DateFrom), typeof(DateTime?), typeof(KmFilterPanel),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public static readonly DependencyProperty DateToProperty =
            DependencyProperty.Register(nameof(DateTo), typeof(DateTime?), typeof(KmFilterPanel),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public static readonly DependencyProperty SelectedStatusProperty =
            DependencyProperty.Register(nameof(SelectedStatus), typeof(string), typeof(KmFilterPanel),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public static readonly DependencyProperty StatusItemsProperty =
            DependencyProperty.Register(nameof(StatusItems), typeof(ObservableCollection<string>), typeof(KmFilterPanel),
                new PropertyMetadata(new ObservableCollection<string>()));

        public static readonly DependencyProperty SelectedCategoryProperty =
            DependencyProperty.Register(nameof(SelectedCategory), typeof(string), typeof(KmFilterPanel),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public static readonly DependencyProperty CategoryItemsProperty =
            DependencyProperty.Register(nameof(CategoryItems), typeof(ObservableCollection<string>), typeof(KmFilterPanel),
                new PropertyMetadata(new ObservableCollection<string>()));

        public static readonly DependencyProperty ApplyCommandProperty =
            DependencyProperty.Register(nameof(ApplyCommand), typeof(ICommand), typeof(KmFilterPanel));

        public static readonly DependencyProperty ClearAllCommandProperty =
            DependencyProperty.Register(nameof(ClearAllCommand), typeof(ICommand), typeof(KmFilterPanel));

        public static readonly DependencyProperty HasActiveFiltersProperty =
            DependencyProperty.Register(nameof(HasActiveFilters), typeof(bool), typeof(KmFilterPanel),
                new PropertyMetadata(false));

        public static readonly DependencyProperty ActiveFilterCountProperty =
            DependencyProperty.Register(nameof(ActiveFilterCount), typeof(int), typeof(KmFilterPanel),
                new PropertyMetadata(0));

        #endregion

        #region Properties

        public DateTime? DateFrom
        {
            get => (DateTime?)GetValue(DateFromProperty);
            set => SetValue(DateFromProperty, value);
        }

        public DateTime? DateTo
        {
            get => (DateTime?)GetValue(DateToProperty);
            set => SetValue(DateToProperty, value);
        }

        public string SelectedStatus
        {
            get => (string)GetValue(SelectedStatusProperty);
            set => SetValue(SelectedStatusProperty, value);
        }

        public ObservableCollection<string> StatusItems
        {
            get => (ObservableCollection<string>)GetValue(StatusItemsProperty);
            set => SetValue(StatusItemsProperty, value);
        }

        public string SelectedCategory
        {
            get => (string)GetValue(SelectedCategoryProperty);
            set => SetValue(SelectedCategoryProperty, value);
        }

        public ObservableCollection<string> CategoryItems
        {
            get => (ObservableCollection<string>)GetValue(CategoryItemsProperty);
            set => SetValue(CategoryItemsProperty, value);
        }

        public ICommand ApplyCommand
        {
            get => (ICommand)GetValue(ApplyCommandProperty);
            set => SetValue(ApplyCommandProperty, value);
        }

        public ICommand ClearAllCommand
        {
            get => (ICommand)GetValue(ClearAllCommandProperty);
            set => SetValue(ClearAllCommandProperty, value);
        }

        public bool HasActiveFilters
        {
            get => (bool)GetValue(HasActiveFiltersProperty);
            set => SetValue(HasActiveFiltersProperty, value);
        }

        public int ActiveFilterCount
        {
            get => (int)GetValue(ActiveFilterCountProperty);
            set => SetValue(ActiveFilterCountProperty, value);
        }

        #endregion

        private void ExecuteClearAll()
        {
            DateFrom = null;
            DateTo = null;
            SelectedStatus = string.Empty;
            SelectedCategory = string.Empty;
            HasActiveFilters = false;
            ActiveFilterCount = 0;
            ApplyCommand?.Execute(null);
        }

        public void UpdateFilterState()
        {
            var count = 0;
            if (DateFrom.HasValue) count++;
            if (DateTo.HasValue) count++;
            if (!string.IsNullOrEmpty(SelectedStatus)) count++;
            if (!string.IsNullOrEmpty(SelectedCategory)) count++;

            ActiveFilterCount = count;
            HasActiveFilters = count > 0;
        }
    }
}
