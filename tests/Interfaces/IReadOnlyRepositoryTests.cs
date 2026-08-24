using System;
using System.Linq;
using ArturRios.Data.Relational.Core.Entities;
using ArturRios.Data.Relational.Core.Interfaces;
using ArturRios.Output;

namespace ArturRios.Data.Tests.Interfaces;

[Trait("Category", "Unit")]
public class IReadOnlyRepositoryTests
{
    private static readonly Type Type = typeof(IReadOnlyRepository<>);

    [Fact]
    public void GivenTheReadOnlyRepositoryContract_WhenInspected_ThenItIsAnInterfaceConstrainedToEntity()
    {
        Assert.True(Type.IsInterface);
        var param = Type.GetGenericArguments()[0];
        Assert.Contains(typeof(Entity), param.GetGenericParameterConstraints());
    }

    [Fact]
    public void GivenTheQueryMethod_WhenInspected_ThenItReturnsAnIQueryableOfTheEntity()
    {
        var m = Type.GetMethod("Query")!;
        Assert.Empty(m.GetParameters());
        Assert.Equal(typeof(IQueryable<>), m.ReturnType.GetGenericTypeDefinition());
    }

    [Fact]
    public void GivenTheGetAllMethod_WhenInspected_ThenItReturnsADataOutputOfAnEnumerable()
    {
        var m = Type.GetMethod("GetAll")!;
        Assert.Equal(typeof(DataOutput<>), m.ReturnType.GetGenericTypeDefinition());
    }

    [Fact]
    public void GivenTheGetByIdMethod_WhenInspected_ThenItTakesALongAndReturnsADataOutput()
    {
        var m = Type.GetMethod("GetById")!;
        Assert.Equal(typeof(long), m.GetParameters().Single().ParameterType);
        Assert.Equal(typeof(DataOutput<>), m.ReturnType.GetGenericTypeDefinition());
    }
}
