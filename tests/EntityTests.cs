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
        Assert.True(typeof(Entity<>).IsAbstract);
        Assert.False(typeof(Entity<>).IsInterface);
    }

    [Fact]
    public void GivenTheEntityType_WhenInspected_ThenItsKeyTypeIsConstrainedToBeEquatable()
    {
        var key = typeof(Entity<>).GetGenericArguments().Single();
        var constraint = Assert.Single(key.GetGenericParameterConstraints());

        Assert.Equal(typeof(IEquatable<>).MakeGenericType(key), constraint);
    }

    [Theory]
    [InlineData(typeof(long))]
    [InlineData(typeof(int))]
    [InlineData(typeof(Guid))]
    [InlineData(typeof(string))]
    public void GivenAKeyType_WhenInspectingTheEntityId_ThenItHasThatType(Type keyType)
    {
        var prop = typeof(Entity<>).MakeGenericType(keyType).GetProperty("Id");

        Assert.NotNull(prop);
        Assert.Equal(keyType, prop.PropertyType);
    }

    [Fact]
    public void GivenTheEntityType_WhenInspected_ThenItsIdIsPubliclyReadableAndWritable()
    {
        var prop = typeof(Entity<long>).GetProperty("Id", BindingFlags.Public | BindingFlags.Instance);

        Assert.NotNull(prop);
        Assert.True(prop.CanRead);
        Assert.True(prop.CanWrite);
        Assert.True(prop.GetGetMethod()!.IsPublic);
        Assert.True(prop.GetSetMethod()!.IsPublic);
    }

    [Fact]
    public void GivenTheEntityId_WhenInspected_ThenItCarriesAColumnAttributeOrderedFirst()
    {
        var prop = typeof(Entity<long>).GetProperty("Id");
        Assert.NotNull(prop);

        var attr = prop.GetCustomAttribute<ColumnAttribute>();

        Assert.NotNull(attr);
        Assert.Equal(1, attr.Order);
    }
}
