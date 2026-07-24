using System;
using FluentAssertions;
using KamatekCrm.Data;
using KamatekCrm.Services;
using KamatekCrm.Shared.Models;
using KamatekCrm.ViewModels;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace KamatekCrm.Tests.ViewModels
{
    public class AddProductViewModelTests
    {
        private readonly Mock<IDbContextFactory<AppDbContext>> _dbContextFactoryMock;
        private readonly Mock<IProductImageService> _imageServiceMock;

        public AddProductViewModelTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _dbContextFactoryMock = new Mock<IDbContextFactory<AppDbContext>>();
            _dbContextFactoryMock.Setup(f => f.CreateDbContext()).Returns(() => new AppDbContext(options));
            _dbContextFactoryMock.Setup(f => f.CreateDbContextAsync(default)).ReturnsAsync(() => new AppDbContext(options));

            _imageServiceMock = new Mock<IProductImageService>();
        }

        [Fact]
        public void SaveCommand_ShouldNotBeExecutable_WhenProductNameIsEmpty()
        {
            // Arrange
            var viewModel = new AddProductViewModel(_dbContextFactoryMock.Object, _imageServiceMock.Object);
            viewModel.Initialize(null);

            viewModel.NewProduct.ProductName = string.Empty;

            // Act
            bool canExecute = viewModel.SaveCommand.CanExecute(null);

            // Assert
            canExecute.Should().BeFalse();
        }

        [Fact]
        public void SaveCommand_ShouldNotBeExecutable_WhenSalePriceIsNegative()
        {
            // Arrange
            var viewModel = new AddProductViewModel(_dbContextFactoryMock.Object, _imageServiceMock.Object);
            viewModel.Initialize(null);

            viewModel.NewProduct.ProductName = "Test Product";
            viewModel.NewProduct.SalePrice = -10m;

            // Act
            bool canExecute = viewModel.SaveCommand.CanExecute(null);

            // Assert
            canExecute.Should().BeFalse();
        }

        [Fact]
        public void SaveCommand_ShouldBeExecutable_WhenProductNameAndSalePriceAreValid()
        {
            // Arrange
            var viewModel = new AddProductViewModel(_dbContextFactoryMock.Object, _imageServiceMock.Object);
            viewModel.Initialize(null);

            viewModel.NewProduct.ProductName = "IP Kamera 4MP";
            viewModel.NewProduct.SalePrice = 1250.50m;

            // Act
            bool canExecute = viewModel.SaveCommand.CanExecute(null);

            // Assert
            canExecute.Should().BeTrue();
        }
    }
}
