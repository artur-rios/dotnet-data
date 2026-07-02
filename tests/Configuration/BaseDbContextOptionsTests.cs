using System.Reflection;
using System.Runtime.CompilerServices;
using ArturRios.Data.Configuration;

namespace ArturRios.Data.Tests.Configuration;

public class BaseDbContextOptionsTests
{
    [Fact]
    public void BaseDbContextOptions_HasConnectionString_OfTypeString()
    {
        var prop = typeof(BaseDbContextOptions).GetProperty("ConnectionString");

        Assert.NotNull(prop);
        Assert.Equal(typeof(string), prop.PropertyType);
    }

    [Fact]
    public void BaseDbContextOptions_ConnectionString_DefaultsToEmptyString()
    {
        var options = new BaseDbContextOptions();

        Assert.Equal(string.Empty, options.ConnectionString);
    }

    [Fact]
    public void BaseDbContextOptions_ConnectionString_IsInitOnly()
    {
        var prop = typeof(BaseDbContextOptions).GetProperty("ConnectionString");
        Assert.NotNull(prop);

        var setter = prop.GetSetMethod();
        Assert.NotNull(setter);

        var modifiers = setter.ReturnParameter.GetRequiredCustomModifiers();
        Assert.Contains(typeof(IsExternalInit), modifiers);
    }

    [Fact]
    public void BaseDbContextOptions_ConnectionString_CanBeSetViaObjectInitializer()
    {
        var options = new BaseDbContextOptions { ConnectionString = "Server=localhost;Database=test" };

        Assert.Equal("Server=localhost;Database=test", options.ConnectionString);
    }

    [Fact]
    public void Options_CarryDatabaseTypeAndConnectionString()
    {
        var options = new ArturRios.Data.Configuration.BaseDbContextOptions
        {
            DatabaseType = ArturRios.Data.Configuration.DatabaseType.SQLite,
            ConnectionString = "Filename=:memory:"
        };

        Assert.Equal(ArturRios.Data.Configuration.DatabaseType.SQLite, options.DatabaseType);
        Assert.Equal("Filename=:memory:", options.ConnectionString);
    }
}
