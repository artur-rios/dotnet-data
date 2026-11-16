using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using ArturRios.Data.DynamoDb.Configuration;
using ArturRios.Data.DynamoDb.DependencyInjection;
using ArturRios.Data.DynamoDb.Interfaces;
using ArturRios.Data.Tests.DynamoDb.TestSupport;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ArturRios.Data.Tests.DynamoDb;

[Trait("Category", "Unit")]
public class AddDynamoDataTests
{
    [Fact]
    public void GivenAServiceCollection_WhenAddingDynamoData_ThenTheClientContextAndRepositoryResolve()
    {
        var services = new ServiceCollection();
        services.AddDynamoData(new DynamoOptions { Region = "us-east-1", ServiceUrl = "http://localhost:8000" });

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;

        Assert.NotNull(sp.GetRequiredService<IAmazonDynamoDB>());
        Assert.NotNull(sp.GetRequiredService<IDynamoDBContext>());
        Assert.NotNull(sp.GetRequiredService<IAsyncDynamoRepository<TestItem>>());
    }

    [Fact]
    public void GivenLoggingIsRegistered_WhenAddingDynamoData_ThenTheRepositoryResolves()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDynamoData(new DynamoOptions { Region = "us-east-1", ServiceUrl = "http://localhost:8000" });

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IAsyncDynamoRepository<TestItem>>());
    }

    [Fact]
    public void GivenARegionAndNoServiceUrl_WhenAddingDynamoData_ThenTheClientResolves()
    {
        var services = new ServiceCollection();
        services.AddDynamoData(new DynamoOptions { Region = "us-east-1" });
        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetRequiredService<IAmazonDynamoDB>());
    }

    [Fact]
    public void GivenASettingsSection_WhenAddingDynamoDataFromConfiguration_ThenTheClientTargetsItsServiceUrlAndRegion()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ArturRios.Data.DynamoDb:ServiceUrl"] = "http://localhost:8123",
                ["ArturRios.Data.DynamoDb:Region"] = "eu-west-1"
            })
            .Build();
        var services = new ServiceCollection();

        services.AddDynamoData(configuration);
        using var provider = services.BuildServiceProvider();
        var config = provider.GetRequiredService<IAmazonDynamoDB>().Config;

        Assert.StartsWith("http://localhost:8123", config.ServiceURL);
        Assert.Equal("eu-west-1", config.AuthenticationRegion);
    }

    [Fact]
    public void GivenAServiceUrlWithoutARegion_WhenAddingDynamoData_ThenNoAuthenticationRegionIsSet()
    {
        var services = new ServiceCollection();
        services.AddDynamoData(new DynamoOptions { ServiceUrl = "http://localhost:8000", AccessKey = "ak", SecretKey = "sk" });
        using var provider = services.BuildServiceProvider();

        var config = provider.GetRequiredService<IAmazonDynamoDB>().Config;

        Assert.StartsWith("http://localhost:8000", config.ServiceURL);
        Assert.Null(config.AuthenticationRegion);
    }

    [Fact]
    public void GivenARegionAndKeysAndNoServiceUrl_WhenAddingDynamoData_ThenTheClientTargetsThatRegion()
    {
        var services = new ServiceCollection();
        services.AddDynamoData(new DynamoOptions { Region = "eu-west-1", AccessKey = "ak", SecretKey = "sk" });
        using var provider = services.BuildServiceProvider();

        var config = provider.GetRequiredService<IAmazonDynamoDB>().Config;

        Assert.Equal(RegionEndpoint.EUWest1, config.RegionEndpoint);
        Assert.Null(config.ServiceURL);
    }
}
