using ArturRios.Data.MongoDb;

namespace ArturRios.Data.Tests.MongoDb;

public class CollectionNameTests
{
    [Fact]
    public void For_UsesTypeName_WhenNoAttribute() => Assert.Equal("Plain", CollectionName.For<Plain>());

    [Fact]
    public void For_UsesAttributeName_WhenPresent() => Assert.Equal("custom_things", CollectionName.For<Annotated>());

    private sealed class Plain : Document
    {
    }

    [MongoCollection("custom_things")]
    private sealed class Annotated : Document
    {
    }
}
