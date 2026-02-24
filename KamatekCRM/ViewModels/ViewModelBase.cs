using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace KamatekCrm.ViewModels
{
    /// <summary>
    /// Tüm ViewModellerin base sınıfı - INotifyPropertyChanged implementasyonu
    /// </summary>
    public class ViewModelBase : INotifyPropertyChanged
    {
        /// <summary>
        /// Property değiştiğinde tetiklenir
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Property değişikliğini bildirir
        /// </summary>
        /// <param name="propertyName">Değişen property adı</param>
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Property değerini set eder ve değişikliği bildirir
        /// </summary>
        /// <typeparam name="T">Property tipi</typeparam>
        /// <param name="field">Backing field</param>
        /// <param name="value">Yeni değer</param>
        /// <param name="propertyName">Property adı</param>
        /// <returns>Değer değiştiyse true</returns>
        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
