using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;

namespace KamatekCrm.Converters
{
    public class BallInCourtTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is BallInCourtStatus status)
            {
                return status switch
                {
                    BallInCourtStatus.Us => "Top Bizde",
                    BallInCourtStatus.Customer => "Müşteride",
                    BallInCourtStatus.Manager => "Patronda",
                    _ => "Top Bizde"
                };
            }
            return "Top Bizde";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    }

    public class BallInCourtColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is BallInCourtStatus status)
            {
                var key = status switch
                {
                    BallInCourtStatus.Us => "ThemePrimary",
                    BallInCourtStatus.Customer => "StatusWarningFg",
                    BallInCourtStatus.Manager => "AccentBrush",
                    _ => "ThemePrimary"
                };
                return Application.Current.TryFindResource(key) ?? Brushes.DodgerBlue;
            }
            return Application.Current.TryFindResource("ThemePrimary") ?? Brushes.DodgerBlue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    }

    public class DirectionTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is InteractionDirection dir)
            {
                return dir switch
                {
                    InteractionDirection.InboundRequest => "Müşteri Talebi",
                    InteractionDirection.OutboundInfoRequest => "Bilgi/Onay İstendi",
                    InteractionDirection.InternalNote => "İç Not",
                    _ => "Müşteri Talebi"
                };
            }
            return "Müşteri Talebi";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    }

    public class ThreadEntryTypeTitleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ThreadEntryType type)
            {
                return type switch
                {
                    ThreadEntryType.Note => "Not",
                    ThreadEntryType.CallAttempt => "Arama Denemesi",
                    ThreadEntryType.CustomerReply => "Müşteri Yanıtı",
                    ThreadEntryType.Snooze => "Erteleme",
                    ThreadEntryType.Handoff => "Sorumlu Devri",
                    ThreadEntryType.ManagerInstruction => "★ PATRON TALİMATI",
                    ThreadEntryType.SystemConversion => "Sistem Dönüşümü",
                    _ => "Kayıt"
                };
            }
            return "Kayıt";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    }

    public class IsManagerInstructionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ThreadEntryType type)
            {
                return type == ThreadEntryType.ManagerInstruction;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    }
}
