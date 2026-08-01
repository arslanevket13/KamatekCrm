using System.Globalization;
using System.Windows;
using System.Windows.Data;
using KamatekCrm.ApplicationCore.Interfaces;
using KamatekCrm.ApplicationCore.Security;
using Microsoft.Extensions.DependencyInjection;

namespace KamatekCrm.Converters;

public sealed class SensitiveDataConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (!Enum.TryParse<PersonalDataKind>(parameter?.ToString(), true, out var kind))
            return "••••";

        var protection = App.ServiceProvider?.GetService<IPersonalDataProtectionService>();
        return protection?.Protect(value?.ToString(), kind) ?? "••••";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        DependencyProperty.UnsetValue;
}
