namespace ArturRios.Data.Configuration;

/// <summary>
/// Base configuration options for a <see cref="Microsoft.EntityFrameworkCore.DbContext"/>.
/// </summary>
public class BaseDbContextOptions
{
    /// <summary>
    /// The database connection string.
    /// </summary>
    public string ConnectionString { get; init; } = string.Empty;
}
