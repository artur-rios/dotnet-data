using ArturRios.Data.Core.Configuration;
using ArturRios.Data.Core.Exceptions;
using ArturRios.Data.Core.Interfaces;
using ArturRios.Data.Core.Providers;
using ArturRios.Data.Core.Repositories;
using ArturRios.Data.Core.Transactions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ArturRios.Data.Core.DependencyInjection;

/// <summary>
/// Dependency-injection registration for the ArturRios.Data.Core relational stack.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <param name="services">The service collection.</param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers the configured <typeparamref name="TContext"/>, repositories, and unit of work,
        /// binding options from the given configuration section.
        /// </summary>
        /// <param name="configuration">Application configuration.</param>
        /// <param name="sectionName">Configuration section holding the options. Defaults to "ArturRios.Data.Core".</param>
        /// <typeparam name="TContext">The application's context type.</typeparam>
        public IServiceCollection AddDataConfig<TContext>(IConfiguration configuration,
            string sectionName = "ArturRios.Data.Core")
            where TContext : BaseDbContext
        {
            var options = configuration.GetSection(sectionName).Get<BaseDbContextOptions>()
                          ?? new BaseDbContextOptions();
            return services.AddDataConfig<TContext>(options);
        }

        /// <summary>
        /// Registers the configured <typeparamref name="TContext"/>, repositories, and unit of work
        /// from an explicit options instance.
        /// </summary>
        /// <param name="options">The database options.</param>
        /// <typeparam name="TContext">The application's context type.</typeparam>
        public IServiceCollection AddDataConfig<TContext>(BaseDbContextOptions options)
            where TContext : BaseDbContext
        {
            services.AddDbContext<TContext>((sp, builder) =>
            {
                var provider = ResolveProvider(sp.GetServices<IDatabaseProvider>(), options.DatabaseType);
                provider.Configure(builder, options.ConnectionString);
            });

            services.AddScoped<BaseDbContext>(sp => sp.GetRequiredService<TContext>());

            services.AddScoped(typeof(IReadOnlyRepository<>), typeof(EfRepository<>));
            services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));
            services.AddScoped(typeof(IAsyncReadOnlyRepository<>), typeof(EfRepository<>));
            services.AddScoped(typeof(IAsyncRepository<>), typeof(EfRepository<>));

            services.AddScoped<IUnitOfWork, EfUnitOfWork>();
            services.AddScoped<IAsyncUnitOfWork, EfUnitOfWork>();

            // Validate provider availability eagerly so misconfiguration fails at registration, not first use.
            EnsureProviderRegistered(services, options.DatabaseType);

            return services;
        }
    }

    // Best-effort eager check: only throws when it can prove no provider matches.
    // Factory-registered providers (services.AddSingleton<IDatabaseProvider>(sp => ...)) expose
    // neither ImplementationInstance nor ImplementationType, so TryGetProviderType cannot inspect
    // them. Their presence means absence cannot be proven, so validation defers to ResolveProvider
    // at first use instead of failing fast on a false negative.
    private static void EnsureProviderRegistered(IServiceCollection services, DatabaseType type)
    {
        var providerTypes = services
            .Where(d => d.ServiceType == typeof(IDatabaseProvider))
            .Select(TryGetProviderType)
            .ToList();

        if (providerTypes.Any(t => t == type))
        {
            return;
        }

        var hasUninspectableProvider = providerTypes.Any(t => t is null);

        if (hasUninspectableProvider)
        {
            return;
        }

        throw new DataAccessException(
        [
            $"No IDatabaseProvider registered for DatabaseType '{type}'. " +
            $"Install and register the matching provider package " +
            $"(e.g. ArturRios.Data.Core.{type}) by calling its Add{type}Provider() extension."
        ]);
    }

    // Reads a registered provider's DatabaseType without building a ServiceProvider.
    // Providers are stateless with parameterless constructors, so instantiating to read Type is safe.
    private static DatabaseType? TryGetProviderType(ServiceDescriptor descriptor)
    {
        if (descriptor.ImplementationInstance is IDatabaseProvider instance)
        {
            return instance.Type;
        }

        if (descriptor.ImplementationType is { } implementationType &&
            Activator.CreateInstance(implementationType) is IDatabaseProvider created)
        {
            return created.Type;
        }

        return null;
    }

    private static IDatabaseProvider ResolveProvider(
        IEnumerable<IDatabaseProvider> providers, DatabaseType type)
    {
        var match = providers.FirstOrDefault(p => p.Type == type);
        if (match is null)
        {
            throw new DataAccessException(
            [
                $"No IDatabaseProvider registered for DatabaseType '{type}'. " +
                $"Install and register the matching provider package " +
                $"(e.g. ArturRios.Data.Core.{type}) by calling its Add{type}Provider() extension."
            ]);
        }

        return match;
    }
}
