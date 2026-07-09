using ArturRios.Data.Relational.Core.Configuration;
using ArturRios.Data.Relational.Core.Providers;
using Microsoft.EntityFrameworkCore;

namespace ArturRios.Data.Sqlite;

/// <summary>
///     <see cref="IDatabaseProvider" /> that configures EF Core to use SQLite.
/// </summary>
public class SqliteProvider : IDatabaseProvider
{
    /// <inheritdoc />
    public DatabaseType Type => DatabaseType.SqLite;

    /// <inheritdoc />
    public void Configure(DbContextOptionsBuilder builder, string connectionString) =>
        builder.UseSqlite(connectionString);
}
