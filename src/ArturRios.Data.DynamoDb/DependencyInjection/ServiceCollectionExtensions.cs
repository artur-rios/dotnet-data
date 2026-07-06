using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.Runtime;
using ArturRios.Data.DynamoDb.Configuration;
using ArturRios.Data.DynamoDb.Interfaces;
using ArturRios.Data.DynamoDb.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ArturRios.Data.DynamoDb.DependencyInjection;

/// <summary>Dependency-injection registration for the ArturRios.Data.DynamoDb store.</summary>
public static class ServiceCollectionExtensions
{
    /// <param name="services">The service collection.</param>
    extension(IServiceCollection services)
    {
        /// <summary>Registers the DynamoDB store, binding options from configuration.</summary>
        /// <param name="configuration">Application configuration.</param>
        /// <param name="sectionName">Configuration section holding the options. Defaults to "ArturRios.Data.DynamoDb".</param>
        public IServiceCollection AddDynamoData(IConfiguration configuration, string sectionName = "ArturRios.Data.DynamoDb")
        {
            var options = configuration.GetSection(sectionName).Get<DynamoOptions>() ?? new DynamoOptions();
            return services.AddDynamoData(options);
        }

        /// <summary>Registers the DynamoDB store from an explicit options instance.</summary>
        /// <param name="options">The DynamoDB options.</param>
        public IServiceCollection AddDynamoData(DynamoOptions options)
        {
            services.AddSingleton<IAmazonDynamoDB>(_ => CreateClient(options));
            services.AddSingleton<IDynamoDBContext>(sp =>
                new DynamoDBContextBuilder()
                    .WithDynamoDBClient(() => sp.GetRequiredService<IAmazonDynamoDB>())
                    .Build());
            services.AddScoped(typeof(IAsyncDynamoRepository<>), typeof(DynamoRepository<>));
            return services;
        }
    }

    /// <summary>Builds an <see cref="IAmazonDynamoDB"/> client from the given options.</summary>
    /// <param name="options">The DynamoDB options.</param>
    private static IAmazonDynamoDB CreateClient(DynamoOptions options)
    {
        var hasKeys = !string.IsNullOrEmpty(options.AccessKey) && !string.IsNullOrEmpty(options.SecretKey);

        if (!string.IsNullOrEmpty(options.ServiceUrl))
        {
            var credentials = hasKeys
                ? new BasicAWSCredentials(options.AccessKey, options.SecretKey)
                : new BasicAWSCredentials("dummy", "dummy");
            var config = new AmazonDynamoDBConfig { ServiceURL = options.ServiceUrl };

            if (!string.IsNullOrEmpty(options.Region))
            {
                config.AuthenticationRegion = options.Region;
            }

            return new AmazonDynamoDBClient(credentials, config);
        }

        var region = RegionEndpoint.GetBySystemName(options.Region);

        return hasKeys
            ? new AmazonDynamoDBClient(new BasicAWSCredentials(options.AccessKey, options.SecretKey), region)
            : new AmazonDynamoDBClient(region);
    }
}
