using ArturRios.Data.MySql;
using ArturRios.Data.Relational.Core.Configuration;
using ArturRios.Data.Relational.Core.Providers;
using Microsoft.Extensions.DependencyInjection;

namespace ArturRios.Data.Tests.Providers;

[Trait("Category", "Unit")]
public class MySqlProviderTests
{
    [Fact]
    public void GivenTheMySqlProvider_WhenInspected_ThenItsTypeIsMySql()
    {
        Assert.Equal(DatabaseType.MySql, new MySqlProvider().Type);
    }

    [Fact]
    public void GivenAServiceCollection_WhenAddingTheMySqlProvider_ThenItIsRegisteredAsASingletonDatabaseProvider()
    {
        var services = new ServiceCollection();

        var returned = services.AddMySqlProvider();
        var descriptor = Assert.Single(services);

        Assert.Same(services, returned);
        Assert.Equal(typeof(IDatabaseProvider), descriptor.ServiceType);
        Assert.Equal(typeof(MySqlProvider), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    [Fact]
    public void GivenTheMySqlProviderIsRegistered_WhenResolved_ThenItIsTheMySqlProvider()
    {
        var services = new ServiceCollection();
        services.AddMySqlProvider();

        using var serviceProvider = services.BuildServiceProvider();
        var resolved = serviceProvider.GetRequiredService<IDatabaseProvider>();

        Assert.IsType<MySqlProvider>(resolved);
        Assert.Equal(DatabaseType.MySql, resolved.Type);
    }
}
