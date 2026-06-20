namespace ArturRios.Data.Configuration;

/// <summary>
/// Base configuration options for an Entity Framework Core DbContext.
/// </summary>
public class BaseDbContextOptions
{
    /// <summary>
    /// The database connection string.
    /// </summary>
    public string ConnectionString { get; init; } = string.Empty;
}
