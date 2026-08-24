using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using ArturRios.Data.DynamoDb.Interfaces;
using ArturRios.Output;

namespace ArturRios.Data.Tests.DynamoDb;

[Trait("Category", "Unit")]
public class DynamoInterfaceTests
{
    private static readonly Type Type = typeof(IAsyncDynamoRepository<>);

    [Fact]
    public void GivenTheRepositoryContract_WhenInspected_ThenItsTypeParameterIsConstrainedToAClass()
    {
        var param = Type.GetGenericArguments()[0];
        Assert.True((param.GenericParameterAttributes &
                     GenericParameterAttributes.ReferenceTypeConstraint) != 0);
    }

    [Theory]
    [InlineData("SaveAsync")]
    [InlineData("LoadAsync")]
    [InlineData("QueryAsync")]
    [InlineData("ScanAsync")]
    [InlineData("SaveManyAsync")]
    [InlineData("LoadManyAsync")]
    public void GivenTheAsynchronousContract_WhenInspected_ThenEveryMethodReturnsTaskOfDataOutputAndTakesACancellationToken(string name)
    {
        var m = Type.GetMethods().First(x => x.Name == name);
        Assert.Equal(typeof(Task<>), m.ReturnType.GetGenericTypeDefinition());
        var inner = m.ReturnType.GetGenericArguments()[0];
        Assert.Equal(typeof(DataOutput<>), inner.GetGenericTypeDefinition());
        Assert.Contains(m.GetParameters(), p => p.ParameterType == typeof(CancellationToken));
    }

    [Theory]
    [InlineData("DeleteAsync")]
    [InlineData("DeleteManyAsync")]
    public void GivenTheDeleteMethods_WhenInspected_ThenTheyReturnTaskOfProcessOutput(string name)
    {
        var m = Type.GetMethods().First(x => x.Name == name);
        Assert.Equal(typeof(Task<ProcessOutput>), m.ReturnType);
    }
}
