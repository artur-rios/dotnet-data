using System.Runtime.CompilerServices;
using ArturRios.Data.Relational.Core.Configuration;

namespace ArturRios.Data.Tests.Configuration;

[Trait("Category", "Unit")]
public class BaseDbContextOptionsTests
{
    [Fact]
    public void GivenTheOptionsType_WhenInspected_ThenConnectionStringIsAString()
    {
        var prop = typeof(BaseDbContextOptions).GetProperty("ConnectionString");

        Assert.NotNull(prop);
        Assert.Equal(typeof(string), prop.PropertyType);
    }

    [Fact]
    public void GivenNewOptions_WhenInspected_ThenConnectionStringDefaultsToEmpty()
    {
        var options = new BaseDbContextOptions();

        Assert.Equal(string.Empty, options.ConnectionString);
    }

    [Fact]
    public void GivenTheOptionsType_WhenInspected_ThenConnectionStringIsInitOnly()
    {
        var prop = typeof(BaseDbContextOptions).GetProperty("ConnectionString");
        Assert.NotNull(prop);

        var setter = prop.GetSetMethod();
        Assert.NotNull(setter);

        var modifiers = setter.ReturnParameter?.GetRequiredCustomModifiers();

        Assert.NotNull(modifiers);
        Assert.Contains(typeof(IsExternalInit), modifiers);
    }

    [Fact]
    public void GivenAnObjectInitializer_WhenSettingConnectionString_ThenItIsApplied()
    {
        var options = new BaseDbContextOptions { ConnectionString = "Server=localhost;Database=test" };

        Assert.Equal("Server=localhost;Database=test", options.ConnectionString);
    }

    [Fact]
    public void GivenAnObjectInitializer_WhenSettingBothValues_ThenBothAreCarried()
    {
        var options = new BaseDbContextOptions
        {
            DatabaseType = DatabaseType.SqLite, ConnectionString = "Filename=:memory:"
        };

        Assert.Equal(DatabaseType.SqLite, options.DatabaseType);
        Assert.Equal("Filename=:memory:", options.ConnectionString);
    }
}
