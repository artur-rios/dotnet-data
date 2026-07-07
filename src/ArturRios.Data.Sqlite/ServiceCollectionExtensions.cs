using ArturRios.Data.Relational.Core.Providers;
using Microsoft.Extensions.DependencyInjection;

namespace ArturRios.Data.Sqlite;

/// <summary>
/// DI registration for the SQLite provider.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>Registers the SQLite <see cref="IDatabaseProvider"/>.</summary>
    public static IServiceCollection AddSqliteProvider(this IServiceCollection services)
    {
        services.AddSingleton<IDatabaseProvider, SqliteProvider>();
        return services;
    }
}
