using KamatekCrm.ViewModels;
using KamatekCrm.Views;
using Microsoft.Extensions.DependencyInjection;

namespace KamatekCrm.Services;

public interface IProjectQuoteEditorLauncher
{
    void ShowNew();
    void ShowEdit(int projectId);
}

/// <summary>
/// Pencere oluşturma ayrıntısını ViewModel'lerden ayıran masaüstü adaptörü.
/// </summary>
public sealed class ProjectQuoteEditorLauncher : IProjectQuoteEditorLauncher
{
    private readonly IServiceProvider _serviceProvider;

    public ProjectQuoteEditorLauncher(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public void ShowNew() => CreateWindow().ShowDialog();

    public void ShowEdit(int projectId)
    {
        var window = CreateWindow();
        if (window.DataContext is ProjectQuoteEditorViewModel viewModel)
            viewModel.LoadExistingProject(projectId);
        window.ShowDialog();
    }

    private ProjectQuoteEditorWindow CreateWindow() =>
        _serviceProvider.GetRequiredService<ProjectQuoteEditorWindow>();
}
