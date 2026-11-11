using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace KamatekCrm.Components
{
    /// <summary>
    /// KmAvatar — Kullanıcı/Müşteri avatar bileşeni.
    /// İsim verirseniz baş harfleri gösterir, ImageSource verirseniz resim gösterir.
    /// Boyut: Small(28), Medium(36), Large(48), XLarge(64)
    /// </summary>
    public class KmAvatar : Control
    {
        private static readonly string[] PaletteResourceKeys =
        {
            "AccentBrush",
            "ThemePrimaryDark",
            "ThemeSuccessDark",
            "ThemeErrorDark",
            "KpiGradientPrimary",
            "KpiGradientSuccess",
            "KpiGradientError",
            "KpiGradientPurple"
        };

        private bool _isUpdatingAutoBackground;
        private bool _hasExplicitBackground;
        private string? _appliedAutoBrushKey;

        static KmAvatar()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(KmAvatar),
                new FrameworkPropertyMetadata(typeof(KmAvatar)));
        }

        public KmAvatar()
        {
            UpdateBackground();
        }

        #region Dependency Properties

        public static readonly DependencyProperty FullNameProperty = DependencyProperty.Register(
            nameof(FullName), typeof(string), typeof(KmAvatar),
            new PropertyMetadata(string.Empty, OnFullNameChanged));

        public static readonly DependencyProperty ImageSourceProperty = DependencyProperty.Register(
            nameof(ImageSource), typeof(ImageSource), typeof(KmAvatar),
            new PropertyMetadata(null));

        public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(
            nameof(Size), typeof(AvatarSize), typeof(KmAvatar),
            new PropertyMetadata(AvatarSize.Medium, OnSizeChanged));

        public static readonly DependencyProperty InitialsProperty = DependencyProperty.Register(
            nameof(Initials), typeof(string), typeof(KmAvatar),
            new PropertyMetadata("?"));

        public static readonly DependencyProperty AvatarBackgroundProperty = DependencyProperty.Register(
            nameof(AvatarBackground), typeof(Brush), typeof(KmAvatar),
            new PropertyMetadata(null, OnAvatarBackgroundChanged));

        public static readonly DependencyProperty AvatarFontSizeProperty = DependencyProperty.Register(
            nameof(AvatarFontSize), typeof(double), typeof(KmAvatar),
            new PropertyMetadata(14.0));

        public static readonly DependencyProperty AvatarDiameterProperty = DependencyProperty.Register(
            nameof(AvatarDiameter), typeof(double), typeof(KmAvatar),
            new PropertyMetadata(36.0));

        public static readonly DependencyProperty IsOnlineProperty = DependencyProperty.Register(
            nameof(IsOnline), typeof(bool?), typeof(KmAvatar),
            new PropertyMetadata(null));

        #endregion

        #region Properties

        public string FullName
        {
            get => (string)GetValue(FullNameProperty);
            set => SetValue(FullNameProperty, value);
        }

        public ImageSource? ImageSource
        {
            get => (ImageSource?)GetValue(ImageSourceProperty);
            set => SetValue(ImageSourceProperty, value);
        }

        public AvatarSize Size
        {
            get => (AvatarSize)GetValue(SizeProperty);
            set => SetValue(SizeProperty, value);
        }

        public string Initials
        {
            get => (string)GetValue(InitialsProperty);
            set => SetValue(InitialsProperty, value);
        }

        public Brush? AvatarBackground
        {
            get => (Brush?)GetValue(AvatarBackgroundProperty);
            set => SetValue(AvatarBackgroundProperty, value);
        }

        public double AvatarFontSize
        {
            get => (double)GetValue(AvatarFontSizeProperty);
            set => SetValue(AvatarFontSizeProperty, value);
        }

        public double AvatarDiameter
        {
            get => (double)GetValue(AvatarDiameterProperty);
            set => SetValue(AvatarDiameterProperty, value);
        }

        /// <summary>null = gösterme, true = yeşil, false = kırmızı</summary>
        public bool? IsOnline
        {
            get => (bool?)GetValue(IsOnlineProperty);
            set => SetValue(IsOnlineProperty, value);
        }

        #endregion

        private static void OnFullNameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is KmAvatar avatar)
            {
                avatar.UpdateInitials();
                avatar.UpdateBackground();
            }
        }

        private static void OnSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is KmAvatar avatar)
                avatar.UpdateSizeValues();
        }

        private void UpdateInitials()
        {
            if (string.IsNullOrWhiteSpace(FullName))
            {
                Initials = "?";
                return;
            }

            var parts = FullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            Initials = parts.Length >= 2
                ? $"{parts[0][0]}{parts[^1][0]}".ToUpperInvariant()
                : parts[0][..Math.Min(2, parts[0].Length)].ToUpperInvariant();
        }

        private static void OnAvatarBackgroundChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not KmAvatar avatar || avatar._isUpdatingAutoBackground)
                return;

            var valueSource = DependencyPropertyHelper.GetValueSource(avatar, AvatarBackgroundProperty);
            if (valueSource.BaseValueSource == BaseValueSource.Default)
            {
                avatar._hasExplicitBackground = false;
                return;
            }

            if (avatar._appliedAutoBrushKey != null &&
                ReferenceEquals(e.NewValue, avatar.TryFindResource(avatar._appliedAutoBrushKey)))
            {
                return;
            }

            avatar._hasExplicitBackground = e.NewValue != null;
        }

        private void UpdateBackground()
        {
            if (_hasExplicitBackground)
                return; // User-set custom background takes priority

            var index = Math.Abs((FullName ?? "").GetHashCode()) % PaletteResourceKeys.Length;
            var resourceKey = PaletteResourceKeys[index];

            _isUpdatingAutoBackground = true;
            try
            {
                _appliedAutoBrushKey = resourceKey;
                SetResourceReference(AvatarBackgroundProperty, resourceKey);
            }
            finally
            {
                _isUpdatingAutoBackground = false;
            }
        }

        private void UpdateSizeValues()
        {
            (AvatarDiameter, AvatarFontSize) = Size switch
            {
                AvatarSize.Small => (28.0, 11.0),
                AvatarSize.Medium => (36.0, 14.0),
                AvatarSize.Large => (48.0, 18.0),
                AvatarSize.XLarge => (64.0, 24.0),
                _ => (36.0, 14.0)
            };
        }
    }

    public enum AvatarSize
    {
        Small,
        Medium,
        Large,
        XLarge
    }
}
