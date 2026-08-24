using ArturRios.Data.Relational.Core.Configuration;
using ArturRios.Data.Relational.Core.Providers;
using ArturRios.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArturRios.Data.Tests.Providers;

[Trait("Category", "Unit")]
public class SqliteProviderTests
{
    [Fact]
    public void GivenTheSqliteProvider_WhenInspected_ThenItsTypeIsSqLite() => Assert.Equal(DatabaseType.SqLite, new SqliteProvider().Type);

    [Fact]
    public void GivenABuilder_WhenTheSqliteProviderConfiguresIt_ThenTheSqliteProviderIsSelected()
    {
        var builder = new DbContextOptionsBuilder();
        new SqliteProvider().Configure(builder, "Filename=:memory:");
        Assert.Contains(builder.Options.Extensions, e => e.GetType().Name.Contains("Sqlite"));
    }

    [Fact]
    public void GivenAServiceCollection_WhenAddingTheSqliteProvider_ThenItIsRegisteredAsASingletonDatabaseProvider()
    {
        var services = new ServiceCollection();

        var returned = services.AddSqliteProvider();
        var descriptor = Assert.Single(services);

        Assert.Same(services, returned);
        Assert.Equal(typeof(IDatabaseProvider), descriptor.ServiceType);
        Assert.Equal(typeof(SqliteProvider), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    [Fact]
    public void GivenTheSqliteProviderIsRegistered_WhenResolved_ThenItIsTheSqliteProvider()
    {
        var services = new ServiceCollection();
        services.AddSqliteProvider();

        using var serviceProvider = services.BuildServiceProvider();
        var resolved = serviceProvider.GetRequiredService<IDatabaseProvider>();

        Assert.IsType<SqliteProvider>(resolved);
        Assert.Equal(DatabaseType.SqLite, resolved.Type);
    }
}
