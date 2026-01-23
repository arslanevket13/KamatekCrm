using System;
using System.Globalization;
using System.Windows.Data;

namespace KamatekCrm.Helpers
{
    /// <summary>
    /// Rol adını kullanıcı dostu gösterimle dönüştürür
    /// Admin -> Patron, Technician -> Teknisyen, Viewer -> İzleyici
    /// </summary>
    public class RoleDisplayConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string role)
            {
                return role.ToLower() switch
                {
                    "admin" => "Patron",
                    "technician" => "Teknisyen",
                    "viewer" => "İzleyici",
                    _ => role
                };
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string displayRole)
            {
                return displayRole.ToLower() switch
                {
                    "patron" => "Admin",
                    "teknisyen" => "Technician",
                    "izleyici" => "Viewer",
                    _ => displayRole
                };
            }
            return value;
        }
    }
}
