using ArturRios.Data.Export.Abstractions;
using ArturRios.Data.Tests.Export.TestSupport;

namespace ArturRios.Data.Tests.Export;

[Trait("Category", "Unit")]
public class ColumnMapTests
{
    [Fact]
    public void GivenAPropertyMarkedIgnored_WhenBuildingTheColumnMap_ThenItIsExcluded()
    {
        var columns = ColumnMap.For<AttributedRow>();
        Assert.DoesNotContain(columns, c => c.Header == "Internal");
    }

    [Fact]
    public void GivenNameAndOrderOverrides_WhenBuildingTheColumnMap_ThenTheyAreApplied()
    {
        var headers = ColumnMap.For<AttributedRow>().Select(c => c.Header).ToArray();
        // Id has Order=0 and Name="Identifier"; Name has default order (int.MaxValue).
        Assert.Equal(new[] { "Identifier", "Name" }, headers);
    }

    [Fact]
    public void GivenNoOverrides_WhenBuildingTheColumnMap_ThenDeclarationOrderAndPropertyNamesAreUsed()
    {
        var headers = ColumnMap.For<Widget>().Select(c => c.Header).ToArray();
        Assert.Equal(new[] { "Id", "Name", "Price" }, headers);
    }

    [Fact]
    public void GivenARecord_WhenInvokingAColumnGetter_ThenThePropertyValueComesBack()
    {
        var column = ColumnMap.For<Widget>().Single(c => c.Header == "Name");
        Assert.Equal("gizmo", column.Getter(new Widget { Name = "gizmo" }));
    }

    [Fact]
    public void GivenTheSameRecordType_WhenBuildingTheColumnMapTwice_ThenTheCachedInstanceComesBack()
    {
        Assert.Same(ColumnMap.For<Widget>(), ColumnMap.For<Widget>());
    }
}
