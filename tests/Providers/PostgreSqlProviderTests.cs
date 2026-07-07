using ArturRios.Data.PostgreSql;
using ArturRios.Data.Relational.Core.Configuration;

namespace ArturRios.Data.Tests.Providers;

public class PostgreSqlProviderTests
{
    [Fact]
    public void Type_IsPostgreSql() => Assert.Equal(DatabaseType.PostgreSql, new PostgreSqlProvider().Type);
}
