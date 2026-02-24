namespace KamatekCrm.Services
{
    public interface ILoadingService
    {
        void Show(string message = "Yükleniyor...");
        void Hide();
    }
}
