using ArturRios.Data.Relational.Core.Configuration;
using ArturRios.Data.Relational.Core.Providers;
using Microsoft.EntityFrameworkCore;

namespace ArturRios.Data.MySql;

/// <summary>
/// <see cref="IDatabaseProvider"/> that configures EF Core to use MySQL or MariaDB via
/// Microting.EntityFrameworkCore.MySql, a maintained fork of Pomelo.EntityFrameworkCore.MySql.
/// </summary>
public class MySqlProvider : IDatabaseProvider
{
    /// <inheritdoc />
    public DatabaseType Type => DatabaseType.MySql;

    /// <inheritdoc />
    public void Configure(DbContextOptionsBuilder builder, string connectionString) =>
        builder.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
}
