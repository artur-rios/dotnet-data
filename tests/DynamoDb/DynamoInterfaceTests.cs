using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArturRios.Data.DynamoDb.Interfaces;
using ArturRios.Output;

namespace ArturRios.Data.Tests.DynamoDb;

public class DynamoInterfaceTests
{
    private static readonly System.Type Type = typeof(IAsyncDynamoRepository<>);

    [Fact]
    public void GenericParameter_IsConstrainedToClass()
    {
        var param = Type.GetGenericArguments()[0];
        Assert.True((param.GenericParameterAttributes &
            System.Reflection.GenericParameterAttributes.ReferenceTypeConstraint) != 0);
    }

    [Theory]
    [InlineData("SaveAsync")]
    [InlineData("LoadAsync")]
    [InlineData("QueryAsync")]
    [InlineData("ScanAsync")]
    [InlineData("SaveManyAsync")]
    [InlineData("LoadManyAsync")]
    public void AsyncMethods_ReturnTaskOfDataOutput_AndTakeCancellationToken(string name)
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
    public void DeleteMethods_ReturnTaskOfProcessOutput(string name)
    {
        var m = Type.GetMethods().First(x => x.Name == name);
        Assert.Equal(typeof(Task<ProcessOutput>), m.ReturnType);
    }
}
