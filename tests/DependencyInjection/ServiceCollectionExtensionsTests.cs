using ArturRios.Data.Relational.Core.Configuration;
using ArturRios.Data.Relational.Core.DependencyInjection;
using ArturRios.Data.Relational.Core.Exceptions;
using ArturRios.Data.Relational.Core.Interfaces;
using ArturRios.Data.Relational.Core.Providers;
using ArturRios.Data.Relational.Core.Transactions;
using ArturRios.Data.Tests.TestSupport;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArturRios.Data.Tests.DependencyInjection;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddArturRiosData_RegistersRepositoriesAndUnitOfWork()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        connection.Open();

        var services = new ServiceCollection();
        services.AddSingleton<IDatabaseProvider>(new FakeSqliteProvider(connection));
        services.AddDataConfig<TestDbContext>(new BaseDbContextOptions
        {
            DatabaseType = DatabaseType.SqLite, ConnectionString = "Filename=:memory:"
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
            services.AddDataConfig<TestDbContext>(new BaseDbContextOptions { DatabaseType = DatabaseType.SqLite }));
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
                DatabaseType = DatabaseType.SqLite, ConnectionString = "Filename=:memory:"
            }));

        Assert.Null(exception);

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<TestDbContext>().Database.EnsureCreated();

        Assert.NotNull(provider.GetRequiredService<IRepository<TestEntity>>());
    }

    [Fact]
    public void GivenEnvVarsSet_WhenAddDataConfigFromEnvironment_ThenRegistersRepositoriesAndUnitOfWork()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        connection.Open();

        const string prefix = "ARTURRIOS_TEST";
        Environment.SetEnvironmentVariable($"{prefix}_DATABASETYPE", "SqLite");
        Environment.SetEnvironmentVariable($"{prefix}_CONNECTIONSTRING", "Filename=:memory:");

        try
        {
            var services = new ServiceCollection();
            services.AddSingleton<IDatabaseProvider>(new FakeSqliteProvider(connection));
            services.AddDataConfigFromEnvironment<TestDbContext>(prefix);

            using var provider = services.BuildServiceProvider();
            provider.GetRequiredService<TestDbContext>().Database.EnsureCreated();

            Assert.NotNull(provider.GetRequiredService<IRepository<TestEntity>>());
            Assert.NotNull(provider.GetRequiredService<IAsyncRepository<TestEntity>>());
            Assert.NotNull(provider.GetRequiredService<IUnitOfWork>());
            Assert.NotNull(provider.GetRequiredService<IAsyncUnitOfWork>());
        }
        finally
        {
            Environment.SetEnvironmentVariable($"{prefix}_DATABASETYPE", null);
            Environment.SetEnvironmentVariable($"{prefix}_CONNECTIONSTRING", null);
        }
    }

    [Fact]
    public void GivenDatabaseTypeEnvVarUnset_WhenAddDataConfigFromEnvironment_ThenThrowsDataAccessException()
    {
        const string prefix = "ARTURRIOS_TEST_UNSET";
        Environment.SetEnvironmentVariable($"{prefix}_DATABASETYPE", null);

        var services = new ServiceCollection();

        Assert.Throws<DataAccessException>(() =>
            services.AddDataConfigFromEnvironment<TestDbContext>(prefix));
    }

    [Fact]
    public void GivenDatabaseTypeEnvVarInvalid_WhenAddDataConfigFromEnvironment_ThenThrowsDataAccessException()
    {
        const string prefix = "ARTURRIOS_TEST_INVALID";
        Environment.SetEnvironmentVariable($"{prefix}_DATABASETYPE", "NotARealDb");

        try
        {
            var services = new ServiceCollection();

            Assert.Throws<DataAccessException>(() =>
                services.AddDataConfigFromEnvironment<TestDbContext>(prefix));
        }
        finally
        {
            Environment.SetEnvironmentVariable($"{prefix}_DATABASETYPE", null);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GivenNullOrWhitespacePrefix_WhenAddDataConfigFromEnvironment_ThenThrowsArgumentException(string? prefix)
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>(() =>
            services.AddDataConfigFromEnvironment<TestDbContext>(prefix!));
    }

    // Minimal in-test provider so the core DI test does not depend on a provider package.
    private sealed class FakeSqliteProvider(SqliteConnection connection) : IDatabaseProvider
    {
        public DatabaseType Type => DatabaseType.SqLite;

        public void Configure(DbContextOptionsBuilder builder, string connectionString) =>
            builder.UseSqlite(connection);
    }
}
