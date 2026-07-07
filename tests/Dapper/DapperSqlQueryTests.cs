using System.Linq;
using ArturRios.Data.Dapper;
using ArturRios.Data.Tests.TestSupport;

namespace ArturRios.Data.Tests.Dapper;

public class DapperSqlQueryTests
{
    private static void Seed(TestDbContext context, params string[] names)
    {
        foreach (var name in names)
        {
            context.Items.Add(new TestEntity { Name = name });
        }

        context.SaveChanges();
    }

    [Fact]
    public void Query_ReturnsAllRows()
    {
        using var context = SqliteTestContextFactory.Create();
        Seed(context, "a", "b");
        var sut = new DapperSqlQuery(context);

        var result = sut.Query<ItemRow>("SELECT Id, Name FROM Items ORDER BY Id");

        Assert.True(result.Success);
        Assert.Equal(["a", "b"], result.Data!.Select(r => r.Name));
    }

    [Fact]
    public void Query_EmptyResult_IsSuccessWithEmptySequence()
    {
        using var context = SqliteTestContextFactory.Create();
        var sut = new DapperSqlQuery(context);

        var result = sut.Query<ItemRow>("SELECT Id, Name FROM Items");

        Assert.True(result.Success);
        Assert.Empty(result.Data!);
    }

    [Fact]
    public void QueryFirstOrDefault_ReturnsRow_OrNull()
    {
        using var context = SqliteTestContextFactory.Create();
        Seed(context, "only");
        var sut = new DapperSqlQuery(context);

        var found = sut.QueryFirstOrDefault<ItemRow>("SELECT Id, Name FROM Items WHERE Name = @Name",
            new { Name = "only" });
        Assert.True(found.Success);
        Assert.Equal("only", found.Data!.Name);

        var missing =
            sut.QueryFirstOrDefault<ItemRow>("SELECT Id, Name FROM Items WHERE Name = @Name", new { Name = "nope" });
        Assert.True(missing.Success);
        Assert.Null(missing.Data);
    }

    [Fact]
    public void QuerySingleOrDefault_MultipleRows_ReturnsErrorEnvelope()
    {
        using var context = SqliteTestContextFactory.Create();
        Seed(context, "dup", "dup");
        var sut = new DapperSqlQuery(context);

        var result =
            sut.QuerySingleOrDefault<ItemRow>("SELECT Id, Name FROM Items WHERE Name = @Name", new { Name = "dup" });

        Assert.False(result.Success);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void ExecuteScalar_ReturnsScalar()
    {
        using var context = SqliteTestContextFactory.Create();
        Seed(context, "a", "b", "c");
        var sut = new DapperSqlQuery(context);

        var result = sut.ExecuteScalar<long>("SELECT COUNT(*) FROM Items");

        Assert.True(result.Success);
        Assert.Equal(3L, result.Data);
    }

    [Fact]
    public void Query_MalformedSql_ReturnsErrorEnvelope_DoesNotThrow()
    {
        using var context = SqliteTestContextFactory.Create();
        var sut = new DapperSqlQuery(context);

        var result = sut.Query<ItemRow>("SELECT Id, Name FROM NoSuchTable");

        Assert.False(result.Success);
        Assert.NotEmpty(result.Errors);
    }

    private sealed record ItemRow(long Id, string Name);
}
