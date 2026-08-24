using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArturRios.Data.Relational.Core.Interfaces;

namespace ArturRios.Data.Tests.Interfaces;

[Trait("Category", "Unit")]
public class IAsyncRepositoryTests
{
    private static readonly Type Type = typeof(IAsyncRepository<>);

    [Fact]
    public void GivenTheAsynchronousRepositoryContract_WhenInspected_ThenItExtendsTheReadOnlyOne() =>
        Assert.Contains(typeof(IAsyncReadOnlyRepository<>),
            Type.GetInterfaces().Select(i => i.IsGenericType ? i.GetGenericTypeDefinition() : i));

    [Theory]
    [InlineData("CreateAsync")]
    [InlineData("CreateRangeAsync")]
    [InlineData("UpdateAsync")]
    [InlineData("UpdateRangeAsync")]
    [InlineData("DeleteAsync")]
    [InlineData("DeleteRangeAsync")]
    public void GivenTheAsynchronousWriteMethods_WhenInspected_ThenTheyReturnTaskOfDataOutputAndTakeACancellationToken(string name)
    {
        var m = Type.GetMethod(name)!;
        Assert.Equal(typeof(Task<>), m.ReturnType.GetGenericTypeDefinition());
        Assert.Contains(m.GetParameters(), p => p.ParameterType == typeof(CancellationToken));
    }
}
