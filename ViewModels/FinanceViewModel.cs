using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using KamatekCrm.Commands;
using KamatekCrm.Data;
using KamatekCrm.Enums;
using KamatekCrm.Models;
using KamatekCrm.Services;
using Microsoft.EntityFrameworkCore;

namespace KamatekCrm.ViewModels
{
    /// <summary>
    /// Finans / Kasa Modülü ViewModel
    /// Günlük gelir/gider takibi ve gün sonu raporu
    /// </summary>
    public class FinanceViewModel : ViewModelBase
    {
        private readonly AppDbContext _context;

        #region Properties

        public ObservableCollection<CashTransaction> Transactions { get; } = new();
        public ICollectionView FilteredTransactions { get; private set; }

        private DateTime _selectedDate = DateTime.Today;
        public DateTime SelectedDate
        {
            get => _selectedDate;
            set
            {
                if (SetProperty(ref _selectedDate, value))
                {
                    LoadData();
                }
            }
        }

        private bool _showMonthly = false;
        public bool ShowMonthly
        {
            get => _showMonthly;
            set
            {
                if (SetProperty(ref _showMonthly, value))
                {
                    LoadData();
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

        // Özet Kartları
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

        public decimal NetBalance => CashIncome + CardIncome - TotalExpense;
        public string NetBalanceDisplay => $"₺{NetBalance:N2}";
        public string NetBalanceColor => NetBalance >= 0 ? "#4CAF50" : "#F44336";

        // Gider Ekleme Formu
        private decimal _newExpenseAmount;
        public decimal NewExpenseAmount
        {
            get => _newExpenseAmount;
            set => SetProperty(ref _newExpenseAmount, value);
        }

        private string _newExpenseDescription = string.Empty;
        public string NewExpenseDescription
        {
            get => _newExpenseDescription;
            set => SetProperty(ref _newExpenseDescription, value);
        }

        private string _newExpenseCategory = "Genel";
        public string NewExpenseCategory
        {
            get => _newExpenseCategory;
            set => SetProperty(ref _newExpenseCategory, value);
        }

        public ObservableCollection<string> ExpenseCategories { get; } = new()
        {
            "Genel",
            "Fatura",
            "Kira",
            "Malzeme Alımı",
            "Personel",
            "Yakıt",
            "Yemek",
            "Nakliye",
            "Bakım/Onarım",
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

        public ICommand AddExpenseCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand DeleteTransactionCommand { get; }
        public ICommand PreviousDayCommand { get; }
        public ICommand NextDayCommand { get; }
        public ICommand GoToTodayCommand { get; }

        #endregion

        #region Constructor

        public FinanceViewModel()
        {
            _context = new AppDbContext();

            FilteredTransactions = CollectionViewSource.GetDefaultView(Transactions);
            FilteredTransactions.Filter = FilterTransactions;
            FilteredTransactions.SortDescriptions.Add(new SortDescription(nameof(CashTransaction.Date), ListSortDirection.Descending));

            AddExpenseCommand = new RelayCommand(_ => AddExpense(), _ => CanAddExpense());
            RefreshCommand = new RelayCommand(_ => LoadData());
            DeleteTransactionCommand = new RelayCommand(DeleteTransaction, CanDeleteTransaction);
            PreviousDayCommand = new RelayCommand(_ => SelectedDate = SelectedDate.AddDays(-1));
            NextDayCommand = new RelayCommand(_ => SelectedDate = SelectedDate.AddDays(1));
            GoToTodayCommand = new RelayCommand(_ => SelectedDate = DateTime.Today);

            LoadData();
        }

        #endregion

        #region Methods

        private void LoadData()
        {
            IsBusy = true;
            try
            {
                Transactions.Clear();

                DateTime startDate, endDate;

                if (ShowMonthly)
                {
                    startDate = new DateTime(SelectedDate.Year, SelectedDate.Month, 1);
                    endDate = startDate.AddMonths(1).AddTicks(-1); // Ayın son günü 23:59:59.9999999
                }
                else
                {
                    startDate = SelectedDate.Date;
                    endDate = startDate.AddDays(1).AddTicks(-1);
                }

                var transactions = _context.CashTransactions
                    .Include(t => t.Customer)
                    .Where(t => t.Date >= startDate && t.Date <= endDate)
                    .OrderByDescending(t => t.Date)
                    .ToList();

                foreach (var t in transactions)
                {
                    Transactions.Add(t);
                }

                // Özet hesapla
                CashIncome = transactions
                    .Where(t => t.TransactionType == CashTransactionType.CashIncome)
                    .Sum(t => t.Amount);

                CardIncome = transactions
                    .Where(t => t.TransactionType == CashTransactionType.CardIncome)
                    .Sum(t => t.Amount);

                TotalExpense = transactions
                    .Where(t => t.TransactionType == CashTransactionType.Expense || 
                                t.TransactionType == CashTransactionType.TransferExpense)
                    .Sum(t => t.Amount);

                OnPropertyChanged(nameof(NetBalance));
                OnPropertyChanged(nameof(NetBalanceDisplay));
                OnPropertyChanged(nameof(NetBalanceColor));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Veri yüklenirken hata: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
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
            return NewExpenseAmount > 0 && !string.IsNullOrWhiteSpace(NewExpenseDescription);
        }

        private void AddExpense()
        {
            try
            {
                var expense = new CashTransaction
                {
                    Date = DateTime.Now,
                    Amount = NewExpenseAmount,
                    TransactionType = CashTransactionType.Expense,
                    Description = NewExpenseDescription,
                    Category = NewExpenseCategory,
                    CreatedBy = AuthService.CurrentUser?.AdSoyad ?? "Sistem",
                    CreatedAt = DateTime.Now
                };

                _context.CashTransactions.Add(expense);
                _context.SaveChanges();

                MessageBox.Show($"Gider kaydedildi: ₺{NewExpenseAmount:N2}", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);

                // Formu temizle
                NewExpenseAmount = 0;
                NewExpenseDescription = string.Empty;
                NewExpenseCategory = "Genel";

                LoadData();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gider eklenirken hata: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool CanDeleteTransaction(object? parameter)
        {
            return parameter is CashTransaction && AuthService.IsAdmin;
        }

        private void DeleteTransaction(object? parameter)
        {
            if (parameter is not CashTransaction transaction) return;

            var result = MessageBox.Show(
                $"Bu işlemi silmek istediğinize emin misiniz?\n\n{transaction.Description}\nTutar: ₺{transaction.Amount:N2}",
                "Silme Onayı",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                _context.CashTransactions.Remove(transaction);
                _context.SaveChanges();
                LoadData();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Silme hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion
    }
}
