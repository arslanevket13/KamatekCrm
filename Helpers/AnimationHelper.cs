using System;
using System.Windows;
using System.Windows.Media.Animation;

namespace KamatekCrm.Helpers
{
    public static class AnimationHelper
    {
        public enum AnimationType
        {
            None,
            FadeIn,
            SlideInFromRight,
            SlideInFromBottom,
            PopIn
        }

        public static readonly DependencyProperty EntranceAnimationProperty =
            DependencyProperty.RegisterAttached(
                "EntranceAnimation",
                typeof(AnimationType),
                typeof(AnimationHelper),
                new PropertyMetadata(AnimationType.None, OnEntranceAnimationChanged));

        public static void SetEntranceAnimation(UIElement element, AnimationType value)
        {
            element.SetValue(EntranceAnimationProperty, value);
        }

        public static AnimationType GetEntranceAnimation(UIElement element)
        {
            return (AnimationType)element.GetValue(EntranceAnimationProperty);
        }

        private static void OnEntranceAnimationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FrameworkElement element)
            {
                if ((AnimationType)e.NewValue != AnimationType.None)
                {
                    element.Loaded += Element_Loaded;
                }
                else
                {
                    element.Loaded -= Element_Loaded;
                }
            }
        }

        private static void Element_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element)
            {
                var animationType = GetEntranceAnimation(element);
                string storyboardKey = animationType switch
                {
                    AnimationType.FadeIn => "FadeInAnimation",
                    AnimationType.SlideInFromRight => "SlideInFromRight",
                    AnimationType.SlideInFromBottom => "SlideInFromBottom",
                    AnimationType.PopIn => "PopInAnimation",
                    _ => null
                };

                if (storyboardKey != null)
                {
                    try 
                    {
                        var storyboard = element.FindResource(storyboardKey) as Storyboard;
                        storyboard?.Begin(element);
                    }
                    catch (ResourceReferenceKeyNotFoundException)
                    {
                        // Resource not found, ignore animation
                    }
                }
            }
        }
    }
}
