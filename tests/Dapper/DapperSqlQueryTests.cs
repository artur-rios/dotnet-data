using System;
using System.Linq;
using ArturRios.Data.Dapper;
using ArturRios.Data.Tests.TestSupport;
using Microsoft.Extensions.Logging;

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

    [Fact]
    public void ExecuteScalar_DuplicateUniqueValue_ReturnsConflict_WithoutLeakingConstraintText()
    {
        using var context = SqliteTestContextFactory.Create();
        var sut = new DapperSqlQuery(context);
        const string insert = "INSERT INTO UniqueItems (Email) VALUES (@Email)";
        Assert.True(sut.ExecuteScalar<long>(insert, new { Email = "a@b.com" }).Success);

        var result = sut.ExecuteScalar<long>(insert, new { Email = "a@b.com" });

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("same unique value already exists"));
        Assert.DoesNotContain(result.Errors, e =>
            e.Contains("IX_UniqueItems_Email") || e.Contains("Email") || e.Contains("a@b.com"));
    }

    [Fact]
    public void ExecuteScalar_NotNullViolation_ReturnsIntegrityConflict_WithoutLeakingColumn()
    {
        using var context = SqliteTestContextFactory.Create();
        var sut = new DapperSqlQuery(context);

        var result = sut.ExecuteScalar<long>("INSERT INTO UniqueItems (Email) VALUES (NULL)");

        Assert.False(result.Success);
        Assert.Equal(["Conflict: the operation violates a data-integrity rule."], result.Errors);
    }

    [Fact]
    public void Query_MalformedSql_DoesNotLeakProviderText()
    {
        using var context = SqliteTestContextFactory.Create();
        var sut = new DapperSqlQuery(context);

        var result = sut.Query<ItemRow>("SELECT Id, Name FROM NoSuchTable");

        Assert.False(result.Success);
        Assert.Equal(["A data-access error occurred."], result.Errors);
    }

    [Fact]
    public void Query_WithLogger_LogsProviderDetail_ButEnvelopeStaysGeneric()
    {
        using var context = SqliteTestContextFactory.Create();
        var logger = new ListLogger<DapperSqlQuery>();
        var sut = new DapperSqlQuery(context, logger);

        var result = sut.Query<ItemRow>("SELECT Id, Name FROM NoSuchTable");

        Assert.Equal(["A data-access error occurred."], result.Errors);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Contains("NoSuchTable", entry.Message);
        Assert.Contains("no such table", entry.Exception!.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Query_WithoutLogger_Succeeds()
    {
        using var context = SqliteTestContextFactory.Create();
        Seed(context, "a");
        var sut = new DapperSqlQuery(context);

        var result = sut.Query<ItemRow>("SELECT Id, Name FROM Items");

        Assert.True(result.Success);
    }

    private sealed record ItemRow(long Id, string Name);
}
