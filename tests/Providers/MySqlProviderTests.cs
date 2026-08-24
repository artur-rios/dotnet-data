using ArturRios.Data.Relational.Core.Configuration;
using ArturRios.Data.MySql;

namespace ArturRios.Data.Tests.Providers;

[Trait("Category", "Unit")]
public class MySqlProviderTests
{
    [Fact]
    public void GivenTheMySqlProvider_WhenInspected_ThenItsTypeIsMySql()
    {
        Assert.Equal(DatabaseType.MySql, new MySqlProvider().Type);
    }
}
