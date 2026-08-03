using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
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
    /// <summary>
    /// Filtre sekmeleri için enum.
    /// </summary>
    public enum InteractionListFilter
    {
        All,
        Today,
        FollowUpRequired,
        Overdue,
        AssignedToMe,
        Completed
    }

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

        // --- Yeni UI state ---
        private InteractionListFilter _activeFilter = InteractionListFilter.All;
        private CustomerPhoneMatchResultDto? _selectedCustomerContext;
        private bool _isLoadingContext;
        private bool _isDetailExpanded;
        private bool _isRightPanelVisible = true;
        private CancellationTokenSource? _contextLoadCts;

        public ObservableCollection<CustomerInteractionDto> Interactions { get; } = new ObservableCollection<CustomerInteractionDto>();
        public ObservableCollection<CustomerInteractionDto> RecentCustomerInteractions { get; } = new ObservableCollection<CustomerInteractionDto>();

        /// <summary>
        /// Sol panelde gömülü olarak kullanılacak form ViewModel.
        /// </summary>
        public QuickInteractionAddViewModel InlineFormViewModel { get; }

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
                    _ = LoadCustomerContextAsync();
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

        // --- Yeni UI state property'leri ---

        public InteractionListFilter ActiveFilter
        {
            get => _activeFilter;
            set
            {
                if (SetProperty(ref _activeFilter, value))
                {
                    _ = LoadInteractionsAsync();
                }
            }
        }

        public CustomerPhoneMatchResultDto? SelectedCustomerContext
        {
            get => _selectedCustomerContext;
            set
            {
                if (SetProperty(ref _selectedCustomerContext, value))
                {
                    OnPropertyChanged(nameof(HasCustomerContext));
                }
            }
        }

        public bool HasCustomerContext => SelectedCustomerContext != null;

        public bool IsLoadingContext
        {
            get => _isLoadingContext;
            set => SetProperty(ref _isLoadingContext, value);
        }

        public bool IsDetailExpanded
        {
            get => _isDetailExpanded;
            set => SetProperty(ref _isDetailExpanded, value);
        }

        public bool IsRightPanelVisible
        {
            get => _isRightPanelVisible;
            set => SetProperty(ref _isRightPanelVisible, value);
        }

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

            // Sol panel form VM'ini DI ile oluştur
            InlineFormViewModel = _serviceProvider.GetRequiredService<QuickInteractionAddViewModel>();
            InlineFormViewModel.OnSaved += OnInlineFormSaved;

            _ = LoadInteractionsAsync();
        }

        /// <summary>
        /// Inline form kaydedildikten sonra listeyi ve metrikleri yeniler.
        /// </summary>
        private void OnInlineFormSaved()
        {
            _ = LoadInteractionsAsync();
        }

        [RelayCommand]
        private async Task LoadInteractionsAsync()
        {
            _loadingService.Show();
            try
            {
                var filter = BuildFilter();

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

        /// <summary>
        /// Aktif filtreye göre CustomerInteractionFilterDto oluşturur.
        /// </summary>
        private CustomerInteractionFilterDto BuildFilter()
        {
            var filter = new CustomerInteractionFilterDto
            {
                SearchText = SearchText,
                PageNumber = 1,
                PageSize = 100
            };

            switch (ActiveFilter)
            {
                case InteractionListFilter.Today:
                    filter.StartDate = DateTime.UtcNow.Date;
                    filter.EndDate = DateTime.UtcNow.Date.AddDays(1).AddTicks(-1);
                    break;

                case InteractionListFilter.FollowUpRequired:
                    filter.RequiresFollowUp = true;
                    break;

                case InteractionListFilter.Overdue:
                    filter.OnlyOverdue = true;
                    break;

                case InteractionListFilter.AssignedToMe:
                    var currentUserId = _authService.CurrentUser?.Id;
                    if (currentUserId.HasValue)
                        filter.AssignedToUserId = currentUserId.Value;
                    break;

                case InteractionListFilter.Completed:
                    filter.Status = InteractionStatus.Completed;
                    break;

                case InteractionListFilter.All:
                default:
                    break;
            }

            return filter;
        }

        [RelayCommand]
        private void SelectFilter(string filterName)
        {
            if (Enum.TryParse<InteractionListFilter>(filterName, out var parsed))
            {
                ActiveFilter = parsed;
            }
        }

        [RelayCommand]
        private void ToggleRightPanel()
        {
            IsRightPanelVisible = !IsRightPanelVisible;
        }

        [RelayCommand]
        private void ResetInlineForm()
        {
            InlineFormViewModel.ResetForm();
        }

        /// <summary>
        /// Seçili görüşmenin müşteri bilgilerini yükler (CancellationToken ile race condition koruması).
        /// </summary>
        private async Task LoadCustomerContextAsync()
        {
            // Önceki yüklemeyi iptal et
            _contextLoadCts?.Cancel();
            _contextLoadCts = new CancellationTokenSource();
            var token = _contextLoadCts.Token;

            var interaction = SelectedInteraction;
            if (interaction == null)
            {
                SelectedCustomerContext = null;
                RecentCustomerInteractions.Clear();
                return;
            }

            IsLoadingContext = true;
            try
            {
                // Müşteri ID varsa bağlam verilerini yükle
                if (interaction.CustomerId.HasValue && interaction.CustomerId.Value > 0)
                {
                    token.ThrowIfCancellationRequested();

                    // Telefon ile arama yaparak müşteri bağlam bilgisini al
                    var phoneResult = await _readService.SearchByPhoneAsync(interaction.CallerPhone, token);
                    if (token.IsCancellationRequested) return;

                    if (phoneResult.IsSuccess && phoneResult.Value != null)
                    {
                        var match = phoneResult.Value.FirstOrDefault(m => m.CustomerId == interaction.CustomerId.Value);
                        SelectedCustomerContext = match;
                    }
                    else
                    {
                        SelectedCustomerContext = null;
                    }

                    // Son görüşmeleri yükle
                    token.ThrowIfCancellationRequested();
                    var recentRes = await _readService.GetByCustomerIdAsync(interaction.CustomerId.Value, token);
                    if (token.IsCancellationRequested) return;

                    RecentCustomerInteractions.Clear();
                    if (recentRes.IsSuccess && recentRes.Value != null)
                    {
                        foreach (var item in recentRes.Value.Take(5))
                        {
                            RecentCustomerInteractions.Add(item);
                        }
                    }
                }
                else
                {
                    // Müşteri kayıtlı değil — sadece görüşme detayını göster
                    SelectedCustomerContext = null;
                    RecentCustomerInteractions.Clear();
                }
            }
            catch (OperationCanceledException)
            {
                // Beklenen iptal — sessizce geç
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Müşteri bağlamı yüklenemedi: {ex.Message}");
                SelectedCustomerContext = null;
                RecentCustomerInteractions.Clear();
            }
            finally
            {
                if (!token.IsCancellationRequested)
                {
                    IsLoadingContext = false;
                }
            }
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
