using System.Collections.Generic;
using System.Collections.ObjectModel;
using KamatekCrm.ApplicationCore.DTOs.ServiceJobs;
using KamatekCrm.Shared.Enums;

namespace KamatekCrm.ViewModels
{
    /// <summary>
    /// Geçmiş sekmesi: işin son hareketleri (GetWorkspaceAsync'in RecentActivities
    /// projeksiyonundan beslenir). Sekme veri gösterir; işlem üretmez.
    /// </summary>
    public partial class WorkspaceTimelineViewModel : WorkspaceTabViewModelBase
    {
        public WorkspaceTimelineViewModel()
            : base(executeAction: null)
        {
        }

        public ObservableCollection<ServiceJobHistoryDto> Entries { get; } = new();
        public bool HasEntries => Entries.Count > 0;

        protected override bool IsRelevantAction(WorkOrderAction action) => false;

        public void ApplyData(IReadOnlyList<ServiceJobHistoryDto>? entries)
        {
            Entries.Clear();
            if (entries is not null)
            {
                foreach (var entry in entries) Entries.Add(entry);
            }
            OnPropertyChanged(nameof(HasEntries));
        }
    }
}
