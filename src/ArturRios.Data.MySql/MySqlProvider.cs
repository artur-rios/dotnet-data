using ArturRios.Data.Configuration;
using ArturRios.Data.Providers;
using Microsoft.EntityFrameworkCore;

namespace ArturRios.Data.MySql;

/// <summary>
/// <see cref="IDatabaseProvider"/> that configures EF Core to use MySQL via Pomelo.
/// </summary>
public class MySqlProvider : IDatabaseProvider
{
    /// <inheritdoc />
    public DatabaseType Type => DatabaseType.MySql;

    /// <inheritdoc />
    public void Configure(DbContextOptionsBuilder builder, string connectionString) =>
        builder.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
}
