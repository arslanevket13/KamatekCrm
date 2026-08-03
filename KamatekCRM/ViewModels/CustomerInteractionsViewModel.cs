using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using KamatekCrm.ApplicationCore.DTOs.CustomerInteractions;
using KamatekCrm.ApplicationCore.Interfaces;
using KamatekCrm.Services;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Views;
using Microsoft.Extensions.DependencyInjection;

namespace KamatekCrm.ViewModels
{
    public partial class CustomerInteractionsViewModel : ViewModelBase
    {
        private readonly ICustomerInteractionReadService _readService;
        private readonly ICustomerInteractionCommandService _commandService;
        private readonly IToastService _toastService;
        private readonly ILoadingService _loadingService;
        private readonly IServiceProvider _serviceProvider;
        private readonly IAuthService _authService;

        private CustomerInteractionDto? _selectedInteraction;
        private string _searchText = string.Empty;
        private CustomerInteractionSummaryDto _summary = new CustomerInteractionSummaryDto();

        public ObservableCollection<CustomerInteractionDto> Interactions { get; } = new ObservableCollection<CustomerInteractionDto>();

        public CustomerInteractionDto? SelectedInteraction
        {
            get => _selectedInteraction;
            set
            {
                if (SetProperty(ref _selectedInteraction, value))
                {
                    OnPropertyChanged(nameof(HasSelectedInteraction));
                    CompleteInteractionCommand.NotifyCanExecuteChanged();
                    ConvertToQuoteCommand.NotifyCanExecuteChanged();
                    ConvertToServiceJobCommand.NotifyCanExecuteChanged();
                }
            }
        }

        public bool HasSelectedInteraction => SelectedInteraction != null;

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    _ = LoadInteractionsAsync();
                }
            }
        }

        public CustomerInteractionSummaryDto Summary
        {
            get => _summary;
            set => SetProperty(ref _summary, value);
        }

        public bool IsAdmin => _authService.IsAdmin;

        public CustomerInteractionsViewModel(
            ICustomerInteractionReadService readService,
            ICustomerInteractionCommandService commandService,
            IToastService toastService,
            ILoadingService loadingService,
            IServiceProvider serviceProvider,
            IAuthService authService)
        {
            _readService = readService;
            _commandService = commandService;
            _toastService = toastService;
            _loadingService = loadingService;
            _serviceProvider = serviceProvider;
            _authService = authService;

            _ = LoadInteractionsAsync();
        }

        [RelayCommand]
        private async Task LoadInteractionsAsync()
        {
            _loadingService.Show();
            try
            {
                var filter = new CustomerInteractionFilterDto
                {
                    SearchText = SearchText,
                    PageNumber = 1,
                    PageSize = 100
                };

                var res = await _readService.FilterAsync(filter);
                if (res.IsSuccess && res.Value != null)
                {
                    Interactions.Clear();
                    foreach (var item in res.Value.Items)
                    {
                        Interactions.Add(item);
                    }
                }

                var summaryRes = await _readService.GetSummaryMetricsAsync();
                if (summaryRes.IsSuccess && summaryRes.Value != null)
                {
                    Summary = summaryRes.Value;
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

        [RelayCommand]
        private void OpenAddWindow()
        {
            var vm = _serviceProvider.GetRequiredService<QuickInteractionAddViewModel>();
            var win = new QuickInteractionAddWindow(vm)
            {
                Owner = Application.Current.MainWindow
            };
            win.ShowDialog();
            _ = LoadInteractionsAsync();
        }

        private bool CanActOnSelected() => SelectedInteraction != null && SelectedInteraction.Status != InteractionStatus.Completed;

        [RelayCommand(CanExecute = nameof(CanActOnSelected))]
        private async Task CompleteInteractionAsync()
        {
            if (SelectedInteraction == null) return;
            _loadingService.Show();
            try
            {
                var dto = new UpdateCustomerInteractionStatusDto
                {
                    InteractionId = SelectedInteraction.Id,
                    NewStatus = InteractionStatus.Completed,
                    Reason = "Kullanıcı tarafından tamamlandı.",
                    ResolutionNotes = "Görüşme ve takip tamamlandı."
                };

                var res = await _commandService.UpdateStatusAsync(dto);
                if (res.IsSuccess)
                {
                    _toastService.ShowSuccess("Tamamlandı", $"{SelectedInteraction.InteractionNumber} tamamlandı.");
                    await LoadInteractionsAsync();
                }
                else
                {
                    _toastService.ShowError("Hata", res.Error ?? "Hata");
                }
            }
            finally
            {
                _loadingService.Hide();
            }
        }

        [RelayCommand(CanExecute = nameof(CanActOnSelected))]
        private void ConvertToQuote()
        {
            if (SelectedInteraction == null) return;
            _toastService.ShowInfo("Teklife Dönüştür", $"{SelectedInteraction.InteractionNumber} standart teklif hazırlama ekranına aktarılıyor.");
        }

        [RelayCommand(CanExecute = nameof(CanActOnSelected))]
        private void ConvertToServiceJob()
        {
            if (SelectedInteraction == null) return;
            _toastService.ShowInfo("Servis İşe Dönüştür", $"{SelectedInteraction.InteractionNumber} servis iş emri ekranına aktarılıyor.");
        }
    }
}
