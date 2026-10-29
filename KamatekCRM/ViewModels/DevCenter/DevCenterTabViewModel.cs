using System.Threading.Tasks;

namespace KamatekCrm.ViewModels.DevCenter
{
    /// <summary>
    /// Geliştirici Merkezi sekmelerinin ortak sözleşmesi.
    /// <para>
    /// <see cref="IsAvailable"/> false olan sekmeler veri uydurmaz; yalnızca
    /// <see cref="AvailabilityNote"/> metnini gösterir. Böylece henüz uygulanmamış
    /// bir yetenek "çalışıyor" gibi sunulmaz.
    /// </para>
    /// </summary>
    public abstract class DevCenterTabViewModel : ViewModelBase
    {
        protected DevCenterTabViewModel(string title, string icon)
        {
            Title = title;
            Icon = icon;
        }

        public string Title { get; }

        /// <summary>Emoji tabanlı simge (ek paket/ikon bağımlılığı yok).</summary>
        public string Icon { get; }

        public virtual bool IsAvailable => true;

        public virtual string AvailabilityNote => string.Empty;

        public virtual Task RefreshAsync() => Task.CompletedTask;
    }
}
