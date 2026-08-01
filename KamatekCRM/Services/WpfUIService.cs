using System.Windows;
using KamatekCrm.Shared.Services;

namespace KamatekCrm.Services
{
    public class WpfUIService : IUIService
    {
        public void InvokeOnUIThread(Action action)
        {
            if (Application.Current?.Dispatcher != null && !Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.Invoke(action);
            }
            else
            {
                action();
            }
        }

        public async Task InvokeOnUIThreadAsync(Func<Task> action)
        {
            if (Application.Current?.Dispatcher != null && !Application.Current.Dispatcher.CheckAccess())
            {
                await Application.Current.Dispatcher.InvokeAsync(action).Task.Unwrap();
            }
            else
            {
                await action();
            }
        }

        public void SetClipboardText(string text)
        {
            InvokeOnUIThread(() =>
            {
                Clipboard.SetText(text);
            });
        }
    }
}
