namespace KamatekCrm.Shared.Services
{
    public interface IUIService
    {
        void InvokeOnUIThread(Action action);
        Task InvokeOnUIThreadAsync(Func<Task> action);
        void SetClipboardText(string text);
    }
}
