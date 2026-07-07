using ArturRios.Data.Relational.Core.Configuration;
using ArturRios.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ArturRios.Data.Tests.Providers;

public class SqliteProviderTests
{
    [Fact]
    public void Type_IsSqlite() => Assert.Equal(DatabaseType.SQLite, new SqliteProvider().Type);

    [Fact]
    public void Configure_UsesSqlite()
    {
        var builder = new DbContextOptionsBuilder();
        new SqliteProvider().Configure(builder, "Filename=:memory:");
        Assert.Contains(builder.Options.Extensions, e => e.GetType().Name.Contains("Sqlite"));
    }
}
