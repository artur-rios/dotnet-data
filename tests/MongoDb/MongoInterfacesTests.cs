using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArturRios.Data.MongoDb;
using ArturRios.Data.MongoDb.Interfaces;
using ArturRios.Output;

namespace ArturRios.Data.Tests.MongoDb;

public class MongoInterfacesTests
{
    [Fact]
    public void ReadOnly_IsConstrainedToDocument()
    {
        var param = typeof(IDocumentReadOnlyRepository<>).GetGenericArguments()[0];
        Assert.Contains(typeof(Document), param.GetGenericParameterConstraints());
    }

    [Fact]
    public void Repository_ExtendsReadOnly() =>
        Assert.Contains(typeof(IDocumentReadOnlyRepository<>),
            typeof(IDocumentRepository<>).GetInterfaces()
                .Select(i => i.IsGenericType ? i.GetGenericTypeDefinition() : i));

    [Theory]
    [InlineData("Create")]
    [InlineData("Update")]
    [InlineData("Delete")]
    [InlineData("Find")]
    public void SyncMethods_ReturnDataOutput(string name)
    {
        var m = typeof(IDocumentRepository<>).GetMethod(name) ?? typeof(IDocumentReadOnlyRepository<>).GetMethod(name);
        Assert.NotNull(m);
        Assert.Equal(typeof(DataOutput<>), m!.ReturnType.GetGenericTypeDefinition());
    }

    [Theory]
    [InlineData("CreateAsync")]
    [InlineData("UpdateAsync")]
    [InlineData("DeleteAsync")]
    [InlineData("FindAsync")]
    public void AsyncMethods_ReturnTaskOfDataOutput_WithCancellationToken(string name)
    {
        var m = typeof(IAsyncDocumentRepository<>).GetMethod(name) ??
                typeof(IAsyncDocumentReadOnlyRepository<>).GetMethod(name);
        Assert.NotNull(m);
        Assert.Equal(typeof(Task<>), m!.ReturnType.GetGenericTypeDefinition());
        Assert.Contains(m.GetParameters(), p => p.ParameterType == typeof(CancellationToken));
    }
}
