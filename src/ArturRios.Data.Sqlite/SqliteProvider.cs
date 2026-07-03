using ArturRios.Data.Core.Configuration;
using ArturRios.Data.Core.Providers;
using Microsoft.EntityFrameworkCore;

namespace ArturRios.Data.Sqlite;

/// <summary>
/// <see cref="IDatabaseProvider"/> that configures EF Core to use SQLite.
/// </summary>
public class SqliteProvider : IDatabaseProvider
{
    /// <inheritdoc />
    public DatabaseType Type => DatabaseType.SQLite;

    /// <inheritdoc />
    public void Configure(DbContextOptionsBuilder builder, string connectionString) =>
        builder.UseSqlite(connectionString);
}
