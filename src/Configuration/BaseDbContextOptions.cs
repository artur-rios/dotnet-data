namespace ArturRios.Data.Core.Configuration;

/// <summary>
/// Base configuration options for an Entity Framework Core DbContext.
/// </summary>
public class BaseDbContextOptions
{
    /// <summary>
    /// The database engine used to select the EF Core provider at runtime.
    /// </summary>
    public DatabaseType DatabaseType { get; init; }

    /// <summary>
    /// The database connection string.
    /// </summary>
    public string ConnectionString { get; init; } = string.Empty;
}
