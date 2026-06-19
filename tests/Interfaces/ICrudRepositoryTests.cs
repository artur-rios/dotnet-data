using System;
using System.Linq;
using System.Reflection;
using ArturRios.Data;
using ArturRios.Data.Interfaces;

namespace ArturRios.Data.Tests.Interfaces;

public class ICrudRepositoryTests
{
    private static readonly Type InterfaceType = typeof(ICrudRepository<>);

    [Fact]
    public void ICrudRepository_IsInterface()
    {
        Assert.True(InterfaceType.IsInterface);
    }

    [Fact]
    public void ICrudRepository_GenericParameter_IsConstrainedToEntity()
    {
        var param = InterfaceType.GetGenericArguments()[0];
        var constraints = param.GetGenericParameterConstraints();

        Assert.Contains(typeof(Entity), constraints);
    }

    [Fact]
    public void ICrudRepository_HasCreate_WithTParameter_ReturningInt()
    {
        var method = InterfaceType.GetMethod("Create");

        Assert.NotNull(method);
        var parameters = method.GetParameters();
        Assert.Single(parameters);
        Assert.Equal("entity", parameters[0].Name);
        Assert.True(parameters[0].ParameterType.IsGenericParameter);
        Assert.Equal(typeof(int), method.ReturnType);
    }

    [Fact]
    public void ICrudRepository_HasGetAll_ReturningIQueryableOfT()
    {
        var method = InterfaceType.GetMethod("GetAll");

        Assert.NotNull(method);
        Assert.Empty(method.GetParameters());
        Assert.True(method.ReturnType.IsGenericType);
        Assert.Equal(typeof(IQueryable<>), method.ReturnType.GetGenericTypeDefinition());
    }

    [Fact]
    public void ICrudRepository_HasGetById_WithIntParameter_Named_Id()
    {
        var method = InterfaceType.GetMethod("GetById");

        Assert.NotNull(method);
        var parameters = method.GetParameters();
        Assert.Single(parameters);
        Assert.Equal(typeof(int), parameters[0].ParameterType);
        Assert.Equal("id", parameters[0].Name);
    }

    [Fact]
    public void ICrudRepository_GetById_ReturnsT()
    {
        var method = InterfaceType.GetMethod("GetById");
        Assert.NotNull(method);

        Assert.True(method.ReturnType.IsGenericParameter);
    }

    [Fact]
    public void ICrudRepository_HasUpdate_WithTParameter_ReturningT()
    {
        var method = InterfaceType.GetMethod("Update");

        Assert.NotNull(method);
        var parameters = method.GetParameters();
        Assert.Single(parameters);
        Assert.Equal("entity", parameters[0].Name);
        Assert.True(parameters[0].ParameterType.IsGenericParameter);
        Assert.True(method.ReturnType.IsGenericParameter);
    }

    [Fact]
    public void ICrudRepository_HasDelete_WithTParameter_ReturningInt()
    {
        var method = InterfaceType.GetMethod("Delete");

        Assert.NotNull(method);
        var parameters = method.GetParameters();
        Assert.Single(parameters);
        Assert.Equal("entity", parameters[0].Name);
        Assert.True(parameters[0].ParameterType.IsGenericParameter);
        Assert.Equal(typeof(int), method.ReturnType);
    }
}
