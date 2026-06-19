using System;
using System.Linq;
using System.Reflection;
using ArturRios.Data;
using ArturRios.Data.Interfaces;

namespace ArturRios.Data.Tests.Interfaces;

public class IReadOnlyRepositoryTests
{
    private static readonly Type InterfaceType = typeof(IReadOnlyRepository<>);

    [Fact]
    public void IReadOnlyRepository_IsInterface()
    {
        Assert.True(InterfaceType.IsInterface);
    }

    [Fact]
    public void IReadOnlyRepository_GenericParameter_IsCovariant()
    {
        var param = InterfaceType.GetGenericArguments()[0];

        Assert.True(param.GenericParameterAttributes.HasFlag(GenericParameterAttributes.Covariant));
    }

    [Fact]
    public void IReadOnlyRepository_GenericParameter_IsConstrainedToEntity()
    {
        var param = InterfaceType.GetGenericArguments()[0];
        var constraints = param.GetGenericParameterConstraints();

        Assert.Contains(typeof(Entity), constraints);
    }

    [Fact]
    public void IReadOnlyRepository_HasGetAll_ReturningIQueryableOfT()
    {
        var method = InterfaceType.GetMethod("GetAll");

        Assert.NotNull(method);
        Assert.Empty(method.GetParameters());
        Assert.True(method.ReturnType.IsGenericType);
        Assert.Equal(typeof(IQueryable<>), method.ReturnType.GetGenericTypeDefinition());
    }

    [Fact]
    public void IReadOnlyRepository_HasGetById_WithIntParameter_Named_Id()
    {
        var method = InterfaceType.GetMethod("GetById");

        Assert.NotNull(method);
        var parameters = method.GetParameters();
        Assert.Single(parameters);
        Assert.Equal(typeof(int), parameters[0].ParameterType);
        Assert.Equal("id", parameters[0].Name);
    }

    [Fact]
    public void IReadOnlyRepository_GetById_ReturnsT()
    {
        var method = InterfaceType.GetMethod("GetById");
        Assert.NotNull(method);

        Assert.True(method.ReturnType.IsGenericParameter);
    }
}
