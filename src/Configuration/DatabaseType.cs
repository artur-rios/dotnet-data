namespace ArturRios.Data.Core.Configuration;

/// <summary>
/// Supported relational database engines for provider selection.
/// </summary>
public enum DatabaseType
{
    /// <summary>PostgreSQL via Npgsql.</summary>
    PostgreSql,

    /// <summary>MySQL via Pomelo.</summary>
    MySql,

    /// <summary>SQLite via Microsoft.EntityFrameworkCore.Sqlite.</summary>
    SQLite
}
