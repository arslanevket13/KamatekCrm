using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace KamatekCrm.Helpers
{
    /// <summary>
    /// Wizard stepper için adım durumuna göre arka plan rengi döndürür.
    /// CurrentStep ile StepIndex karşılaştırılır:
    /// - Aktif (mavi): CurrentStep == StepIndex
    /// - Tamamlanmış (yeşil): CurrentStep > StepIndex
    /// - Beklemede (gri): CurrentStep < StepIndex
    /// </summary>
    public class StepBackgroundConverter : IValueConverter
    {
        private static readonly SolidColorBrush ActiveBrush = new SolidColorBrush(Color.FromRgb(0x21, 0x96, 0xF3)); // #2196F3 Blue
        private static readonly SolidColorBrush CompletedBrush = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50)); // #4CAF50 Green
        private static readonly SolidColorBrush InactiveBrush = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0)); // #E0E0E0 Gray

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int currentStep && parameter != null)
            {
                int stepIndex;
                if (parameter is int intParam)
                {
                    stepIndex = intParam;
                }
                else if (int.TryParse(parameter.ToString(), out int parsedIndex))
                {
                    stepIndex = parsedIndex;
                }
                else
                {
                    return InactiveBrush;
                }

                if (currentStep == stepIndex)
                {
                    return ActiveBrush; // Aktif adım - Mavi
                }
                else if (currentStep > stepIndex)
                {
                    return CompletedBrush; // Tamamlanmış adım - Yeşil
                }
                else
                {
                    return InactiveBrush; // Bekleyen adım - Gri
                }
            }

            return InactiveBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
