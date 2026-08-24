using ArturRios.Data.PostgreSql;
using ArturRios.Data.Relational.Core.Configuration;

namespace ArturRios.Data.Tests.Providers;

[Trait("Category", "Unit")]
public class PostgreSqlProviderTests
{
    [Fact]
    public void GivenThePostgreSqlProvider_WhenInspected_ThenItsTypeIsPostgreSql() => Assert.Equal(DatabaseType.PostgreSql, new PostgreSqlProvider().Type);
}
