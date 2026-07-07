using ArturRios.Data.Relational.Core.Configuration;
using Microsoft.EntityFrameworkCore;

namespace ArturRios.Data.Relational.Core.Providers;

/// <summary>
/// Configures an EF Core <see cref="DbContextOptionsBuilder"/> for a specific database engine.
/// Each provider package registers one implementation into DI, keyed by <see cref="Type"/>.
/// </summary>
public interface IDatabaseProvider
{
    /// <summary>The database engine this provider handles.</summary>
    DatabaseType Type { get; }

    /// <summary>Applies the engine-specific configuration to the builder.</summary>
    void Configure(DbContextOptionsBuilder builder, string connectionString);
}
