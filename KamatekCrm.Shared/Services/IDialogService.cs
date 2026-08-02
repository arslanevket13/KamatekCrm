namespace KamatekCrm.Shared.Services
{
    public interface IDialogService
    {
        Task ShowMessageAsync(string message, string title = "Bilgi");
        Task ShowWarningAsync(string message, string title = "Uyarı");
        Task ShowErrorAsync(string message, string title = "Hata");
        Task<bool> ShowConfirmationAsync(string message, string title = "Onay");
        Task<string?> ShowInputAsync(string message, string title = "Bilgi Girişi", string? defaultValue = null);
        Task<string?> ShowOpenFileDialogAsync(string title = "Dosya Seç", string filter = "Tüm Dosyalar (*.*)|*.*");
        Task<IReadOnlyList<string>> ShowOpenFilesDialogAsync(string title = "Dosya Seç", string filter = "Tüm Dosyalar (*.*)|*.*");
        Task<string?> ShowSaveFileDialogAsync(string title = "Dosya Kaydet", string filter = "Tüm Dosyalar (*.*)|*.*", string? defaultFileName = null);
    }
}
