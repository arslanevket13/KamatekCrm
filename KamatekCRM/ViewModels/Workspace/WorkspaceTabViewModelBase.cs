using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KamatekCrm.ApplicationCore.DTOs.ServiceJobs;
using KamatekCrm.Shared.Enums;

namespace KamatekCrm.ViewModels
{
    /// <summary>
    /// Application katmanından gelen tek bir izinli işlem (AllowedActions üyesi) için
    /// buton paketi: görünen metin, aktiflik, pasiflik gerekçesi (tooltip) ve komut.
    /// Aktiflik kararı UI'da değil, <see cref="WorkOrderActionInfo.IsEnabled"/>'da saklıdır.
    /// </summary>
    public sealed class WorkspaceActionItem
    {
        public WorkOrderActionInfo Info { get; }
        public IAsyncRelayCommand Command { get; }

        public WorkOrderAction Action => Info.Action;
        public string Title => Info.Title;
        public string Description => Info.Description;
        public string PrimaryButtonText => Info.PrimaryButtonText;
        public bool IsEnabled => Info.IsEnabled;
        public string DisabledReason => Info.DisabledReason;
        public WorkOrderSeverity Severity => Info.Severity;
        public DateTime? DueDate => Info.DueDate;

        public WorkspaceActionItem(WorkOrderActionInfo info, Func<WorkOrderAction, Task> execute)
        {
            Info = info ?? throw new ArgumentNullException(nameof(info));
            Command = new AsyncRelayCommand(() => execute(info.Action));
        }
    }

    /// <summary>
    /// Çalışma alanı sekme ViewModel'lerinin ortak tabanı. Her sekme, application katmanının
    /// ürettiği AllowedActions listesinden yalnızca kendisiyle ilgili işlemleri gösterir;
    /// butonların görünürlüğü/aktifliği bu listeden beslenir (UI kural üretmez).
    /// </summary>
    public abstract partial class WorkspaceTabViewModelBase : ViewModelBase
    {
        /// <summary>Shell dağıtıcısı — alt sınıflar ek koleksiyonları bu delegeden besleyebilir.</summary>
        protected readonly Func<WorkOrderAction, Task>? ExecuteAction;

        protected WorkspaceTabViewModelBase(Func<WorkOrderAction, Task>? executeAction = null)
        {
            ExecuteAction = executeAction;
        }

        public ObservableCollection<WorkspaceActionItem> Actions { get; } = new();
        public bool HasActions => Actions.Count > 0;

        /// <summary>İzinli işlem listesinden tek bir işlemin meta verisini döndürür (test ve guard'lar için).</summary>
        public WorkOrderActionInfo? GetAction(WorkOrderAction action) =>
            Actions.FirstOrDefault(item => item.Action == action)?.Info;

        /// <summary>İşlem pasifse gerekçesini döndürür; komutlar bayat durumda bunu kullanır.</summary>
        protected bool IsActionEnabled(WorkOrderAction action, out string disabledReason)
        {
            var info = GetAction(action);
            if (info is null)
            {
                disabledReason = "Bu işlem şu anda sunulmuyor.";
                return false;
            }
            disabledReason = info.DisabledReason;
            return info.IsEnabled;
        }

        public virtual void ApplyActions(IReadOnlyList<WorkOrderActionInfo>? all)
        {
            Actions.Clear();
            if (all is not null && ExecuteAction is not null)
            {
                foreach (var info in all.Where(action => IsRelevantAction(action.Action)))
                {
                    Actions.Add(new WorkspaceActionItem(info, ExecuteAction));
                }
            }
            OnPropertyChanged(nameof(HasActions));
        }

        /// <summary>Sekmenin göstereceği işlem kümesi (uygulama listesinden filtre).</summary>
        protected abstract bool IsRelevantAction(WorkOrderAction action);
    }

    /// <summary>
    /// Genel Bakış sekmesindeki "İş Bilgileri" kartı için salt-okunur künye.
    /// Shell, GetWorkspaceAsync'ten gelen canlı veriyi her yenilemede buraya kopyalar.
    /// </summary>
    public partial class WorkspaceJobHeader : ViewModelBase
    {
        [ObservableProperty]
        private string _customerFullName = "—";

        [ObservableProperty]
        private string _customerPhone = "—";

        [ObservableProperty]
        private string _workOrderTypeDisplay = "—";

        [ObservableProperty]
        private string _priorityDisplay = "—";

        [ObservableProperty]
        private string _assignedTechnician = "Atanmadı";

        [ObservableProperty]
        private string _scheduledDateDisplay = "Planlanmadı";

        [ObservableProperty]
        private string _createdDateDisplay = "—";

        [ObservableProperty]
        private string _slaStatusDisplay = "—";

        [ObservableProperty]
        private string _statusDisplay = "—";

        internal void Apply(ServiceJobRowDto row, WorkOrderWorkspaceDto dto)
        {
            CustomerFullName = string.IsNullOrWhiteSpace(dto.CustomerName)
                ? row.CustomerFullName
                : dto.CustomerName;
            CustomerPhone = string.IsNullOrWhiteSpace(dto.CustomerPhone)
                ? row.CustomerPhone
                : dto.CustomerPhone;
            WorkOrderTypeDisplay = row.WorkOrderTypeDisplay;
            PriorityDisplay = row.PriorityDisplay;
            StatusDisplay = ServiceJobRowDto.MapStatusDisplay(dto.JobStatus);
            AssignedTechnician = string.IsNullOrWhiteSpace(dto.AssignedTechnicianName)
                ? "Atanmadı"
                : dto.AssignedTechnicianName;
            ScheduledDateDisplay = dto.TargetDate?.ToString("dd.MM.yyyy HH:mm") ?? "Planlanmadı";
            CreatedDateDisplay = dto.CreatedAt.ToString("dd.MM.yyyy HH:mm");
            SlaStatusDisplay = dto.SlaStatus;
        }
    }

    /// <summary>Uyarı şeridinde gösterilecek tek uyarı (mesaj + önem simgesi).</summary>
    public sealed class WorkspaceWarningItem
    {
        public string Message { get; }
        public string Glyph { get; }
        public WorkOrderSeverity Severity { get; }

        public WorkspaceWarningItem(WorkOrderWarning warning)
        {
            Message = warning.Message;
            Severity = warning.Severity;
            Glyph = warning.Severity switch
            {
                WorkOrderSeverity.Critical => "❌",
                WorkOrderSeverity.Warning => "⚠️",
                WorkOrderSeverity.Action => "👉",
                _ => "ℹ️"
            };
        }
    }
}
