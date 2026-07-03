using ArturRios.Data.Configuration;
using ArturRios.Data.PostgreSql;

namespace ArturRios.Data.Tests.Providers;

public class PostgreSqlProviderTests
{
    [Fact]
    public void Type_IsPostgreSql()
    {
        Assert.Equal(DatabaseType.PostgreSql, new PostgreSqlProvider().Type);
    }
}
