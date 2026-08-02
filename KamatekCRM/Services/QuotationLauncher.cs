using KamatekCrm.ViewModels;
using KamatekCrm.Views;
using Microsoft.Extensions.DependencyInjection;

namespace KamatekCrm.Services;

public interface IQuotationLauncher
{
    Task ShowAsync(int? customerId = null, bool modal = true);
}

public sealed class QuotationLauncher : IQuotationLauncher
{
    private readonly IServiceProvider _serviceProvider;

    public QuotationLauncher(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task ShowAsync(int? customerId = null, bool modal = true)
    {
        var window = _serviceProvider.GetRequiredService<QuotationWindow>();
        window.Owner = System.Windows.Application.Current?.MainWindow;
        if (customerId.HasValue && window.DataContext is QuotationViewModel viewModel)
            await viewModel.SelectCustomerByIdAsync(customerId.Value);

        if (modal) window.ShowDialog();
        else window.Show();
    }
}
