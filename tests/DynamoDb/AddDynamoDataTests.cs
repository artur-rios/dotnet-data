using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using ArturRios.Data.DynamoDb.Configuration;
using ArturRios.Data.DynamoDb.DependencyInjection;
using ArturRios.Data.DynamoDb.Interfaces;
using ArturRios.Data.Tests.DynamoDb.TestSupport;
using Microsoft.Extensions.DependencyInjection;

namespace ArturRios.Data.Tests.DynamoDb;

[Trait("Category", "Functional")]
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
}
