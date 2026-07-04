using System.Linq;
using System.Threading.Tasks;
using ArturRios.Data.Dapper;
using ArturRios.Data.Tests.TestSupport;

namespace ArturRios.Data.Tests.Dapper;

public class DapperSqlQueryAsyncTests
{
    private sealed record ItemRow(long Id, string Name);

    private static void Seed(TestDbContext context, params string[] names)
    {
        foreach (var name in names)
        {
            context.Items.Add(new TestEntity { Name = name });
        }
        context.SaveChanges();
    }

    [Fact]
    public async Task QueryAsync_ReturnsAllRows()
    {
        using var context = SqliteTestContextFactory.Create();
        Seed(context, "a", "b");
        var sut = new DapperSqlQuery(context);

        var result = await sut.QueryAsync<ItemRow>("SELECT Id, Name FROM Items ORDER BY Id");

        Assert.True(result.Success);
        Assert.Equal(new[] { "a", "b" }, result.Data!.Select(r => r.Name));
    }

    [Fact]
    public async Task QueryFirstOrDefaultAsync_ReturnsNull_WhenMissing()
    {
        using var context = SqliteTestContextFactory.Create();
        var sut = new DapperSqlQuery(context);

        var result = await sut.QueryFirstOrDefaultAsync<ItemRow>("SELECT Id, Name FROM Items WHERE Id = @Id", new { Id = 999 });

        Assert.True(result.Success);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task QuerySingleOrDefaultAsync_MultipleRows_ReturnsErrorEnvelope()
    {
        using var context = SqliteTestContextFactory.Create();
        Seed(context, "dup", "dup");
        var sut = new DapperSqlQuery(context);

        var result = await sut.QuerySingleOrDefaultAsync<ItemRow>("SELECT Id, Name FROM Items WHERE Name = @Name", new { Name = "dup" });

        Assert.False(result.Success);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public async Task ExecuteScalarAsync_ReturnsScalar()
    {
        using var context = SqliteTestContextFactory.Create();
        Seed(context, "a", "b");
        var sut = new DapperSqlQuery(context);

        var result = await sut.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM Items");

        Assert.True(result.Success);
        Assert.Equal(2L, result.Data);
    }

    [Fact]
    public async Task QueryAsync_MalformedSql_ReturnsErrorEnvelope_DoesNotThrow()
    {
        using var context = SqliteTestContextFactory.Create();
        var sut = new DapperSqlQuery(context);

        var result = await sut.QueryAsync<ItemRow>("SELECT Id, Name FROM NoSuchTable");

        Assert.False(result.Success);
        Assert.NotEmpty(result.Errors);
    }
}
