using ArturRios.Data.MongoDb;

namespace ArturRios.Data.Tests.MongoDb;

[Trait("Category", "Unit")]
public class CollectionNameTests
{
    [Fact]
    public void GivenNoCollectionAttribute_WhenResolvingTheCollectionName_ThenTheTypeNameIsUsed() => Assert.Equal("Plain", CollectionName.For<Plain>());

    [Fact]
    public void GivenACollectionAttribute_WhenResolvingTheCollectionName_ThenTheAttributeNameIsUsed() => Assert.Equal("custom_things", CollectionName.For<Annotated>());

    private sealed class Plain : Document
    {
    }

    [MongoCollection("custom_things")]
    private sealed class Annotated : Document
    {
    }
}
