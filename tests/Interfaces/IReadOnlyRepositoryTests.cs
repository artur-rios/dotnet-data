using System;
using System.Linq;
using ArturRios.Data.Relational.Core;
using ArturRios.Data.Relational.Core.Interfaces;
using ArturRios.Output;

namespace ArturRios.Data.Tests.Interfaces;

public class IReadOnlyRepositoryTests
{
    private static readonly Type Type = typeof(IReadOnlyRepository<>);

    [Fact]
    public void IsInterface_ConstrainedToEntity()
    {
        Assert.True(Type.IsInterface);
        var param = Type.GetGenericArguments()[0];
        Assert.Contains(typeof(Entity), param.GetGenericParameterConstraints());
    }

    [Fact]
    public void Query_ReturnsIQueryableOfT()
    {
        var m = Type.GetMethod("Query")!;
        Assert.Empty(m.GetParameters());
        Assert.Equal(typeof(IQueryable<>), m.ReturnType.GetGenericTypeDefinition());
    }

    [Fact]
    public void GetAll_ReturnsDataOutputOfEnumerable()
    {
        var m = Type.GetMethod("GetAll")!;
        Assert.Equal(typeof(DataOutput<>), m.ReturnType.GetGenericTypeDefinition());
    }

    [Fact]
    public void GetById_TakesInt_ReturnsDataOutput()
    {
        var m = Type.GetMethod("GetById")!;
        Assert.Equal(typeof(int), m.GetParameters().Single().ParameterType);
        Assert.Equal(typeof(DataOutput<>), m.ReturnType.GetGenericTypeDefinition());
    }
}
