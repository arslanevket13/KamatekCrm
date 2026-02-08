using CommunityToolkit.Mvvm.ComponentModel;

namespace KamatekCrm.ViewModels
{
    public partial class LoadingViewModel : ObservableObject
    {
        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private string _busyMessage = "Yükleniyor...";
    }
}
