using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ArturRios.Data;
using ArturRios.Data.Interfaces;

namespace ArturRios.Data.Tests.Interfaces;

public class IRangeRepositoryTests
{
    private static readonly Type InterfaceType = typeof(IRangeRepository<>);

    [Fact]
    public void IRangeRepository_IsInterface()
    {
        Assert.True(InterfaceType.IsInterface);
    }

    [Fact]
    public void IRangeRepository_GenericParameter_IsConstrainedToEntity()
    {
        var param = InterfaceType.GetGenericArguments()[0];
        var constraints = param.GetGenericParameterConstraints();

        Assert.Contains(typeof(Entity), constraints);
    }

    [Fact]
    public void IRangeRepository_HasUpdateRange_WithListOfT_Parameter()
    {
        var method = InterfaceType.GetMethod("UpdateRange");

        Assert.NotNull(method);
        var parameters = method.GetParameters();
        Assert.Single(parameters);
        Assert.Equal("entities", parameters[0].Name);
        Assert.True(parameters[0].ParameterType.IsGenericType);
        Assert.Equal(typeof(List<>), parameters[0].ParameterType.GetGenericTypeDefinition());
    }

    [Fact]
    public void IRangeRepository_UpdateRange_ReturnsIEnumerableOfT()
    {
        var method = InterfaceType.GetMethod("UpdateRange");
        Assert.NotNull(method);

        Assert.True(method.ReturnType.IsGenericType);
        Assert.Equal(typeof(IEnumerable<>), method.ReturnType.GetGenericTypeDefinition());
    }

    [Fact]
    public void IRangeRepository_HasDeleteRange_WithListOfInt_Parameter()
    {
        var method = InterfaceType.GetMethod("DeleteRange");

        Assert.NotNull(method);
        var parameters = method.GetParameters();
        Assert.Single(parameters);
        Assert.Equal("ids", parameters[0].Name);
        Assert.Equal(typeof(List<int>), parameters[0].ParameterType);
    }

    [Fact]
    public void IRangeRepository_DeleteRange_ReturnsIEnumerableOfInt()
    {
        var method = InterfaceType.GetMethod("DeleteRange");
        Assert.NotNull(method);

        Assert.Equal(typeof(IEnumerable<int>), method.ReturnType);
    }
}
