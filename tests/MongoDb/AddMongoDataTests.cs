using ArturRios.Data.MongoDb;
using ArturRios.Data.MongoDb.Configuration;
using ArturRios.Data.MongoDb.DependencyInjection;
using ArturRios.Data.MongoDb.Interfaces;
using ArturRios.Data.MongoDb.Transactions;
using ArturRios.Data.Tests.MongoDb.TestSupport;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace ArturRios.Data.Tests.MongoDb;

[Trait("Category", "Unit")]
public class AddMongoDataTests
{
    [Fact]
    public void GivenAServiceCollection_WhenAddingMongoData_ThenTheRepositoriesAndUnitOfWorkResolve()
    {
        var services = new ServiceCollection();
        services.AddMongoData(new MongoOptions
        {
            ConnectionString = "mongodb://localhost:27017",
            DatabaseName = "testdb"
        });

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;

        Assert.NotNull(sp.GetRequiredService<IMongoClient>());
        Assert.NotNull(sp.GetRequiredService<MongoContext>());
        Assert.NotNull(sp.GetRequiredService<IDocumentRepository<TestDoc>>());
        Assert.NotNull(sp.GetRequiredService<IAsyncDocumentRepository<TestDoc>>());
        Assert.NotNull(sp.GetRequiredService<IMongoUnitOfWork>());
        Assert.NotNull(sp.GetRequiredService<IAsyncMongoUnitOfWork>());
    }

    [Fact]
    public void GivenLoggingIsRegistered_WhenAddingMongoData_ThenTheRepositoriesResolve()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMongoData(new MongoOptions
        {
            ConnectionString = "mongodb://localhost:27017",
            DatabaseName = "testdb"
        });

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IDocumentRepository<TestDoc>>());
    }

    [Fact]
    public void GivenASettingsSection_WhenAddingMongoDataFromConfiguration_ThenItsDatabaseIsUsed()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ArturRios.Data.MongoDb:ConnectionString"] = "mongodb://localhost:27017",
                ["ArturRios.Data.MongoDb:DatabaseName"] = "configured"
            })
            .Build();
        var services = new ServiceCollection();

        services.AddMongoData(configuration);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.Equal("configured", scope.ServiceProvider.GetRequiredService<IMongoDatabase>().DatabaseNamespace.DatabaseName);
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IAsyncDocumentRepository<TestDoc>>());
    }

    [Fact]
    public void GivenANamedSettingsSection_WhenAddingMongoDataFromConfiguration_ThenThatSectionIsRead()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Mongo:ConnectionString"] = "mongodb://localhost:27017",
                ["Mongo:DatabaseName"] = "named"
            })
            .Build();
        var services = new ServiceCollection();

        services.AddMongoData(configuration, "Mongo");
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.Equal("named", scope.ServiceProvider.GetRequiredService<IMongoDatabase>().DatabaseNamespace.DatabaseName);
    }
}
