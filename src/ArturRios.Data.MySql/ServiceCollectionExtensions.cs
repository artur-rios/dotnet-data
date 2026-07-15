using ArturRios.Data.Relational.Core.Providers;
using Microsoft.Extensions.DependencyInjection;

namespace ArturRios.Data.MySql;

/// <summary>
/// DI registration for the MySQL provider.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>Registers the MySQL <see cref="IDatabaseProvider"/>.</summary>
    public static IServiceCollection AddMySqlProvider(this IServiceCollection services)
    {
        services.AddSingleton<IDatabaseProvider, MySqlProvider>();
        return services;
    }
}
