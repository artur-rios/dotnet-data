using System;
using System.Linq;
using ArturRios.Data.Relational.Core.Interfaces;
using ArturRios.Output;

namespace ArturRios.Data.Tests.Interfaces;

public class IRepositoryTests
{
    private static readonly Type Type = typeof(IRepository<>);

    [Fact]
    public void ExtendsReadOnlyRepository() =>
        Assert.Contains(typeof(IReadOnlyRepository<>),
            Type.GetInterfaces().Select(i => i.IsGenericType ? i.GetGenericTypeDefinition() : i));

    [Theory]
    [InlineData("Create")]
    [InlineData("CreateRange")]
    [InlineData("Update")]
    [InlineData("UpdateRange")]
    [InlineData("Delete")]
    [InlineData("DeleteRange")]
    public void WriteMethods_Exist_ReturningDataOutput(string name)
    {
        var m = Type.GetMethod(name)!;
        Assert.NotNull(m);
        Assert.Equal(typeof(DataOutput<>), m.ReturnType.GetGenericTypeDefinition());
    }
}
