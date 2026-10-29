using System.Collections.Concurrent;
using ArturRios.Data.Relational.Core.Configuration;
using ArturRios.Data.Relational.Core.Providers;
using Microsoft.EntityFrameworkCore;

namespace ArturRios.Data.MySql;

/// <summary>
/// <see cref="IDatabaseProvider"/> that configures EF Core to use MySQL or MariaDB via
/// Microting.EntityFrameworkCore.MySql, a maintained fork of Pomelo.EntityFrameworkCore.MySql.
/// The server version is detected from the server the first time a connection string is configured,
/// then reused.
/// </summary>
public class MySqlProvider : IDatabaseProvider
{
    // Context options are built once per DI scope (per request, in a web app), and detecting the
    // server version opens a connection and queries the server. The provider is a singleton, so the
    // detected version is kept per connection string: one probe per server instead of one per scope.
    // A failed probe is not cached, so the next scope retries it.
    private readonly ConcurrentDictionary<string, ServerVersion> _serverVersions = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public DatabaseType Type => DatabaseType.MySql;

    /// <inheritdoc />
    public void Configure(DbContextOptionsBuilder builder, string connectionString) =>
        builder.UseMySql(connectionString, _serverVersions.GetOrAdd(connectionString, ServerVersion.AutoDetect));
}
