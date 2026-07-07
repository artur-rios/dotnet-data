using ArturRios.Data.Relational.Core.Providers;
using Microsoft.Extensions.DependencyInjection;

namespace ArturRios.Data.PostgreSql;

/// <summary>
///     DI registration for the PostgreSQL provider.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>Registers the PostgreSQL <see cref="IDatabaseProvider" />.</summary>
    public static IServiceCollection AddPostgreSqlProvider(this IServiceCollection services)
    {
        services.AddSingleton<IDatabaseProvider, PostgreSqlProvider>();
        return services;
    }
}
