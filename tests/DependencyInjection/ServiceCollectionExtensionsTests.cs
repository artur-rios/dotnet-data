using ArturRios.Data.Core.Configuration;
using ArturRios.Data.Core.DependencyInjection;
using ArturRios.Data.Core.Exceptions;
using ArturRios.Data.Core.Interfaces;
using ArturRios.Data.Core.Providers;
using ArturRios.Data.Core.Transactions;
using ArturRios.Data.Tests.TestSupport;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArturRios.Data.Tests.DependencyInjection;

public class ServiceCollectionExtensionsTests
{
    // Minimal in-test provider so the core DI test does not depend on a provider package.
    private sealed class FakeSqliteProvider(SqliteConnection connection) : IDatabaseProvider
    {
        public DatabaseType Type => DatabaseType.SQLite;
        public void Configure(DbContextOptionsBuilder builder, string connectionString) =>
            builder.UseSqlite(connection);
    }

    [Fact]
    public void AddArturRiosData_RegistersRepositoriesAndUnitOfWork()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        connection.Open();

        var services = new ServiceCollection();
        services.AddSingleton<IDatabaseProvider>(new FakeSqliteProvider(connection));
        services.AddDataConfig<TestDbContext>(new BaseDbContextOptions
        {
            DatabaseType = DatabaseType.SQLite,
            ConnectionString = "Filename=:memory:"
        });

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<TestDbContext>().Database.EnsureCreated();

        Assert.NotNull(provider.GetRequiredService<IRepository<TestEntity>>());
        Assert.NotNull(provider.GetRequiredService<IAsyncRepository<TestEntity>>());
        Assert.NotNull(provider.GetRequiredService<IUnitOfWork>());
        Assert.NotNull(provider.GetRequiredService<IAsyncUnitOfWork>());
    }

    [Fact]
    public void AddArturRiosData_Throws_WhenProviderMissing()
    {
        var services = new ServiceCollection(); // no IDatabaseProvider registered

        Assert.Throws<DataAccessException>(() =>
            services.AddDataConfig<TestDbContext>(new BaseDbContextOptions
            {
                DatabaseType = DatabaseType.SQLite
            }));
    }

    [Fact]
    public void AddArturRiosData_DoesNotThrow_WhenProviderRegisteredViaFactory()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        connection.Open();

        var services = new ServiceCollection();
        // Factory registration: ImplementationInstance and ImplementationType are both null,
        // so the eager validation cannot inspect the descriptor and must defer to resolution time.
        services.AddSingleton<IDatabaseProvider>(_ => new FakeSqliteProvider(connection));

        var exception = Record.Exception(() =>
            services.AddDataConfig<TestDbContext>(new BaseDbContextOptions
            {
                DatabaseType = DatabaseType.SQLite,
                ConnectionString = "Filename=:memory:"
            }));

        Assert.Null(exception);

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<TestDbContext>().Database.EnsureCreated();

        Assert.NotNull(provider.GetRequiredService<IRepository<TestEntity>>());
    }
}
