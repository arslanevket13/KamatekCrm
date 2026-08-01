using KamatekCrm.Infrastructure.Data;
using KamatekCrm.Shared.Models;
using KamatekCrm.Views;
using Microsoft.EntityFrameworkCore;

namespace KamatekCrm.Services;

public sealed class ForcedPasswordChangeService : IForcedPasswordChangeService
{
    private readonly IAuthService _authService;
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;

    public ForcedPasswordChangeService(
        IAuthService authService,
        IDbContextFactory<AppDbContext> dbContextFactory)
    {
        _authService = authService;
        _dbContextFactory = dbContextFactory;
    }

    public Task<bool> RequireChangeAsync(User user)
    {
        var window = new PasswordResetView(user, _authService, _dbContextFactory)
        {
            Title = "Geçici Parolayı Değiştir"
        };

        if (System.Windows.Application.Current?.MainWindow is { } owner && owner != window)
            window.Owner = owner;

        return Task.FromResult(window.ShowDialog() == true);
    }
}
