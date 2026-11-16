using ArturRios.Data.Relational.Core.Configuration;
using ArturRios.Data.Relational.Core.DependencyInjection;
using ArturRios.Data.Relational.Core.Exceptions;
using ArturRios.Data.Relational.Core.Interfaces;
using ArturRios.Data.Relational.Core.Providers;
using ArturRios.Data.Relational.Core.Transactions;
using ArturRios.Data.Sqlite;
using ArturRios.Data.Tests.TestSupport;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ArturRios.Data.Tests.DependencyInjection;

[Trait("Category", "Functional")]
public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void GivenAServiceCollection_WhenAddingTheDataConfiguration_ThenRepositoriesAndTheUnitOfWorkAreRegistered()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        connection.Open();

        var services = new ServiceCollection();
        services.AddSingleton<IDatabaseProvider>(new FakeSqliteProvider(connection));
        services.AddDataConfig<TestDbContext>(new BaseDbContextOptions
        {
            DatabaseType = DatabaseType.SqLite,
            ConnectionString = "Filename=:memory:"
        });

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<TestDbContext>().Database.EnsureCreated();

        Assert.NotNull(provider.GetRequiredService<IRepository<TestEntity, long>>());
        Assert.NotNull(provider.GetRequiredService<IAsyncRepository<TestEntity, long>>());
        Assert.NotNull(provider.GetRequiredService<IReadOnlyRepository<GuidKeyedTestEntity, Guid>>());
        Assert.NotNull(provider.GetRequiredService<IRepository<GuidKeyedTestEntity, Guid>>());
        Assert.NotNull(provider.GetRequiredService<IAsyncReadOnlyRepository<StringKeyedTestEntity, string>>());
        Assert.NotNull(provider.GetRequiredService<IAsyncRepository<StringKeyedTestEntity, string>>());
        Assert.NotNull(provider.GetRequiredService<IUnitOfWork>());
        Assert.NotNull(provider.GetRequiredService<IAsyncUnitOfWork>());
    }

    [Fact]
    public void GivenNoDatabaseProvider_WhenAddingTheDataConfiguration_ThenRegistrationFails()
    {
        var services = new ServiceCollection(); // no IDatabaseProvider registered

        Assert.Throws<DataAccessException>(() =>
            services.AddDataConfig<TestDbContext>(new BaseDbContextOptions { DatabaseType = DatabaseType.SqLite }));
    }

    [Theory]
    [InlineData(DatabaseType.SqLite, "ArturRios.Data.Sqlite", "AddSqliteProvider()")]
    [InlineData(DatabaseType.PostgreSql, "ArturRios.Data.PostgreSql", "AddPostgreSqlProvider()")]
    [InlineData(DatabaseType.MySql, "ArturRios.Data.MySql", "AddMySqlProvider()")]
    public void GivenNoDatabaseProvider_WhenAddingTheDataConfiguration_ThenTheErrorNamesThePackageAndCallThatExist(
        DatabaseType type, string package, string registration)
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<DataAccessException>(() =>
            services.AddDataConfig<TestDbContext>(new BaseDbContextOptions { DatabaseType = type }));

        var message = Assert.Single(exception.Messages);
        Assert.Contains(package, message);
        Assert.Contains(registration, message);
    }

    [Fact]
    public void GivenAProviderWithConstructorDependencies_WhenAddingTheDataConfiguration_ThenValidationDefersInsteadOfFailing()
    {
        var services = new ServiceCollection();
        // Type registration whose implementation cannot be instantiated by the eager validation
        // (no parameterless constructor): absence cannot be proven, so it must defer, not crash.
        services.AddSingleton<IDatabaseProvider, FakeSqliteProvider>();

        var exception = Record.Exception(() =>
            services.AddDataConfig<TestDbContext>(new BaseDbContextOptions
            {
                DatabaseType = DatabaseType.SqLite,
                ConnectionString = "Filename=:memory:"
            }));

        Assert.Null(exception);
    }

    [Fact]
    public void GivenAFactoryRegisteredProvider_WhenAddingTheDataConfiguration_ThenValidationDefersInsteadOfFailing()
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
                DatabaseType = DatabaseType.SqLite,
                ConnectionString = "Filename=:memory:"
            }));

        Assert.Null(exception);

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<TestDbContext>().Database.EnsureCreated();

        Assert.NotNull(provider.GetRequiredService<IRepository<TestEntity, long>>());
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

            Assert.NotNull(provider.GetRequiredService<IRepository<TestEntity, long>>());
            Assert.NotNull(provider.GetRequiredService<IAsyncRepository<TestEntity, long>>());
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

    [Theory]
    [InlineData("NotARealDb")]
    [InlineData("99")]
    [InlineData("")]
    public void GivenDatabaseTypeEnvVarInvalid_WhenAddDataConfigFromEnvironment_ThenThrowsDataAccessException(string databaseTypeValue)
    {
        const string prefix = "ARTURRIOS_TEST_INVALID";
        Environment.SetEnvironmentVariable($"{prefix}_DATABASETYPE", databaseTypeValue);

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

    [Fact]
    public void GivenASettingsSection_WhenAddingTheDataConfigurationFromSettings_ThenItsTypeAndConnectionStringAreUsed()
    {
        var provider = new CapturingProvider(DatabaseType.SqLite);
        var services = new ServiceCollection();
        services.AddSingleton<IDatabaseProvider>(provider);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Data:DatabaseType"] = "SqLite",
                ["Data:ConnectionString"] = "Filename=:memory:"
            })
            .Build();

        services.AddDataConfigFromSettings<TestDbContext>(configuration, "Data");
        using var serviceProvider = services.BuildServiceProvider();
        serviceProvider.GetRequiredService<TestDbContext>();

        Assert.Equal("Filename=:memory:", provider.ConnectionString);
        Assert.NotNull(serviceProvider.GetRequiredService<IRepository<TestEntity, long>>());
    }

    [Fact]
    public void GivenAMissingSettingsSection_WhenAddingTheDataConfigurationFromSettings_ThenTheDefaultOptionsAreValidated()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IDatabaseProvider>(new CapturingProvider(DatabaseType.SqLite));
        var configuration = new ConfigurationBuilder().Build();

        // The default options name PostgreSql (the enum's first value), which no registered provider serves.
        var exception = Assert.Throws<DataAccessException>(() =>
            services.AddDataConfigFromSettings<TestDbContext>(configuration, "Missing"));

        Assert.Contains("ArturRios.Data.PostgreSql", Assert.Single(exception.Messages));
    }

    [Fact]
    public void GivenADatabaseTypeWithNoKnownPackage_WhenAddingTheDataConfiguration_ThenTheErrorStaysGeneric()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<DataAccessException>(() =>
            services.AddDataConfig<TestDbContext>(new BaseDbContextOptions { DatabaseType = (DatabaseType)99 }));

        var message = Assert.Single(exception.Messages);
        Assert.Contains("the matching provider package", message);
        Assert.Contains("its provider registration()", message);
    }

    [Fact]
    public void GivenAProviderRegisteredByType_WhenAddingTheDataConfigurationForItsType_ThenRegistrationSucceeds()
    {
        var services = new ServiceCollection();
        services.AddSqliteProvider();

        var exception = Record.Exception(() =>
            services.AddDataConfig<TestDbContext>(new BaseDbContextOptions
            {
                DatabaseType = DatabaseType.SqLite,
                ConnectionString = "Filename=:memory:"
            }));

        Assert.Null(exception);
    }

    [Fact]
    public void GivenOnlyAProviderOfAnotherTypeRegisteredByType_WhenAddingTheDataConfiguration_ThenRegistrationFails()
    {
        var services = new ServiceCollection();
        services.AddSqliteProvider();

        var exception = Assert.Throws<DataAccessException>(() =>
            services.AddDataConfig<TestDbContext>(new BaseDbContextOptions { DatabaseType = DatabaseType.MySql }));

        Assert.Contains("AddMySqlProvider()", Assert.Single(exception.Messages));
    }

    [Fact]
    public void GivenADeferredValidationAndNoMatchingProvider_WhenTheContextIsResolved_ThenTheMissingProviderIsReported()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IDatabaseProvider>(_ => new CapturingProvider(DatabaseType.SqLite));
        services.AddDataConfig<TestDbContext>(new BaseDbContextOptions { DatabaseType = DatabaseType.PostgreSql });
        using var provider = services.BuildServiceProvider();

        var exception = Assert.Throws<DataAccessException>(() => provider.GetRequiredService<TestDbContext>());

        Assert.Contains("AddPostgreSqlProvider()", Assert.Single(exception.Messages));
    }

    [Fact]
    public void GivenNoConnectionStringEnvVar_WhenAddDataConfigFromEnvironment_ThenAnEmptyConnectionStringIsUsed()
    {
        const string prefix = "ARTURRIOS_TEST_NO_CONNECTION";
        Environment.SetEnvironmentVariable($"{prefix}_DATABASETYPE", "sqlite");
        Environment.SetEnvironmentVariable($"{prefix}_CONNECTIONSTRING", null);

        try
        {
            var provider = new CapturingProvider(DatabaseType.SqLite);
            var services = new ServiceCollection();
            services.AddSingleton<IDatabaseProvider>(provider);

            services.AddDataConfigFromEnvironment<TestDbContext>(prefix);
            using var serviceProvider = services.BuildServiceProvider();
            serviceProvider.GetRequiredService<TestDbContext>();

            Assert.Equal(string.Empty, provider.ConnectionString);
        }
        finally
        {
            Environment.SetEnvironmentVariable($"{prefix}_DATABASETYPE", null);
        }
    }

    // Records the connection string it is configured with, over a private in-memory SQLite database.
    private sealed class CapturingProvider(DatabaseType type) : IDatabaseProvider
    {
        public string? ConnectionString { get; private set; }

        public DatabaseType Type => type;

        public void Configure(DbContextOptionsBuilder builder, string connectionString)
        {
            ConnectionString = connectionString;
            builder.UseSqlite("Filename=:memory:");
        }
    }

    // Minimal in-test provider so the core DI test does not depend on a provider package.
    private sealed class FakeSqliteProvider(SqliteConnection connection) : IDatabaseProvider
    {
        public DatabaseType Type => DatabaseType.SqLite;

        public void Configure(DbContextOptionsBuilder builder, string connectionString) =>
            builder.UseSqlite(connection);
    }
}
