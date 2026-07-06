using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using ArturRios.Data.DynamoDb.Configuration;
using ArturRios.Data.DynamoDb.DependencyInjection;
using ArturRios.Data.DynamoDb.Interfaces;
using ArturRios.Data.Tests.DynamoDb.TestSupport;
using Microsoft.Extensions.DependencyInjection;

namespace ArturRios.Data.Tests.DynamoDb;

public class AddDynamoDataTests
{
    [Fact]
    public void AddDynamoData_RegistersClientContextAndRepository_Resolvable()
    {
        var services = new ServiceCollection();
        services.AddDynamoData(new DynamoOptions
        {
            Region = "us-east-1",
            ServiceUrl = "http://localhost:8000"
        });

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;

        Assert.NotNull(sp.GetRequiredService<IAmazonDynamoDB>());
        Assert.NotNull(sp.GetRequiredService<IDynamoDBContext>());
        Assert.NotNull(sp.GetRequiredService<IAsyncDynamoRepository<TestItem>>());
    }

    [Fact]
    public void AddDynamoData_WithRegionAndNoServiceUrl_ResolvesClient()
    {
        var services = new ServiceCollection();
        services.AddDynamoData(new DynamoOptions { Region = "us-east-1" });
        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetRequiredService<IAmazonDynamoDB>());
    }
}
