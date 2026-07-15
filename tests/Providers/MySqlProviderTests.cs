using ArturRios.Data.Relational.Core.Configuration;
using ArturRios.Data.MySql;

namespace ArturRios.Data.Tests.Providers;

public class MySqlProviderTests
{
    [Fact]
    public void Type_IsMySql()
    {
        Assert.Equal(DatabaseType.MySql, new MySqlProvider().Type);
    }
}
