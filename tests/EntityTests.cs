using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using ArturRios.Data.Relational.Core.Entities;

namespace ArturRios.Data.Tests;

[Trait("Category", "Unit")]
public class EntityTests
{
    [Fact]
    public void GivenTheEntityType_WhenInspected_ThenItIsAbstract()
    {
        Assert.True(typeof(Entity).IsAbstract);
        Assert.False(typeof(Entity).IsInterface);
    }

    [Fact]
    public void GivenTheEntityType_WhenInspected_ThenItsIdIsALong()
    {
        var prop = typeof(Entity).GetProperty("Id");

        Assert.NotNull(prop);
        Assert.Equal(typeof(long), prop.PropertyType);
    }

    [Fact]
    public void GivenTheEntityType_WhenInspected_ThenItsIdIsPubliclyReadableAndWritable()
    {
        var prop = typeof(Entity).GetProperty("Id", BindingFlags.Public | BindingFlags.Instance);

        Assert.NotNull(prop);
        Assert.True(prop.CanRead);
        Assert.True(prop.CanWrite);
        Assert.True(prop.GetGetMethod()!.IsPublic);
        Assert.True(prop.GetSetMethod()!.IsPublic);
    }

    [Fact]
    public void GivenTheEntityId_WhenInspected_ThenItCarriesAColumnAttributeOrderedFirst()
    {
        var prop = typeof(Entity).GetProperty("Id");
        Assert.NotNull(prop);

        var attr = prop.GetCustomAttribute<ColumnAttribute>();

        Assert.NotNull(attr);
        Assert.Equal(1, attr.Order);
    }
}
