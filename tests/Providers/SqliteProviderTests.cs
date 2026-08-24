using ArturRios.Data.Relational.Core.Configuration;
using ArturRios.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ArturRios.Data.Tests.Providers;

[Trait("Category", "Unit")]
public class SqliteProviderTests
{
    [Fact]
    public void GivenTheSqliteProvider_WhenInspected_ThenItsTypeIsSqLite() => Assert.Equal(DatabaseType.SqLite, new SqliteProvider().Type);

    [Fact]
    public void GivenABuilder_WhenTheSqliteProviderConfiguresIt_ThenTheSqliteProviderIsSelected()
    {
        var builder = new DbContextOptionsBuilder();
        new SqliteProvider().Configure(builder, "Filename=:memory:");
        Assert.Contains(builder.Options.Extensions, e => e.GetType().Name.Contains("Sqlite"));
    }
}
