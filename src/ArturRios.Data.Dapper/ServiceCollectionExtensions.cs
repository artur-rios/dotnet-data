using Microsoft.Extensions.DependencyInjection;

namespace ArturRios.Data.Dapper;

/// <summary>DI registration for the Dapper query path.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Registers <see cref="ISqlQuery" /> and <see cref="IAsyncSqlQuery" /> (scoped, backed by
    ///     <see cref="DapperSqlQuery" />). Requires a <c>BaseDbContext</c> to be registered
    ///     (e.g. via <c>AddDataConfig&lt;TContext&gt;</c>).
    /// </summary>
    /// <param name="services">The service collection.</param>
    public static IServiceCollection AddDapper(this IServiceCollection services)
    {
        services.AddScoped<ISqlQuery, DapperSqlQuery>();
        services.AddScoped<IAsyncSqlQuery, DapperSqlQuery>();

        return services;
    }
}
