using FluentAssertions;
using KamatekCrm.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Xunit;

namespace KamatekCrm.Tests.Services;

public class DatabaseConnectionProviderTests
{
    [Fact]
    public void GetConnectionString_ReadsExistingNestedConfiguration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DatabaseSettings:ConnectionStrings:PostgreSQL"] =
                    "Host=127.0.0.1;Port=5432;Database=crm;Username=user;Password=secret"
            })
            .Build();

        using var provider = new DatabaseConnectionProvider(configuration);
        var result = new NpgsqlConnectionStringBuilder(provider.GetConnectionString());

        result.Host.Should().Be("127.0.0.1");
        result.Database.Should().Be("crm");
        result.Username.Should().Be("user");
    }

    [Fact]
    public void SetServerIp_ChangesOnlyHostAndPreservesCredentials()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:PostgreSQL"] =
                    "Host=old-server;Port=5433;Database=crm;Username=user;Password=secret"
            })
            .Build();
        using var provider = new DatabaseConnectionProvider(configuration);

        provider.SetServerIp("10.0.0.25");
        var result = new NpgsqlConnectionStringBuilder(provider.GetConnectionString());

        result.Host.Should().Be("10.0.0.25");
        result.Port.Should().Be(5433);
        result.Database.Should().Be("crm");
        result.Username.Should().Be("user");
        result.Password.Should().Be("secret");
    }
}
