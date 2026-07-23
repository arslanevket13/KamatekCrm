using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace KamatekCrm.Components
{
    /// <summary>
    /// KmSplitButton — Ana eylem + dropdown alt eylemler.
    /// Örnek kullanım: "Kaydet" ana butonu + "Kaydet ve Yeni", "Kaydet ve Kapat" alternatifleri.
    /// </summary>
    public class KmSplitButton : Control
    {
        static KmSplitButton()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(KmSplitButton),
                new FrameworkPropertyMetadata(typeof(KmSplitButton)));
        }

        #region Dependency Properties

        public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
            nameof(Text), typeof(string), typeof(KmSplitButton),
            new PropertyMetadata("Kaydet"));

        public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
            nameof(Icon), typeof(string), typeof(KmSplitButton),
            new PropertyMetadata("💾"));

        public static readonly DependencyProperty CommandProperty = DependencyProperty.Register(
            nameof(Command), typeof(ICommand), typeof(KmSplitButton));

        public static readonly DependencyProperty CommandParameterProperty = DependencyProperty.Register(
            nameof(CommandParameter), typeof(object), typeof(KmSplitButton));

        public static readonly DependencyProperty IsDropdownOpenProperty = DependencyProperty.Register(
            nameof(IsDropdownOpen), typeof(bool), typeof(KmSplitButton),
            new PropertyMetadata(false));

        public static readonly DependencyProperty DropdownItemsProperty = DependencyProperty.Register(
            nameof(DropdownItems), typeof(FreezableCollection<SplitButtonItem>), typeof(KmSplitButton),
            new PropertyMetadata(null));

        public static readonly DependencyProperty ButtonStyleTypeProperty = DependencyProperty.Register(
            nameof(ButtonStyleType), typeof(SplitButtonStyle), typeof(KmSplitButton),
            new PropertyMetadata(SplitButtonStyle.Primary));

        #endregion

        #region Properties

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public string Icon
        {
            get => (string)GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }

        public ICommand? Command
        {
            get => (ICommand?)GetValue(CommandProperty);
            set => SetValue(CommandProperty, value);
        }

        public object? CommandParameter
        {
            get => GetValue(CommandParameterProperty);
            set => SetValue(CommandParameterProperty, value);
        }

        public bool IsDropdownOpen
        {
            get => (bool)GetValue(IsDropdownOpenProperty);
            set => SetValue(IsDropdownOpenProperty, value);
        }

        public FreezableCollection<SplitButtonItem> DropdownItems
        {
            get => (FreezableCollection<SplitButtonItem>)GetValue(DropdownItemsProperty);
            set => SetValue(DropdownItemsProperty, value);
        }

        public SplitButtonStyle ButtonStyleType
        {
            get => (SplitButtonStyle)GetValue(ButtonStyleTypeProperty);
            set => SetValue(ButtonStyleTypeProperty, value);
        }

        #endregion

        public KmSplitButton()
        {
            DropdownItems = new FreezableCollection<SplitButtonItem>();
        }

        public void ToggleDropdown() => IsDropdownOpen = !IsDropdownOpen;
    }

    public class SplitButtonItem : Freezable
    {
        protected override Freezable CreateInstanceCore() => new SplitButtonItem();

        public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
            nameof(Text), typeof(string), typeof(SplitButtonItem), new PropertyMetadata(""));

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
            nameof(Icon), typeof(string), typeof(SplitButtonItem), new PropertyMetadata(null));

        public string? Icon
        {
            get => (string?)GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }

        public static readonly DependencyProperty CommandProperty = DependencyProperty.Register(
            nameof(Command), typeof(ICommand), typeof(SplitButtonItem), new PropertyMetadata(null));

        public ICommand? Command
        {
            get => (ICommand?)GetValue(CommandProperty);
            set => SetValue(CommandProperty, value);
        }

        public static readonly DependencyProperty CommandParameterProperty = DependencyProperty.Register(
            nameof(CommandParameter), typeof(object), typeof(SplitButtonItem), new PropertyMetadata(null));

        public object? CommandParameter
        {
            get => GetValue(CommandParameterProperty);
            set => SetValue(CommandParameterProperty, value);
        }

        public static readonly DependencyProperty IsSeparatorProperty = DependencyProperty.Register(
            nameof(IsSeparator), typeof(bool), typeof(SplitButtonItem), new PropertyMetadata(false));

        public bool IsSeparator
        {
            get => (bool)GetValue(IsSeparatorProperty);
            set => SetValue(IsSeparatorProperty, value);
        }
    }

    public enum SplitButtonStyle
    {
        Primary,
        Secondary,
        Success,
        Danger,
        Warning
    }
}
