using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using KamatekCrm.ApplicationCore.DTOs.CustomerInteractions;
using KamatekCrm.ApplicationCore.DTOs.Users;
using KamatekCrm.ApplicationCore.Interfaces;
using KamatekCrm.Services;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;

namespace KamatekCrm.ViewModels
{
    public partial class QuickInteractionAddViewModel : ViewModelBase
    {
        /// <summary>
        /// Inline kullanımda kayıt başarılı olduğunda tetiklenir.
        /// Parent ViewModel listeyi yenilemek için dinler.
        /// </summary>
        public event Action? OnSaved;
        private readonly ICustomerInteractionCommandService _commandService;
        private readonly ICustomerInteractionReadService _readService;
        private readonly IToastService _toastService;
        private readonly ILoadingService _loadingService;
        private readonly IUserAppService _userAppService;

        private string _callerPhone = string.Empty;
        private string _callerName = string.Empty;
        private string _subject = string.Empty;
        private string _summary = string.Empty;
        private string _detailedNotes = string.Empty;
        private InteractionChannel _selectedChannel = InteractionChannel.Phone;
        private InteractionRequestType _selectedRequestType = InteractionRequestType.PriceQuote;
        private InteractionPriority _selectedPriority = InteractionPriority.Normal;
        private bool _requiresFollowUp;
        private DateTime? _followUpDate = DateTime.Now.AddDays(1);
        private bool _requiresManagerAttention;
        private string _managerNotes = string.Empty;

        private int? _selectedCustomerId;
        private string _selectedCustomerName = string.Empty;
        private CustomerPhoneMatchResultDto? _selectedCustomerMatch;

        public ObservableCollection<CustomerPhoneMatchResultDto> PhoneMatches { get; } = new ObservableCollection<CustomerPhoneMatchResultDto>();
        public ObservableCollection<UserListItemDto> Users { get; } = new ObservableCollection<UserListItemDto>();

        private UserListItemDto? _selectedUser;
        public UserListItemDto? SelectedUser
        {
            get => _selectedUser;
            set => SetProperty(ref _selectedUser, value);
        }

        public string CallerPhone
        {
            get => _callerPhone;
            set
            {
                if (SetProperty(ref _callerPhone, value))
                {
                    _ = SearchPhoneDebouncedAsync(value);
                    SaveCommand.NotifyCanExecuteChanged();
                }
            }
        }

        public string CallerName
        {
            get => _callerName;
            set
            {
                if (SetProperty(ref _callerName, value))
                    SaveCommand.NotifyCanExecuteChanged();
            }
        }

        public string Subject
        {
            get => _subject;
            set
            {
                if (SetProperty(ref _subject, value))
                    SaveCommand.NotifyCanExecuteChanged();
            }
        }

        public string Summary
        {
            get => _summary;
            set
            {
                if (SetProperty(ref _summary, value))
                    SaveCommand.NotifyCanExecuteChanged();
            }
        }

        public string DetailedNotes
        {
            get => _detailedNotes;
            set => SetProperty(ref _detailedNotes, value);
        }

        public InteractionChannel SelectedChannel
        {
            get => _selectedChannel;
            set => SetProperty(ref _selectedChannel, value);
        }

        public InteractionRequestType SelectedRequestType
        {
            get => _selectedRequestType;
            set => SetProperty(ref _selectedRequestType, value);
        }

        public InteractionPriority SelectedPriority
        {
            get => _selectedPriority;
            set => SetProperty(ref _selectedPriority, value);
        }

        public bool RequiresFollowUp
        {
            get => _requiresFollowUp;
            set => SetProperty(ref _requiresFollowUp, value);
        }

        public DateTime? FollowUpDate
        {
            get => _followUpDate;
            set => SetProperty(ref _followUpDate, value);
        }

        public bool RequiresManagerAttention
        {
            get => _requiresManagerAttention;
            set => SetProperty(ref _requiresManagerAttention, value);
        }

        public string ManagerNotes
        {
            get => _managerNotes;
            set => SetProperty(ref _managerNotes, value);
        }

        public CustomerPhoneMatchResultDto? SelectedCustomerMatch
        {
            get => _selectedCustomerMatch;
            set
            {
                if (SetProperty(ref _selectedCustomerMatch, value) && value != null)
                {
                    _selectedCustomerId = value.CustomerId;
                    _selectedCustomerName = value.FullName;
                    if (string.IsNullOrWhiteSpace(CallerName))
                        CallerName = value.FullName;
                }
            }
        }

        public QuickInteractionAddViewModel(
            ICustomerInteractionCommandService commandService,
            ICustomerInteractionReadService readService,
            IUserAppService userAppService,
            IToastService toastService,
            ILoadingService loadingService)
        {
            _commandService = commandService;
            _readService = readService;
            _userAppService = userAppService;
            _toastService = toastService;
            _loadingService = loadingService;

            _ = LoadUsersAsync();
            _ = CheckDraftAsync();
        }

        private async Task LoadUsersAsync()
        {
            try
            {
                var result = await _userAppService.GetAllAsync();
                if (result.IsSuccess && result.Value != null)
                {
                    Users.Clear();
                    foreach (var user in result.Value.Where(u => u.IsActive))
                    {
                        Users.Add(user);
                    }
                }
            }
            catch { }
        }

        private async Task SearchPhoneDebouncedAsync(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone) || phone.Length < 3)
            {
                PhoneMatches.Clear();
                return;
            }

            try
            {
                var result = await _readService.SearchByPhoneAsync(phone);
                if (result.IsSuccess && result.Value != null)
                {
                    PhoneMatches.Clear();
                    foreach (var match in result.Value)
                    {
                        PhoneMatches.Add(match);
                    }
                    if (PhoneMatches.Count == 1)
                    {
                        SelectedCustomerMatch = PhoneMatches.First();
                    }
                }
            }
            catch { }
        }

        [RelayCommand]
        private void SelectQuickType(string typeName)
        {
            switch (typeName)
            {
                case "PriceQuote":
                    SelectedRequestType = InteractionRequestType.PriceQuote;
                    Subject = "Fiyat ve Teklif Talebi";
                    break;
                case "Discovery":
                    SelectedRequestType = InteractionRequestType.Discovery;
                    Subject = "Ücretsiz Keşif Talebi";
                    RequiresFollowUp = true;
                    break;
                case "ServiceStatus":
                    SelectedRequestType = InteractionRequestType.ServiceStatus;
                    Subject = "Cihaz / Servis Durumu Sorgulama";
                    break;
                case "ManagerAgenda":
                    SelectedRequestType = InteractionRequestType.ManagerAgenda;
                    Subject = "Yönetici ile Görüşme Talebi";
                    RequiresManagerAttention = true;
                    SelectedPriority = InteractionPriority.High;
                    break;
                case "Complaint":
                    SelectedRequestType = InteractionRequestType.Complaint;
                    Subject = "Müşteri Şikâyet Bildirimi";
                    RequiresManagerAttention = true;
                    SelectedPriority = InteractionPriority.Critical;
                    break;
                case "CallBack":
                    SelectedRequestType = InteractionRequestType.CallBack;
                    Subject = "Müşteri Geri Aranmak İstiyor";
                    RequiresFollowUp = true;
                    break;
            }
        }

        private bool CanSave()
        {
            return !string.IsNullOrWhiteSpace(CallerName) &&
                   !string.IsNullOrWhiteSpace(CallerPhone) &&
                   !string.IsNullOrWhiteSpace(Subject) &&
                   !string.IsNullOrWhiteSpace(Summary);
        }

        [RelayCommand(CanExecute = nameof(CanSave))]
        private async Task SaveAsync(Window? window)
        {
            _loadingService.Show();
            try
            {
                var dto = new CreateCustomerInteractionDto
                {
                    IdempotencyKey = Guid.NewGuid(),
                    CustomerId = _selectedCustomerId,
                    CustomerName = _selectedCustomerName,
                    CallerName = CallerName,
                    CallerPhone = CallerPhone,
                    Channel = SelectedChannel,
                    RequestType = SelectedRequestType,
                    Subject = Subject,
                    Summary = Summary,
                    DetailedNotes = DetailedNotes,
                    Priority = SelectedPriority,
                    AssignedToUserId = SelectedUser?.Id,
                    AssignedToUsername = SelectedUser?.AdSoyad,
                    RequiresFollowUp = RequiresFollowUp,
                    FollowUpDate = RequiresFollowUp ? FollowUpDate?.ToUniversalTime() : null,
                    RequiresManagerAttention = RequiresManagerAttention,
                    ManagerNotes = ManagerNotes
                };

                var res = await _commandService.CreateAsync(dto);
                if (res.IsSuccess)
                {
                    _toastService.ShowSuccess("Başarılı", $"{res.Value?.InteractionNumber} numaralı görüşme kaydı oluşturuldu.");
                    await _commandService.ClearDraftAsync();

                    if (window != null)
                    {
                        window.Close();
                    }
                    else
                    {
                        // Inline kullanım: formu sıfırla ve parent'ı bilgilendir
                        ResetForm();
                        OnSaved?.Invoke();
                    }
                }
                else
                {
                    _toastService.ShowError("Hata", res.Error ?? "Kayıt oluşturulamadı.");
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

        private async Task CheckDraftAsync()
        {
            try
            {
                var res = await _commandService.GetDraftAsync();
                if (res.IsSuccess && !string.IsNullOrWhiteSpace(res.Value))
                {
                    var draft = System.Text.Json.JsonSerializer.Deserialize<CreateCustomerInteractionDto>(res.Value);
                    if (draft != null)
                    {
                        CallerName = draft.CallerName ?? string.Empty;
                        CallerPhone = draft.CallerPhone ?? string.Empty;
                        Subject = draft.Subject ?? string.Empty;
                        Summary = draft.Summary ?? string.Empty;
                        DetailedNotes = draft.DetailedNotes ?? string.Empty;
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// Tüm form alanlarını varsayılan değerlerine sıfırlar.
        /// Inline kullanımda kayıt sonrası çağrılır.
        /// </summary>
        public void ResetForm()
        {
            CallerPhone = string.Empty;
            CallerName = string.Empty;
            Subject = string.Empty;
            Summary = string.Empty;
            DetailedNotes = string.Empty;
            SelectedChannel = InteractionChannel.Phone;
            SelectedRequestType = InteractionRequestType.PriceQuote;
            SelectedPriority = InteractionPriority.Normal;
            RequiresFollowUp = false;
            FollowUpDate = DateTime.Now.AddDays(1);
            RequiresManagerAttention = false;
            ManagerNotes = string.Empty;
            SelectedUser = null;
            SelectedCustomerMatch = null;
            _selectedCustomerId = null;
            _selectedCustomerName = string.Empty;
            PhoneMatches.Clear();
            SaveCommand.NotifyCanExecuteChanged();
        }
    }
}
