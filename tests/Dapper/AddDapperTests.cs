using ArturRios.Data.Dapper;
using ArturRios.Data.Relational.Core.Configuration;
using ArturRios.Data.Tests.TestSupport;
using Microsoft.Extensions.DependencyInjection;

namespace ArturRios.Data.Tests.Dapper;

public class AddDapperTests
{
    [Fact]
    public void AddDapper_RegistersQueryServices_Resolvable()
    {
        var services = new ServiceCollection();
        // DapperSqlQuery depends on BaseDbContext; register a real one via the test factory.
        services.AddScoped<BaseDbContext>(_ => SqliteTestContextFactory.Create());
        services.AddDapper();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ISqlQuery>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IAsyncSqlQuery>());
    }
}
