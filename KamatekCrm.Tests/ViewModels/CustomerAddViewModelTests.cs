using System.ComponentModel;
using FluentAssertions;
using KamatekCrm.Infrastructure.Data;
using KamatekCrm.Shared.Enums;
using KamatekCrm.ViewModels;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace KamatekCrm.Tests.ViewModels;

public class CustomerAddViewModelTests
{
    private static CustomerAddViewModel CreateViewModel()
    {
        var factory = new Mock<IDbContextFactory<AppDbContext>>();
        return new CustomerAddViewModel(factory.Object);
    }

    [Fact]
    public void SaveCommand_RequiresValidNameAndPhone()
    {
        var viewModel = CreateViewModel();

        viewModel.SaveCommand.CanExecute(null).Should().BeFalse();

        viewModel.FullName = "Ayşe Yılmaz";
        viewModel.PhoneNumber = "0532 123 45 67";

        viewModel.HasErrors.Should().BeFalse();
        viewModel.SaveCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public void InvalidEmail_ReportsInlineErrorAndDisablesSave()
    {
        var viewModel = CreateViewModel();
        viewModel.FullName = "Ayşe Yılmaz";
        viewModel.PhoneNumber = "0532 123 45 67";
        viewModel.Email = "gecersiz-adres";

        var errors = ((INotifyDataErrorInfo)viewModel).GetErrors(nameof(viewModel.Email));

        errors.Cast<object>().Should().ContainSingle();
        viewModel.HasErrors.Should().BeTrue();
        viewModel.SaveCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void CorporateCustomer_RequiresCompanyNameOnlyWhileCorporate()
    {
        var viewModel = CreateViewModel();
        viewModel.FullName = "Satın Alma Yetkilisi";
        viewModel.PhoneNumber = "0212 555 44 33";
        viewModel.NewCustomerType = CustomerType.Corporate;

        viewModel.HasErrors.Should().BeTrue();
        viewModel.SaveCommand.CanExecute(null).Should().BeFalse();

        viewModel.NewCompanyName = "Kamatek Teknoloji A.Ş.";

        viewModel.HasErrors.Should().BeFalse();
        viewModel.SaveCommand.CanExecute(null).Should().BeTrue();

        viewModel.NewCompanyName = null;
        viewModel.NewCustomerType = CustomerType.Individual;

        viewModel.HasErrors.Should().BeFalse();
        viewModel.SaveCommand.CanExecute(null).Should().BeTrue();
    }
}
