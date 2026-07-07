using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArturRios.Data.Relational.Core.Interfaces;

namespace ArturRios.Data.Tests.Interfaces;

public class IAsyncRepositoryTests
{
    private static readonly Type Type = typeof(IAsyncRepository<>);

    [Fact]
    public void ExtendsAsyncReadOnlyRepository() =>
        Assert.Contains(typeof(IAsyncReadOnlyRepository<>),
            Type.GetInterfaces().Select(i => i.IsGenericType ? i.GetGenericTypeDefinition() : i));

    [Theory]
    [InlineData("CreateAsync")]
    [InlineData("CreateRangeAsync")]
    [InlineData("UpdateAsync")]
    [InlineData("UpdateRangeAsync")]
    [InlineData("DeleteAsync")]
    [InlineData("DeleteRangeAsync")]
    public void AsyncWriteMethods_ReturnTaskOfDataOutput_AndTakeCancellationToken(string name)
    {
        var m = Type.GetMethod(name)!;
        Assert.Equal(typeof(Task<>), m.ReturnType.GetGenericTypeDefinition());
        Assert.Contains(m.GetParameters(), p => p.ParameterType == typeof(CancellationToken));
    }
}
