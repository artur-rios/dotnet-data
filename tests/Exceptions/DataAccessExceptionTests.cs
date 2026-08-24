using ArturRios.Data.Relational.Core.Exceptions;
using ArturRios.Output;

namespace ArturRios.Data.Tests.Exceptions;

[Trait("Category", "Unit")]
public class DataAccessExceptionTests
{
    [Fact]
    public void GivenMessages_WhenConstructingTheException_ThenItCarriesThemAndIsACustomException()
    {
        var ex = new DataAccessException(["a", "b"]);

        Assert.IsType<CustomException>(ex, false);
        Assert.Equal(["a", "b"], ex.Messages);
    }
}
