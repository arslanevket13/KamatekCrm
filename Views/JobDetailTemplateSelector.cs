using System.Windows;
using System.Windows.Controls;
using KamatekCrm.Models.JobDetails;

namespace KamatekCrm.Views
{
    /// <summary>
    /// İş detay türüne göre doğru DataTemplate'i seçen selector
    /// </summary>
    public class JobDetailTemplateSelector : DataTemplateSelector
    {
        public DataTemplate? CctvTemplate { get; set; }
        public DataTemplate? VideoIntercomTemplate { get; set; }
        public DataTemplate? FireAlarmTemplate { get; set; }
        public DataTemplate? BurglarAlarmTemplate { get; set; }
        public DataTemplate? SmartHomeTemplate { get; set; }
        public DataTemplate? AccessControlTemplate { get; set; }
        public DataTemplate? SatelliteTemplate { get; set; }
        public DataTemplate? FiberOpticTemplate { get; set; }

        public override DataTemplate? SelectTemplate(object item, DependencyObject container)
        {
            return item switch
            {
                CctvJobDetail => CctvTemplate,
                VideoIntercomJobDetail => VideoIntercomTemplate,
                FireAlarmJobDetail => FireAlarmTemplate,
                BurglarAlarmJobDetail => BurglarAlarmTemplate,
                SmartHomeJobDetail => SmartHomeTemplate,
                AccessControlJobDetail => AccessControlTemplate,
                SatelliteJobDetail => SatelliteTemplate,
                FiberOpticJobDetail => FiberOpticTemplate,
                _ => base.SelectTemplate(item, container)
            };
        }
    }
}
