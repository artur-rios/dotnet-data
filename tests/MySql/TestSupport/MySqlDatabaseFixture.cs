using ArturRios.Data.MySql;
using ArturRios.Data.Relational.Core.Providers;
using ArturRios.Data.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace ArturRios.Data.Tests.MySql.TestSupport;

/// <summary>
///     Creates one throwaway database on the configured MySQL server for the whole MySQL test
///     collection and drops it on dispose. Contexts are built through <see cref="MySqlProvider"/>, so
///     the tests exercise the real provider seam rather than calling UseMySql directly.
/// </summary>
public sealed class MySqlDatabaseFixture : IDisposable
{
    private readonly IDatabaseProvider _provider = new MySqlProvider();

    public MySqlDatabaseFixture()
    {
        if (!MySqlTestServer.IsConfigured)
        {
            return;
        }

        Database = $"arturrios_data_test_{Guid.NewGuid():N}"[..40];
        Execute(MySqlTestServer.ServerConnectionString(), $"CREATE DATABASE `{Database}`;");
        Created = true;

        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    public string Database { get; } = string.Empty;

    private bool Created { get; }

    public void Dispose()
    {
        if (!Created)
        {
            return;
        }

        Execute(MySqlTestServer.ServerConnectionString(), $"DROP DATABASE IF EXISTS `{Database}`;");
    }

    public TestDbContext CreateContext()
    {
        var connectionString = MySqlTestServer.ConnectionStringFor(Database);
        var builder = new DbContextOptionsBuilder<TestDbContext>();

        _provider.Configure(builder, connectionString);

        return new TestDbContext(builder.Options);
    }

    /// <summary>Empties every table, so each test in the collection starts from a known state.</summary>
    public void Reset()
    {
        var connectionString = MySqlTestServer.ConnectionStringFor(Database);

        Execute(connectionString, """
                                  SET FOREIGN_KEY_CHECKS = 0;
                                  TRUNCATE TABLE Items;
                                  TRUNCATE TABLE VersionedItems;
                                  TRUNCATE TABLE UniqueItems;
                                  SET FOREIGN_KEY_CHECKS = 1;
                                  """);
    }

    private static void Execute(string connectionString, string sql)
    {
        using var connection = new MySqlConnection(connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}

[CollectionDefinition(Name)]
public class MySqlTestCollection : ICollectionFixture<MySqlDatabaseFixture>
{
    public const string Name = "MySql";
}
