using ArturRios.Data.Relational.Core.Exceptions;
using ArturRios.Output;

namespace ArturRios.Data.Tests.Exceptions;

public class DataAccessExceptionTests
{
    [Fact]
    public void CarriesMessages_AndIsCustomException()
    {
        var ex = new DataAccessException(["a", "b"]);

        Assert.IsType<CustomException>(ex, exactMatch: false);
        Assert.Equal(["a", "b"], ex.Messages);
    }
}
