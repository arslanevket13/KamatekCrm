using System.ComponentModel;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;

namespace KamatekCrm.ViewModels
{
    /// <summary>
    /// Tüm ViewModellerin base sınıfı - INotifyPropertyChanged ve INotifyDataErrorInfo implementasyonu
    /// (CommunityToolkit.Mvvm.ComponentModel.ObservableValidator üzerinden)
    /// </summary>
    public partial class ViewModelBase : ObservableValidator
    {
        /// <summary>
        /// Property değerini set eder, değişikliği bildirir ve varsa DataAnnotations
        /// kurallarını çalıştırır. Böylece tüm formlar aynı doğrulama sözleşmesini kullanır.
        /// </summary>
        protected new bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            return base.SetProperty(ref field, value, validate: true, propertyName);
        }
    }
}
