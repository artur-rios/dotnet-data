using System;
using System.Threading;
using System.Threading.Tasks;
using ArturRios.Data.Relational.Core.Interfaces;
using ArturRios.Output;

namespace ArturRios.Data.Tests.Interfaces;

[Trait("Category", "Unit")]
public class IAsyncReadOnlyRepositoryTests
{
    private static readonly Type Type = typeof(IAsyncReadOnlyRepository<>);

    [Theory]
    [InlineData("GetAllAsync")]
    [InlineData("GetByIdAsync")]
    public void GivenTheAsynchronousContract_WhenInspected_ThenEveryMethodReturnsTaskOfDataOutputAndTakesACancellationToken(string name)
    {
        var m = Type.GetMethod(name)!;
        Assert.Equal(typeof(Task<>), m.ReturnType.GetGenericTypeDefinition());
        var inner = m.ReturnType.GetGenericArguments()[0];
        Assert.Equal(typeof(DataOutput<>), inner.GetGenericTypeDefinition());
        Assert.Contains(m.GetParameters(), p => p.ParameterType == typeof(CancellationToken));
    }
}
