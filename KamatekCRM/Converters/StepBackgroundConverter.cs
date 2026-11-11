using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace KamatekCrm.Converters
{
    /// <summary>
    /// Wizard stepper için adım durumuna göre arka plan rengi döndürür.
    /// CurrentStep ile StepIndex karşılaştırılır:
    /// - Aktif: CurrentStep == StepIndex
    /// - Tamamlanmış: CurrentStep > StepIndex
    /// - Beklemede: CurrentStep < StepIndex
    /// </summary>
    public class StepBackgroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var resourceKey = "StatusNeutralBg";

            if (value is int currentStep && parameter != null)
            {
                int? stepIndex = parameter switch
                {
                    int intParam => intParam,
                    _ when int.TryParse(parameter.ToString(), out int parsedIndex) => parsedIndex,
                    _ => null
                };

                if (stepIndex.HasValue)
                {
                    if (currentStep == stepIndex.Value)
                    {
                        resourceKey = "AccentBrush";
                    }
                    else if (currentStep > stepIndex.Value)
                    {
                        resourceKey = "SuccessBrush";
                    }
                }
            }

            return Application.Current?.TryFindResource(resourceKey) as Brush ?? DependencyProperty.UnsetValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}

