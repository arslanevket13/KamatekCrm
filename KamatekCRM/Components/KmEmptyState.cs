using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace KamatekCrm.Components
{
    /// <summary>
    /// Beautiful empty state component shown when a list or view has no data.
    /// Displays an icon, title, message, and optional action button.
    /// Usage: <km:KmEmptyState Icon="📦" Title="Henüz ürün yok" Message="İlk ürününüzü ekleyin" ActionText="Ürün Ekle" ActionCommand="{Binding AddProduct}"/>
    /// </summary>
    public class KmEmptyState : Control
    {
        static KmEmptyState()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(KmEmptyState),
                new FrameworkPropertyMetadata(typeof(KmEmptyState)));
        }

        public static readonly DependencyProperty IconProperty =
            DependencyProperty.Register(nameof(Icon), typeof(string), typeof(KmEmptyState),
                new PropertyMetadata("📋"));

        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(Title), typeof(string), typeof(KmEmptyState),
                new PropertyMetadata("Veri bulunamadı"));

        public static readonly DependencyProperty MessageProperty =
            DependencyProperty.Register(nameof(Message), typeof(string), typeof(KmEmptyState),
                new PropertyMetadata("Henüz kayıt bulunmuyor. Yeni bir kayıt ekleyerek başlayın."));

        public static readonly DependencyProperty ActionTextProperty =
            DependencyProperty.Register(nameof(ActionText), typeof(string), typeof(KmEmptyState),
                new PropertyMetadata("", OnActionChanged));

        public static readonly DependencyProperty ActionCommandProperty =
            DependencyProperty.Register(nameof(ActionCommand), typeof(ICommand), typeof(KmEmptyState),
                new PropertyMetadata(null, OnActionChanged));

        public static readonly DependencyProperty ActionVisibilityProperty =
            DependencyProperty.Register(nameof(ActionVisibility), typeof(Visibility), typeof(KmEmptyState),
                new PropertyMetadata(Visibility.Collapsed));

        public string Icon
        {
            get => (string)GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public string Message
        {
            get => (string)GetValue(MessageProperty);
            set => SetValue(MessageProperty, value);
        }

        public string ActionText
        {
            get => (string)GetValue(ActionTextProperty);
            set => SetValue(ActionTextProperty, value);
        }

        public ICommand ActionCommand
        {
            get => (ICommand)GetValue(ActionCommandProperty);
            set => SetValue(ActionCommandProperty, value);
        }

        public Visibility ActionVisibility
        {
            get => (Visibility)GetValue(ActionVisibilityProperty);
            set => SetValue(ActionVisibilityProperty, value);
        }

        private static void OnActionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is KmEmptyState comp)
            {
                comp.ActionVisibility = !string.IsNullOrEmpty(comp.ActionText) && comp.ActionCommand != null
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }
    }
}
