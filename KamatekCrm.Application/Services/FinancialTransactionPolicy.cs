using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;

namespace KamatekCrm.ApplicationCore.Services;

public static class FinancialTransactionPolicy
{
    public static readonly CashTransactionType[] CashIncomeTypes =
    [
        CashTransactionType.Income,
        CashTransactionType.CashIncome,
        CashTransactionType.CardIncome,
        CashTransactionType.TransferIncome
    ];

    public static readonly CashTransactionType[] CashExpenseTypes =
    [
        CashTransactionType.Expense,
        CashTransactionType.CashExpense,
        CashTransactionType.CardExpense,
        CashTransactionType.TransferExpense
    ];

    public static bool IsCashIncome(CashTransactionType type) => CashIncomeTypes.Contains(type);

    public static bool IsCashExpense(CashTransactionType type) => CashExpenseTypes.Contains(type);

    public static CashTransactionType ToIncomeType(PaymentMethod method) => method switch
    {
        PaymentMethod.Cash => CashTransactionType.CashIncome,
        PaymentMethod.CreditCard or PaymentMethod.MobilePayment => CashTransactionType.CardIncome,
        PaymentMethod.BankTransfer or PaymentMethod.Check => CashTransactionType.TransferIncome,
        _ => CashTransactionType.Income
    };

    public static CashTransactionType ToExpenseType(PaymentMethod method) => method switch
    {
        PaymentMethod.Cash => CashTransactionType.CashExpense,
        PaymentMethod.CreditCard or PaymentMethod.MobilePayment => CashTransactionType.CardExpense,
        PaymentMethod.BankTransfer or PaymentMethod.Check => CashTransactionType.TransferExpense,
        _ => CashTransactionType.Expense
    };

    public static decimal CalculateCustomerBalance(IEnumerable<Transaction> transactions) =>
        transactions.Sum(transaction => transaction.Type switch
        {
            TransactionType.Debt or TransactionType.Refund => transaction.Amount,
            TransactionType.Payment or TransactionType.CreditNote => -transaction.Amount,
            _ => 0m
        });
}
