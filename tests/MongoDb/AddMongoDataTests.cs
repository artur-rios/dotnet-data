using ArturRios.Data.MongoDb;
using ArturRios.Data.MongoDb.Configuration;
using ArturRios.Data.MongoDb.DependencyInjection;
using ArturRios.Data.MongoDb.Interfaces;
using ArturRios.Data.MongoDb.Transactions;
using ArturRios.Data.Tests.MongoDb.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace ArturRios.Data.Tests.MongoDb;

public class AddMongoDataTests
{
    [Fact]
    public void AddMongoData_RegistersRepositoriesAndUnitOfWork_Resolvable()
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
}
