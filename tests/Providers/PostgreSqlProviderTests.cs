using ArturRios.Data.PostgreSql;
using ArturRios.Data.Relational.Core.Configuration;
using ArturRios.Data.Relational.Core.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArturRios.Data.Tests.Providers;

[Trait("Category", "Unit")]
public class PostgreSqlProviderTests
{
    [Fact]
    public void GivenThePostgreSqlProvider_WhenInspected_ThenItsTypeIsPostgreSql() => Assert.Equal(DatabaseType.PostgreSql, new PostgreSqlProvider().Type);

    [Fact]
    public void GivenABuilder_WhenThePostgreSqlProviderConfiguresIt_ThenTheNpgsqlProviderIsSelected()
    {
        var builder = new DbContextOptionsBuilder();
        new PostgreSqlProvider().Configure(builder, "Host=localhost;Database=mydb;Username=app;Password=secret;");
        Assert.Contains(builder.Options.Extensions, e => e.GetType().Name.Contains("Npgsql"));
    }

    [Fact]
    public void GivenAServiceCollection_WhenAddingThePostgreSqlProvider_ThenItIsRegisteredAsASingletonDatabaseProvider()
    {
        var services = new ServiceCollection();

        var returned = services.AddPostgreSqlProvider();
        var descriptor = Assert.Single(services);

        Assert.Same(services, returned);
        Assert.Equal(typeof(IDatabaseProvider), descriptor.ServiceType);
        Assert.Equal(typeof(PostgreSqlProvider), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    [Fact]
    public void GivenThePostgreSqlProviderIsRegistered_WhenResolved_ThenItIsThePostgreSqlProvider()
    {
        var services = new ServiceCollection();
        services.AddPostgreSqlProvider();

        using var serviceProvider = services.BuildServiceProvider();
        var resolved = serviceProvider.GetRequiredService<IDatabaseProvider>();

        Assert.IsType<PostgreSqlProvider>(resolved);
        Assert.Equal(DatabaseType.PostgreSql, resolved.Type);
    }
}
