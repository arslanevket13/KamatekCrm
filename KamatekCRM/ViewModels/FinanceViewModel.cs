using KamatekCrm.Helpers;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using KamatekCrm.Infrastructure.Data;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;
using KamatekCrm.Shared.Services;
using KamatekCrm.Services;
using Microsoft.EntityFrameworkCore;
using KamatekCrm.ApplicationCore.Services;

namespace KamatekCrm.ViewModels
{
    /// <summary>
    /// Finans / Kasa Modülü ViewModel
    /// Günlük gelir/gider takibi ve gün sonu raporu
    /// </summary>
    public partial class FinanceViewModel : ViewModelBase
    {
        private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
        private readonly IAuthService _authService;
        private readonly IToastService? _toastService;
        private readonly IDialogService? _dialogService;

        #region Properties

        public ObservableCollection<CashTransaction> Transactions { get; } = new();
        public ICollectionView FilteredTransactions { get; private set; }

        private DateTime _selectedDate = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);
        public DateTime SelectedDate
        {
            get => _selectedDate;
            set
            {
                var normalized = value.Kind == DateTimeKind.Utc
                    ? value
                    : DateTime.SpecifyKind(value.Date, DateTimeKind.Utc);
                if (SetProperty(ref _selectedDate, normalized))
                {
                    OnPropertyChanged(nameof(SelectedDateDisplay));
                    RefreshAsync().SafeFireAndForget("FinanceViewModel.RefreshAsync");
                }
            }
        }

        public string SelectedDateDisplay => ShowMonthly 
            ? _selectedDate.ToString("MMMM yyyy", new System.Globalization.CultureInfo("tr-TR"))
            : _selectedDate.ToString("dd MMMM yyyy, dddd", new System.Globalization.CultureInfo("tr-TR"));

        private bool _showMonthly = false;
        public bool ShowMonthly
        {
            get => _showMonthly;
            set
            {
                if (SetProperty(ref _showMonthly, value))
                {
                    OnPropertyChanged(nameof(SelectedDateDisplay));
                    RefreshAsync().SafeFireAndForget("FinanceViewModel.RefreshAsync");
                }
            }
        }

        private string _filterText = string.Empty;
        public string FilterText
        {
            get => _filterText;
            set
            {
                if (SetProperty(ref _filterText, value))
                {
                    FilteredTransactions?.Refresh();
                }
            }
        }

        // Özet Metrikleri
        private decimal _cashIncome;
        public decimal CashIncome
        {
            get => _cashIncome;
            set => SetProperty(ref _cashIncome, value);
        }

        private decimal _cardIncome;
        public decimal CardIncome
        {
            get => _cardIncome;
            set => SetProperty(ref _cardIncome, value);
        }

        private decimal _totalExpense;
        public decimal TotalExpense
        {
            get => _totalExpense;
            set => SetProperty(ref _totalExpense, value);
        }

        private decimal _carriedOverBalance;
        public decimal CarriedOverBalance
        {
            get => _carriedOverBalance;
            set => SetProperty(ref _carriedOverBalance, value);
        }

        public decimal DailyNetBalance => FinancialCalculator.RoundMoney((CashIncome + CardIncome) - TotalExpense);
        public decimal TotalVaultBalance => FinancialCalculator.RoundMoney(CarriedOverBalance + DailyNetBalance);
        public string NetBalanceDisplay => $"₺{TotalVaultBalance:N2}";
        public string CarriedOverDisplay => $"₺{CarriedOverBalance:N2}";
        public System.Windows.Media.Brush NetBalanceColor => ThemeService.ResolveBrush(TotalVaultBalance >= 0 ? "StatusSuccessFg" : "StatusErrorFg");

        // Yeni Gider Ekleme Formu
        private decimal _newExpenseAmount;
        public decimal NewExpenseAmount
        {
            get => _newExpenseAmount;
            set
            {
                if (SetProperty(ref _newExpenseAmount, value))
                {
                    AddExpenseCommand.NotifyCanExecuteChanged();
                }
            }
        }

        private string _newExpenseDescription = string.Empty;
        public string NewExpenseDescription
        {
            get => _newExpenseDescription;
            set
            {
                if (SetProperty(ref _newExpenseDescription, value))
                {
                    AddExpenseCommand.NotifyCanExecuteChanged();
                }
            }
        }

        private string _newExpenseCategory = "Genel";
        public string NewExpenseCategory
        {
            get => _newExpenseCategory;
            set => SetProperty(ref _newExpenseCategory, value);
        }

        public string[] ExpenseCategories { get; } = new[]
        {
            "Genel",
            "Personel / Maaş",
            "Yol / Ulaşım",
            "Yemek / Temsil",
            "Malzeme / Demirbaş",
            "Kira / Fatura",
            "Vergi / Harç",
            "Diğer"
        };

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        #endregion

        #region Commands

        #endregion

        #region Constructor

        public FinanceViewModel(
            IAuthService authService,
            IDbContextFactory<AppDbContext> dbContextFactory,
            IToastService? toastService = null,
            IDialogService? dialogService = null)
        {
            _authService = authService;
            _dbContextFactory = dbContextFactory;
            _toastService = toastService;
            _dialogService = dialogService;

            FilteredTransactions = CollectionViewSource.GetDefaultView(Transactions);
            FilteredTransactions.Filter = FilterTransactions;
            FilteredTransactions.SortDescriptions.Add(new SortDescription(nameof(CashTransaction.Date), ListSortDirection.Descending));

            RefreshAsync().SafeFireAndForget("FinanceViewModel.RefreshAsync");
        }

        #endregion

        #region Methods

        [RelayCommand]
        private async System.Threading.Tasks.Task RefreshAsync()
        {
            IsBusy = true;
            try
            {
                Transactions.Clear();

                DateTime startDate, endDate;

                if (ShowMonthly)
                {
                    startDate = new DateTime(SelectedDate.Year, SelectedDate.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                    endDate = startDate.AddMonths(1).AddTicks(-1);
                }
                else
                {
                    startDate = DateTime.SpecifyKind(SelectedDate.Date, DateTimeKind.Utc);
                    endDate = startDate.AddDays(1).AddTicks(-1);
                }

                await using var context = await _dbContextFactory.CreateDbContextAsync();

                // Devir Bakiye (startDate öncesindeki tüm kasa hareketlerinin net bakiyesi)
                var pastTransactions = await context.CashTransactions
                    .Where(t => t.Date < startDate).Select(t => new { t.TransactionType, t.Amount })
                    .AsNoTracking().ToListAsync();

                var pastIncome = pastTransactions.Where(t => FinancialTransactionPolicy.IsCashIncome(t.TransactionType)).Sum(t => t.Amount);

                var pastExpense = pastTransactions.Where(t => FinancialTransactionPolicy.IsCashExpense(t.TransactionType)).Sum(t => t.Amount);

                CarriedOverBalance = pastIncome - pastExpense;

                var transactions = await context.CashTransactions
                    .Include(t => t.Customer)
                    .Where(t => t.Date >= startDate && t.Date <= endDate)
                    .OrderByDescending(t => t.Date)
                    .AsNoTracking().ToListAsync();

                foreach (var t in transactions)
                {
                    Transactions.Add(t);
                }

                // Özet hesapla
                CashIncome = transactions
                    .Where(t => FinancialTransactionPolicy.IsCashIncome(t.TransactionType) && t.TransactionType != CashTransactionType.CardIncome)
                    .Sum(t => t.Amount);

                CardIncome = transactions
                    .Where(t => t.TransactionType == CashTransactionType.CardIncome)
                    .Sum(t => t.Amount);

                TotalExpense = transactions.Where(t => FinancialTransactionPolicy.IsCashExpense(t.TransactionType)).Sum(t => t.Amount);

                OnPropertyChanged(nameof(DailyNetBalance));
                OnPropertyChanged(nameof(TotalVaultBalance));
                OnPropertyChanged(nameof(NetBalanceDisplay));
                OnPropertyChanged(nameof(CarriedOverDisplay));
                OnPropertyChanged(nameof(NetBalanceColor));
            }
            catch (Exception ex)
            {
                _toastService?.ShowError("Hata", $"Veri yüklenirken hata: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private bool FilterTransactions(object obj)
        {
            if (string.IsNullOrWhiteSpace(FilterText)) return true;

            if (obj is CashTransaction t)
            {
                var search = FilterText.ToLowerInvariant();
                return (t.Description?.ToLowerInvariant().Contains(search) ?? false)
                    || (t.Category?.ToLowerInvariant().Contains(search) ?? false)
                    || (t.Customer?.FullName?.ToLowerInvariant().Contains(search) ?? false)
                    || (t.ReferenceNumber?.ToLowerInvariant().Contains(search) ?? false);
            }
            return false;
        }

        private bool CanAddExpense()
        {
            return _authService.CanViewFinance &&
                   NewExpenseAmount > 0 &&
                   !string.IsNullOrWhiteSpace(NewExpenseDescription);
        }

        [RelayCommand(CanExecute = nameof(CanAddExpense))]
        private void AddExpense()
        {
            if (!_authService.CanViewFinance)
            {
                _toastService?.ShowWarning("Yetkisiz işlem", "Finansal işlem oluşturma yetkiniz yok.");
                return;
            }

            try
            {
                var expense = new CashTransaction
                {
                    Date = DateTime.UtcNow,
                    Amount = NewExpenseAmount,
                    TransactionType = CashTransactionType.Expense,
                    Description = NewExpenseDescription,
                    Category = NewExpenseCategory,
                    CreatedBy = _authService.CurrentUser?.AdSoyad ?? "Sistem",
                    CreatedAt = DateTime.UtcNow
                };

                using var context = _dbContextFactory.CreateDbContext();
                context.CashTransactions.Add(expense);
                context.SaveChanges();

                _toastService?.ShowSuccess("Başarılı", $"Gider kaydedildi: ₺{NewExpenseAmount:N2}");

                // Formu temizle
                NewExpenseAmount = 0;
                NewExpenseDescription = string.Empty;
                NewExpenseCategory = "Genel";

                RefreshAsync().SafeFireAndForget("FinanceViewModel.RefreshAsync");
            }
            catch (Exception ex)
            {
                _toastService?.ShowError("Hata", $"Gider eklenirken hata: {ex.Message}");
            }
        }

        private bool CanDeleteTransaction(object? parameter)
        {
            return parameter is CashTransaction && _authService.IsAdmin;
        }

        [RelayCommand(CanExecute = nameof(CanDeleteTransaction))]
        private async System.Threading.Tasks.Task DeleteTransaction(object? parameter)
        {
            if (parameter is not CashTransaction transaction) return;
            if (!_authService.IsAdmin)
            {
                _toastService?.ShowWarning("Yetkisiz işlem", "Finansal kayıt silmek için yönetici yetkisi gerekir.");
                return;
            }

            bool confirmed = _dialogService != null && await _dialogService.ShowConfirmationAsync(
                $"Bu işlemi silmek istediğinize emin misiniz?\n\n{transaction.Description}\nTutar: ₺{transaction.Amount:N2}",
                "Silme Onayı");

            if (!confirmed) return;

            try
            {
                using var context = _dbContextFactory.CreateDbContext();
                context.CashTransactions.Remove(transaction);
                context.SaveChanges();
                RefreshAsync().SafeFireAndForget("FinanceViewModel.RefreshAsync");
            }
            catch (Exception ex)
            {
                _toastService?.ShowError("Hata", $"Silme hatası: {ex.Message}");
            }
        }

        [RelayCommand]
        private void PreviousDay() => SelectedDate = SelectedDate.AddDays(-1);

        [RelayCommand]
        private void NextDay() => SelectedDate = SelectedDate.AddDays(1);

        [RelayCommand]
        private void GoToToday() => SelectedDate = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);

        #endregion
    }
}

