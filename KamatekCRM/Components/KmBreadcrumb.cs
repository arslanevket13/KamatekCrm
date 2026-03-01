using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace KamatekCrm.Components
{
    /// <summary>
    /// Breadcrumb navigation showing page hierarchy path.
    /// Usage: <km:KmBreadcrumb Items="{Binding BreadcrumbPath}" NavigateCommand="{Binding BreadcrumbNavigate}"/>
    /// BreadcrumbPath can be a list of strings: ["Ana Sayfa", "Müşteriler", "Ahmet Yılmaz"]
    /// </summary>
    public class KmBreadcrumb : Control
    {
        static KmBreadcrumb()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(KmBreadcrumb),
                new FrameworkPropertyMetadata(typeof(KmBreadcrumb)));
        }

        public static readonly DependencyProperty ItemsProperty =
            DependencyProperty.Register(nameof(Items), typeof(ObservableCollection<string>), typeof(KmBreadcrumb),
                new PropertyMetadata(new ObservableCollection<string>()));

        public static readonly DependencyProperty NavigateCommandProperty =
            DependencyProperty.Register(nameof(NavigateCommand), typeof(ICommand), typeof(KmBreadcrumb));

        public ObservableCollection<string> Items
        {
            get => (ObservableCollection<string>)GetValue(ItemsProperty);
            set => SetValue(ItemsProperty, value);
        }

        public ICommand NavigateCommand
        {
            get => (ICommand)GetValue(NavigateCommandProperty);
            set => SetValue(NavigateCommandProperty, value);
        }
    }
}
