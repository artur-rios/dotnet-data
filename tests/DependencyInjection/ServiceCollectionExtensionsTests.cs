using System.Linq;
using ArturRios.Data;
using ArturRios.Data.Configuration;
using ArturRios.Data.DependencyInjection;
using ArturRios.Data.Exceptions;
using ArturRios.Data.Interfaces;
using ArturRios.Data.Providers;
using ArturRios.Data.Tests.TestSupport;
using ArturRios.Data.Transactions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArturRios.Data.Tests.DependencyInjection;

public class ServiceCollectionExtensionsTests
{
    // Minimal in-test provider so the core DI test does not depend on a provider package.
    private sealed class FakeSqliteProvider : IDatabaseProvider
    {
        private readonly SqliteConnection _connection;
        public FakeSqliteProvider(SqliteConnection connection) => _connection = connection;
        public DatabaseType Type => DatabaseType.SQLite;
        public void Configure(DbContextOptionsBuilder builder, string connectionString) =>
            builder.UseSqlite(_connection);
    }

    [Fact]
    public void AddArturRiosData_RegistersRepositoriesAndUnitOfWork()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        connection.Open();

        var services = new ServiceCollection();
        services.AddSingleton<IDatabaseProvider>(new FakeSqliteProvider(connection));
        services.AddArturRiosData<TestDbContext>(new BaseDbContextOptions
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
            services.AddArturRiosData<TestDbContext>(new BaseDbContextOptions
            {
                DatabaseType = DatabaseType.SQLite
            }));
    }
}
