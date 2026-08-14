using ArturRios.Data.Dapper;
using ArturRios.Data.Relational.Core.Configuration;
using ArturRios.Data.Tests.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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

    [Fact]
    public void AddDapper_WithLoggingRegistered_ResolvesAndLogsFailures()
    {
        var logger = new ListLogger<DapperSqlQuery>();
        var services = new ServiceCollection();
        services.AddScoped<BaseDbContext>(_ => SqliteTestContextFactory.Create());
        services.AddSingleton<ILogger<DapperSqlQuery>>(logger);
        services.AddDapper();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var query = scope.ServiceProvider.GetRequiredService<ISqlQuery>();

        var result = query.Query<object>("SELECT 1 FROM NoSuchTable");

        Assert.False(result.Success);
        Assert.NotEmpty(logger.Entries);
    }
}
