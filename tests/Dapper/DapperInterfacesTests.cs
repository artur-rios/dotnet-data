using System.Threading;
using System.Threading.Tasks;
using ArturRios.Data.Dapper;
using ArturRios.Output;

namespace ArturRios.Data.Tests.Dapper;

public class DapperInterfacesTests
{
    [Theory]
    [InlineData("Query")]
    [InlineData("QueryFirstOrDefault")]
    [InlineData("QuerySingleOrDefault")]
    [InlineData("ExecuteScalar")]
    public void ISqlQuery_Methods_AreGeneric_ReturningDataOutput(string name)
    {
        var m = typeof(ISqlQuery).GetMethod(name)!;
        Assert.NotNull(m);
        Assert.True(m.IsGenericMethodDefinition);
        Assert.Equal(typeof(DataOutput<>), m.ReturnType.GetGenericTypeDefinition());
    }

    [Theory]
    [InlineData("QueryAsync")]
    [InlineData("QueryFirstOrDefaultAsync")]
    [InlineData("QuerySingleOrDefaultAsync")]
    [InlineData("ExecuteScalarAsync")]
    public void IAsyncSqlQuery_Methods_ReturnTaskOfDataOutput_AndTakeCancellationToken(string name)
    {
        var m = typeof(IAsyncSqlQuery).GetMethod(name)!;
        Assert.NotNull(m);
        Assert.True(m.IsGenericMethodDefinition);
        Assert.Equal(typeof(Task<>), m.ReturnType.GetGenericTypeDefinition());
        var inner = m.ReturnType.GetGenericArguments()[0];
        Assert.Equal(typeof(DataOutput<>), inner.GetGenericTypeDefinition());
        Assert.Contains(m.GetParameters(), p => p.ParameterType == typeof(CancellationToken));
    }
}
