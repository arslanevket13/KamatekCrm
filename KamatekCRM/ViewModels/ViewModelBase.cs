using System.ComponentModel;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;

namespace KamatekCrm.ViewModels
{
    /// <summary>
    /// Tüm ViewModellerin base sınıfı - INotifyPropertyChanged ve INotifyDataErrorInfo implementasyonu
    /// (CommunityToolkit.Mvvm.ComponentModel.ObservableValidator üzerinden)
    /// </summary>
    public class ViewModelBase : ObservableValidator
    {
        // ObservableValidator already handles INotifyPropertyChanged and SetProperty.
        // Keeping this for backwards compatibility if any derived classes strictly call SetProperty with ref.
        
        /// <summary>
        /// Property değerini set eder ve değişikliği bildirir (Geriye dönük uyumluluk için)
        /// </summary>
        protected new bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            return base.SetProperty(ref field, value, propertyName);
        }
    }
}
