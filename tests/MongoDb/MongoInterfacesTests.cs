using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArturRios.Data.MongoDb;
using ArturRios.Data.MongoDb.Interfaces;
using ArturRios.Output;

namespace ArturRios.Data.Tests.MongoDb;

[Trait("Category", "Unit")]
public class MongoInterfacesTests
{
    [Fact]
    public void GivenTheReadOnlyDocumentContract_WhenInspected_ThenItIsConstrainedToDocument()
    {
        var param = typeof(IDocumentReadOnlyRepository<>).GetGenericArguments()[0];
        Assert.Contains(typeof(Document), param.GetGenericParameterConstraints());
    }

    [Fact]
    public void GivenTheDocumentRepositoryContract_WhenInspected_ThenItExtendsTheReadOnlyOne() =>
        Assert.Contains(typeof(IDocumentReadOnlyRepository<>),
            typeof(IDocumentRepository<>).GetInterfaces()
                .Select(i => i.IsGenericType ? i.GetGenericTypeDefinition() : i));

    [Theory]
    [InlineData("Create")]
    [InlineData("Update")]
    [InlineData("Delete")]
    [InlineData("Find")]
    public void GivenTheSynchronousDocumentMethods_WhenInspected_ThenTheyReturnADataOutput(string name)
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
    public void GivenTheAsynchronousDocumentMethods_WhenInspected_ThenTheyReturnTaskOfDataOutputAndTakeACancellationToken(string name)
    {
        var m = typeof(IAsyncDocumentRepository<>).GetMethod(name) ??
                typeof(IAsyncDocumentReadOnlyRepository<>).GetMethod(name);
        Assert.NotNull(m);
        Assert.Equal(typeof(Task<>), m!.ReturnType.GetGenericTypeDefinition());
        Assert.Contains(m.GetParameters(), p => p.ParameterType == typeof(CancellationToken));
    }
}
