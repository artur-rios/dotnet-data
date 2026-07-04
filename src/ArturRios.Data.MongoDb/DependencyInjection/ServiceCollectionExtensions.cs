using ArturRios.Data.MongoDb.Configuration;
using ArturRios.Data.MongoDb.Interfaces;
using ArturRios.Data.MongoDb.Repositories;
using ArturRios.Data.MongoDb.Transactions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace ArturRios.Data.MongoDb.DependencyInjection;

/// <summary>Dependency-injection registration for the ArturRios.Data.MongoDb document store.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>Registers the MongoDB document store, binding options from configuration.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Application configuration.</param>
    /// <param name="sectionName">Configuration section holding the options. Defaults to "ArturRios.Data.MongoDb".</param>
    public static IServiceCollection AddMongoData(this IServiceCollection services,
        IConfiguration configuration, string sectionName = "ArturRios.Data.MongoDb")
    {
        var options = configuration.GetSection(sectionName).Get<MongoOptions>() ?? new MongoOptions();
        return services.AddMongoData(options);
    }

    /// <summary>Registers the MongoDB document store from an explicit options instance.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="options">The Mongo options.</param>
    public static IServiceCollection AddMongoData(this IServiceCollection services, MongoOptions options)
    {
        services.AddSingleton<IMongoClient>(_ => new MongoClient(options.ConnectionString));
        services.AddScoped<IMongoDatabase>(sp => sp.GetRequiredService<IMongoClient>().GetDatabase(options.DatabaseName));
        services.AddScoped<MongoContext>();

        services.AddScoped(typeof(IDocumentReadOnlyRepository<>), typeof(MongoDocumentRepository<>));
        services.AddScoped(typeof(IDocumentRepository<>), typeof(MongoDocumentRepository<>));
        services.AddScoped(typeof(IAsyncDocumentReadOnlyRepository<>), typeof(MongoDocumentRepository<>));
        services.AddScoped(typeof(IAsyncDocumentRepository<>), typeof(MongoDocumentRepository<>));

        services.AddScoped<IMongoUnitOfWork, MongoUnitOfWork>();
        services.AddScoped<IAsyncMongoUnitOfWork, MongoUnitOfWork>();

        return services;
    }
}
