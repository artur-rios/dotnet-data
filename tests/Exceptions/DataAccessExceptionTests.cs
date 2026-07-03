using ArturRios.Data.Core.Exceptions;
using ArturRios.Output;

namespace ArturRios.Data.Tests.Exceptions;

public class DataAccessExceptionTests
{
    [Fact]
    public void CarriesMessages_AndIsCustomException()
    {
        var ex = new DataAccessException(["a", "b"]);
        Assert.IsAssignableFrom<CustomException>(ex);
        Assert.Equal(["a", "b"], ex.Messages);
    }
}
