using ArturRios.Data.Core.Configuration;
using ArturRios.Data.Core.Providers;
using Microsoft.EntityFrameworkCore;

namespace ArturRios.Data.PostgreSql;

/// <summary>
/// <see cref="IDatabaseProvider"/> that configures EF Core to use PostgreSQL via Npgsql.
/// </summary>
public class PostgreSqlProvider : IDatabaseProvider
{
    /// <inheritdoc />
    public DatabaseType Type => DatabaseType.PostgreSql;

    /// <inheritdoc />
    public void Configure(DbContextOptionsBuilder builder, string connectionString) =>
        builder.UseNpgsql(connectionString);
}
