using KamatekCrm.Shared.Enums;

namespace KamatekCrm.Models
{
    public class ToastMessage
    {
        public string Title { get; set; }
        public string Message { get; set; }
        public ToastType Type { get; set; }
        public TimeSpan Duration { get; set; } = TimeSpan.FromSeconds(3);

        public ToastMessage(string title, string message, ToastType type)
        {
            Title = title;
            Message = message;
            Type = type;
        }
    }
}
