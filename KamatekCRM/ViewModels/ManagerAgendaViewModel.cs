using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using KamatekCrm.ApplicationCore.DTOs.CustomerInteractions;
using KamatekCrm.ApplicationCore.Interfaces;
using KamatekCrm.Services;
using KamatekCrm.Shared.Enums;

namespace KamatekCrm.ViewModels
{
    public partial class ManagerAgendaViewModel : ViewModelBase
    {
        private readonly ICustomerInteractionReadService _readService;
        private readonly ICustomerInteractionCommandService _commandService;
        private readonly IToastService _toastService;
        private readonly ILoadingService _loadingService;
        private readonly IAuthService _authService;

        private CustomerInteractionDto? _selectedAgendaItem;

        public ObservableCollection<CustomerInteractionDto> AgendaItems { get; } = new ObservableCollection<CustomerInteractionDto>();

        public CustomerInteractionDto? SelectedAgendaItem
        {
            get => _selectedAgendaItem;
            set
            {
                if (SetProperty(ref _selectedAgendaItem, value))
                {
                    OnPropertyChanged(nameof(HasSelectedItem));
                    MarkAsSeenCommand.NotifyCanExecuteChanged();
                    ResolveAgendaCommand.NotifyCanExecuteChanged();
                }
            }
        }

        public bool HasSelectedItem => SelectedAgendaItem != null;
        public bool IsAdmin => _authService.IsAdmin;

        public ManagerAgendaViewModel(
            ICustomerInteractionReadService readService,
            ICustomerInteractionCommandService commandService,
            IToastService toastService,
            ILoadingService loadingService,
            IAuthService authService)
        {
            _readService = readService;
            _commandService = commandService;
            _toastService = toastService;
            _loadingService = loadingService;
            _authService = authService;

            _ = LoadAgendaAsync();
        }

        [RelayCommand]
        private async Task LoadAgendaAsync()
        {
            _loadingService.Show();
            try
            {
                var res = await _readService.GetManagerAgendaAsync();
                if (res.IsSuccess && res.Value != null)
                {
                    AgendaItems.Clear();
                    foreach (var item in res.Value)
                    {
                        AgendaItems.Add(item);
                    }
                }
            }
            catch (Exception ex)
            {
                _toastService.ShowError("Hata", ex.Message ?? "Hata");
            }
            finally
            {
                _loadingService.Hide();
            }
        }

        private bool CanActOnAgenda() => SelectedAgendaItem != null && SelectedAgendaItem.Status != InteractionStatus.Completed;

        [RelayCommand(CanExecute = nameof(CanActOnAgenda))]
        private async Task MarkAsSeenAsync()
        {
            if (SelectedAgendaItem == null) return;
            _loadingService.Show();
            try
            {
                var dto = new UpdateCustomerInteractionStatusDto
                {
                    InteractionId = SelectedAgendaItem.Id,
                    NewStatus = InteractionStatus.Seen,
                    Reason = "Yönetici tarafından incelendi."
                };

                var res = await _commandService.UpdateStatusAsync(dto);
                if (res.IsSuccess)
                {
                    _toastService.ShowSuccess("İncelendi", $"{SelectedAgendaItem.InteractionNumber} incelendi olarak işaretlendi.");
                    await LoadAgendaAsync();
                }
            }
            finally
            {
                _loadingService.Hide();
            }
        }

        [RelayCommand(CanExecute = nameof(CanActOnAgenda))]
        private async Task ResolveAgendaAsync()
        {
            if (SelectedAgendaItem == null) return;
            _loadingService.Show();
            try
            {
                var dto = new UpdateCustomerInteractionStatusDto
                {
                    InteractionId = SelectedAgendaItem.Id,
                    NewStatus = InteractionStatus.Completed,
                    Reason = "Yönetici tarafından sonuçlandırıldı."
                };

                var res = await _commandService.UpdateStatusAsync(dto);
                if (res.IsSuccess)
                {
                    _toastService.ShowSuccess("Sonuçlandırıldı", $"{SelectedAgendaItem.InteractionNumber} yönetici gündeminden kaldırıldı.");
                    await LoadAgendaAsync();
                }
            }
            finally
            {
                _loadingService.Hide();
            }
        }
    }
}
