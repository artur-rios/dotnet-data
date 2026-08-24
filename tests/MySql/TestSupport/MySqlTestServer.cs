using MySqlConnector;

namespace ArturRios.Data.Tests.MySql.TestSupport;

/// <summary>
///     Locates the MySQL server the integration tests run against.
///     <para>
///         The connection string comes from the <c>ARTURRIOS_DATA_MYSQL_TEST_CONNECTION</c> environment
///         variable; when it is unset (CI, or a machine without MySQL) the MySQL integration tests skip
///         instead of failing. It must point at a server the test user may create and drop databases on
///         — each run creates a throwaway database and drops it afterwards, so it never touches an
///         existing schema.
///     </para>
/// </summary>
public static class MySqlTestServer
{
    public const string EnvironmentVariable = "ARTURRIOS_DATA_MYSQL_TEST_CONNECTION";

    public const string SkipReason =
        $"Set {EnvironmentVariable} to a MySQL connection string to run the MySQL integration tests.";

    /// <summary>The configured server connection string, or null when the variable is unset.</summary>
    public static string? ConnectionString =>
        Environment.GetEnvironmentVariable(EnvironmentVariable) is { Length: > 0 } value ? value : null;

    public static bool IsConfigured => ConnectionString is not null;

    /// <summary>Rewrites the configured connection string to point at <paramref name="database"/>.</summary>
    public static string ConnectionStringFor(string database)
    {
        var builder = new MySqlConnectionStringBuilder(ConnectionString
                                                       ?? throw new InvalidOperationException(SkipReason))
        {
            Database = database
        };

        return builder.ConnectionString;
    }

    /// <summary>The configured connection string with no database selected (for CREATE/DROP DATABASE).</summary>
    public static string ServerConnectionString() => ConnectionStringFor(string.Empty);
}
